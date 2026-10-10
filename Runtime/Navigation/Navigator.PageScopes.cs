using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private readonly HashSet<ViewHandle> pageDepartures = new HashSet<ViewHandle>();

        public ViewHandle CurrentMainPage
        {
            get
            {
                AssertThread();
                return activeOrder.Where(entry => entry.State == ViewState.Open && entry.ActivationCommitted &&
                    entry.Route.Policy.PageRole == PageRole.Main && ownership.HasExplicitOwner(entry.Handle))
                    .OrderByDescending(entry => entry.Order).Select(entry => entry.Handle).FirstOrDefault();
            }
        }

        public ViewHandle GetParentPage(ViewHandle page)
        {
            AssertThread();
            return entries.TryGetValue(page, out var instance) ? instance.ParentPage : default;
        }

        private static NavigationPreparationRejectedException PageScopeRejected(OpenRejection reason) =>
            new NavigationPreparationRejectedException(reason, "页面归属无效、已离开前台，或页面切换被拒绝。");

        private ViewHandle ResolvePageOwner(RoutePolicy policy, PageOwner owner)
        {
            if (!Enum.IsDefined(typeof(PageOwnerKind), owner.Kind))
            {
                throw PageScopeRejected(OpenRejection.ConflictingData);
            }
            if (policy.PageRole != PageRole.Overlay)
            {
                if (owner.Kind == PageOwnerKind.Page)
                {
                    throw PageScopeRejected(OpenRejection.ConflictingData);
                }
                return default;
            }
            var parent = owner.Kind == PageOwnerKind.Host ? default :
                owner.Kind == PageOwnerKind.Page ? owner.Page : CurrentMainPage;
            if (owner.Kind != PageOwnerKind.Host && !parent.IsValid)
            {
                throw PageScopeRejected(OpenRejection.ConflictingData);
            }
            RequirePageOwner(parent);
            return parent;
        }

        private void RequirePageOwner(ViewHandle parent)
        {
            if (!parent.IsValid)
            {
                return;
            }
            if (!entries.TryGetValue(parent, out var instance) || instance.State != ViewState.Open ||
                instance.HasCloseStarted || !instance.HostVisible || instance.PendingCloseIntent.HasValue || instance.CloseRequest != null ||
                !IsInPageScope(instance, CurrentMainPage, new HashSet<ViewHandle>()))
            {
                throw PageScopeRejected(OpenRejection.Superseded);
            }
            for (var current = instance; current != null;)
            {
                if (pageDepartures.Contains(current.Handle))
                {
                    throw PageScopeRejected(OpenRejection.Busy);
                }
                current = current.ParentPage.IsValid && entries.TryGetValue(current.ParentPage, out var next) ? next : null;
            }
        }

        private bool IsInPageScope(ViewInstance instance, ViewHandle main, HashSet<ViewHandle> visiting)
        {
            if (!visiting.Add(instance.Handle))
            {
                return false;
            }
            try
            {
                if (instance.ParentPage.IsValid)
                {
                    return entries.TryGetValue(instance.ParentPage, out var parent) && parent.State == ViewState.Open &&
                        IsInPageScope(parent, main, visiting);
                }
                if (!ownership.HasExplicitOwner(instance.Handle) && ownership.HasOwners(instance.Handle))
                {
                    return ownership.CaptureOwners(instance.Handle).Any(handle => entries.TryGetValue(handle, out var parent) &&
                        parent.State == ViewState.Open && IsInPageScope(parent, main, visiting));
                }
                return instance.Route.Policy.PageRole != PageRole.Main || instance.Handle == main;
            }
            finally { visiting.Remove(instance.Handle); }
        }

        private bool IsPageDescendant(ViewInstance instance, ViewHandle ancestor)
        {
            var parent = instance.ParentPage;
            while (parent.IsValid)
            {
                if (parent == ancestor)
                {
                    return true;
                }
                if (!entries.TryGetValue(parent, out var next))
                {
                    return false;
                }
                parent = next.ParentPage;
            }
            return false;
        }

        private bool HasPageDescendants(ViewHandle parent) => entries.Values.Any(entry =>
            !entry.HasCloseStarted && IsPageDescendant(entry, parent));

        private Task<CloseOutcome>[] ClosePageDescendants(ViewInstance parent)
        {
            var closing = new List<Task<CloseOutcome>>();
            foreach (var child in entries.Values.Where(entry => IsPageDescendant(entry, parent.Handle))
                .OrderByDescending(entry => entry.Order).ToArray())
            {
                try
                {
                    closing.Add(child.Closing ?? BeginClose(child, DismissReason.Forced));
                }
                catch (Exception error)
                {
                    closing.Add(Task.FromResult(new CloseOutcome(CloseStatus.Failed, error, CleanupStatus.Failed)));
                }
            }
            return closing.ToArray();
        }

        private async Task<CloseOutcome> ClosePageGroupAsync(ViewInstance root, DismissReason reason, Action acceptResult)
        {
            try
            {
                using (var departure = await PreparePageDepartureAsync(root, true, shutdown.Token, reason: reason, isCompletion: acceptResult != null))
                {
                    departure.Validate();
                    presentationDeferrals++;
                    Task<CloseOutcome> closing;
                    try
                    {
                        departure.Commit();
                        closing = BeginClose(root, reason, acceptResult);
                    }
                    finally { presentationDeferrals--; RecomputePresentation(); }
                    return await closing;
                }
            }
            catch (NavigationPreparationRejectedException error)
            {
                return new CloseOutcome(error.Rejection == OpenRejection.CloseDenied ? CloseStatus.Denied :
                    error.Rejection == OpenRejection.ConfirmationUnavailable ? CloseStatus.ConfirmationUnavailable : CloseStatus.Superseded);
            }
            catch (OperationCanceledException) { return new CloseOutcome(CloseStatus.Superseded); }
            catch (Exception error) { UIErrors.Report(error); return new CloseOutcome(CloseStatus.Failed, error); }
        }

        private async Task<PageDepartureTransaction> PreparePageDepartureAsync(ViewInstance root, bool closingRoot,
            CancellationToken token, bool guardRoot = true, DismissReason reason = DismissReason.Replaced, bool isCompletion = false)
        {
            var descendants = entries.Values.Where(entry => !entry.HasCloseStarted && IsPageDescendant(entry, root.Handle)).ToArray();
            var closing = descendants.Where(entry => closingRoot || entry.Route.Policy.OwnerDeparture == OwnerDeparture.Close ||
                descendants.Any(parent => parent.Route.Policy.OwnerDeparture == OwnerDeparture.Close && IsPageDescendant(entry, parent.Handle)))
                .OrderByDescending(entry => entry.Order).ToArray();
            var checkedPages = closing.Where(entry => entry.State == ViewState.Open).ToList();
            if (guardRoot)
            {
                checkedPages.Add(root);
            }
            var transaction = new PageDepartureTransaction(this, root, descendants, closing, checkedPages.ToArray());
            try
            {
                transaction.Acquire();
                foreach (var entry in checkedPages)
                {
                    token.ThrowIfCancellationRequested();
                    if (!entry.HasCloseGuard)
                    {
                        continue;
                    }
                    CloseApproval approval = default;
                    var result = await AwaitCloseEvaluationAsync(entry, activationToken =>
                        EvaluatePageDepartureAsync(entry, reason, token, activationToken, value => approval = value, isCompletion && entry == root), token);
                    if (result.Error is TimeoutException)
                    {
                        throw PageScopeRejected(OpenRejection.CloseDecisionTimedOut);
                    }
                    if (result.Error != null)
                    {
                        throw result.Error;
                    }
                    if (result.Status != CloseStatus.Closed)
                    {
                        throw PageScopeRejected(result.Status == CloseStatus.Denied ? OpenRejection.CloseDenied :
                            result.Status == CloseStatus.ConfirmationUnavailable ? OpenRejection.ConfirmationUnavailable : OpenRejection.Superseded);
                    }
                    transaction.Approvals.Add(entry.Handle, approval);
                }
                transaction.Validate();
                return transaction;
            }
            catch { transaction.Dispose(); throw; }
        }

        private async ValueTask<CloseStatus> EvaluatePageDepartureAsync(ViewInstance entry, DismissReason reason,
            CancellationToken token, CancellationToken activation, Action<CloseApproval> approve, bool isCompletion)
        {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, activation))
            {
                return await EvaluateDecisionAsync(entry, new CloseContext(entry.Handle, reason, isCompletion), linked.Token, approve);
            }
        }

        private sealed class PageDepartureTransaction : IDisposable
        {
            private readonly Navigator navigator;
            private readonly ViewInstance root;
            private readonly ViewInstance[] descendants;
            private readonly ViewInstance[] closing;
            private readonly ViewInstance[] checkedPages;
            private readonly List<ViewInstance> intents = new List<ViewInstance>();
            private readonly List<ViewInstance> locked = new List<ViewInstance>();
            private readonly Dictionary<ViewHandle, long> versions = new Dictionary<ViewHandle, long>();
            internal readonly Dictionary<ViewHandle, CloseApproval> Approvals = new Dictionary<ViewHandle, CloseApproval>();

            internal PageDepartureTransaction(Navigator navigator, ViewInstance root, ViewInstance[] descendants,
                ViewInstance[] closing, ViewInstance[] checkedPages)
            {
                this.navigator = navigator;
                this.root = root;
                this.descendants = descendants;
                this.closing = closing;
                this.checkedPages = checkedPages;
            }

            internal void Acquire()
            {
                foreach (var entry in new[] { root }.Concat(descendants))
                {
                    if (!navigator.pageDepartures.Add(entry.Handle))
                    {
                        throw PageScopeRejected(OpenRejection.Busy);
                    }
                    locked.Add(entry);
                    versions.Add(entry.Handle, entry.CommitVersion);
                }
                foreach (var entry in checkedPages)
                {
                    if (entry.PendingCloseIntent.HasValue || entry.CloseRequest != null || entry.IsUpdatingArgs || entry.IsRebinding ||
                        navigator.ownership.HasOwners(entry.Handle) || navigator.detachedCloseWaits.Contains(entry.Handle))
                    {
                        throw PageScopeRejected(OpenRejection.Busy);
                    }
                }
                foreach (var entry in checkedPages)
                {
                    entry.PendingCloseIntent = CloseRequestIntent.Replace;
                    intents.Add(entry);
                }
            }

            internal void Validate()
            {
                foreach (var entry in locked)
                {
                    if (!navigator.entries.ContainsKey(entry.Handle) || entry.HasCloseStarted ||
                        entry.CommitVersion != versions[entry.Handle])
                    {
                        throw PageScopeRejected(OpenRejection.Superseded);
                    }
                }
                foreach (var entry in checkedPages)
                {
                    if (!entry.HasCloseGuard)
                    {
                        continue;
                    }
                    using (navigator.EnterCallback(entry))
                    {
                        if (!Approvals.TryGetValue(entry.Handle, out var approval) || !approval.IsCurrent(entry, entry.CloseGuardVersion))
                        {
                            throw PageScopeRejected(OpenRejection.Superseded);
                        }
                    }
                }
                // 外部版本读取器可能触发状态变化，返回后再核对账本。
                if (locked.Any(entry => entry.HasCloseStarted || entry.CommitVersion != versions[entry.Handle]))
                {
                    throw PageScopeRejected(OpenRejection.Superseded);
                }
            }

            internal void Commit()
            {
                foreach (var entry in closing)
                {
                    Observe(navigator.BeginClose(entry, DismissReason.Forced));
                }
            }

            public void Dispose()
            {
                foreach (var entry in locked)
                {
                    navigator.pageDepartures.Remove(entry.Handle);
                }
                foreach (var entry in intents)
                {
                    if (entry.PendingCloseIntent == CloseRequestIntent.Replace)
                    {
                        entry.PendingCloseIntent = null;
                    }
                }
            }
        }
    }
}
