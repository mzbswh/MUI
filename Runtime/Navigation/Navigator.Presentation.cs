using System;
using System.Collections.Generic;
using System.Linq;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private bool recomputingPresentation;
        private bool presentationDirty;
        private bool presentationOrderDirty;
        private int presentationDeferrals;

        private void RecomputePresentation() => RecomputePresentation(updateOrder: true);

        private void RecomputePresentation(bool updateOrder)
        {
            presentationDirty = true;
            presentationOrderDirty |= updateOrder;
            if (recomputingPresentation || presentationDeferrals != 0)
            {
                return;
            }

            recomputingPresentation = true;
            ViewInstance[] readySnapshot;
            try
            {
                while (presentationDirty)
                {
                    var reorder = presentationOrderDirty;
                    presentationDirty = false;
                    presentationOrderDirty = false;
                    RecomputePresentationCore(reorder);
                }

                // 渲染门控、覆盖关系与焦点回调均已收敛。替换流程的
                // 延迟提交也在此完成，避免界面仍隐藏时就报告就绪。
                readySnapshot = activeOrder.ToArray();
                CompleteReadinessSnapshot(readySnapshot);
            }
            finally
            {
                recomputingPresentation = false;
            }

            // 回调可能请求关闭或引发新的表现重算；在重算保护退出后按稳定快照派发。
            foreach (var instance in readySnapshot)
            {
                using (EnterCallback(instance))
                {
                    instance.PublishReadinessObservers();
                }
            }
        }

        private void RecomputePresentationCore(bool updateOrder)
        {
            activeOrder.Sort((left, right) =>
            {
                var layer = left.Route.Policy.Layer.CompareTo(right.Route.Policy.Layer);
                return layer != 0 ? layer : left.Order.CompareTo(right.Order);
            });
            if (ownership.HasDependencyEdges)
            {
                var ordered = ownership.OrderPresentation(activeOrder.ToArray());
                activeOrder.Clear();
                activeOrder.AddRange(ordered);
            }
            var visible = activeOrder.Where(entry => entry.State == ViewState.Open || entry.ExitPending).ToArray();
            var hiddenBelow = false;
            var blockedBelow = false;
            var hiddenBy = default(ViewHandle);
            var blockedBy = default(ViewHandle);
            var coverageChanges = new List<(ViewInstance instance, bool covered)>();
            for (var i = visible.Length - 1; i >= 0; --i)
            {
                var entry = visible[i];
                entry.HostVisible = (entry.ActivationCommitted || entry.ExitPending) && !IsShutdown && !hiddenBelow;
                entry.HostInteractable = entry.State == ViewState.Open && entry.HostVisible && !blockedBelow && !entry.EnterPending;
                var covered = hiddenBelow || blockedBelow;
                entry.Covered = covered;
                entry.HiddenBy = hiddenBy;
                entry.BlockedBy = blockedBy;
                if (entry.NotifiedCovered != covered)
                {
                    coverageChanges.Add((entry, covered));
                }

                if (entry.Route.Policy.Coverage == CoveragePolicy.Hide)
                {
                    hiddenBelow = true;
                    hiddenBy = entry.Handle;
                }

                if (entry.Route.Policy.Coverage != CoveragePolicy.None)
                {
                    blockedBelow = true;
                    blockedBy = entry.Handle;
                }
            }

            foreach (var entry in visible)
            {
                if (entry.State != ViewState.Open && !entry.ExitPending)
                {
                    continue;
                }

                try
                {
                    if (entry.View == null || !entry.View.IsAlive)
                    {
                        throw new InvalidOperationException("An active View was externally destroyed.");
                    }

                    if (updateOrder && entry.View is IOrderedView ordered)
                    {
                        ordered.MoveToFront();
                    }

                    if (updateOrder && entry.View is IModalView modal)
                    {
                        modal.SetModalBarrier(entry.Route.Policy.Modal && entry.HostVisible);
                    }

                    if (entry.ExitPending && entry.View is IExitTransitionView exitingView)
                    {
                        using (EnterCallback(entry))
                        {
                            exitingView.SetExitVisible(entry.HostVisible);
                            entry.View.SetHostState(false, false);
                        }
                    }
                    else if (entry.State == ViewState.Open)
                    {
                        entry.View.SetHostState(entry.HostVisible, entry.HostInteractable);
                    }
                }
                catch (Exception failure)
                {
                    entry.SetFailure(failure);
                    UIErrors.Report(failure);
                    if (entry.ExitPending)
                    {
                        FinishExit(entry, failure);
                    }
                    else
                    {
                        CloseAfterFailure(entry, DismissReason.OpenFailed);
                    }
                }
            }

            // 先收敛渲染门控，其事件可能要求再执行一轮更新。
            if (presentationDirty)
            {
                return;
            }

            focused = default;
            for (var i = visible.Length - 1; i >= 0; --i)
            {
                var entry = visible[i];
                if (entry.State != ViewState.Open || !entry.HostInteractable || !entry.Route.Policy.TakesFocus || entry.ExplicitFocusReleased)
                {
                    continue;
                }

                if (entry.View is IInputView input && !input.IsInputEnabled)
                {
                    continue;
                }

                focused = entry.Handle;
                break;
            }

            var changes = new List<(ViewInstance instance, bool focused)>();
            foreach (var entry in entries.Values.ToArray())
            {
                var desired = entry.State == ViewState.Open && entry.Handle == focused;
                entry.Focused = desired;
                if (entry.NotifiedFocused != desired)
                {
                    changes.Add((entry, desired));
                }
            }

            // 先失去焦点再取得焦点，防止旧输入处理器禁用新的焦点拥有者。
            changes.Sort((left, right) => left.focused.CompareTo(right.focused));
            foreach (var change in changes.Where(change => !change.focused))
            {
                NotifyFocusChange(change.instance, false);
            }

            foreach (var change in coverageChanges)
            {
                if (presentationDirty)
                {
                    break;
                }

                if (change.instance.State != ViewState.Open || !change.instance.ActivationCommitted || change.instance.Covered != change.covered)
                {
                    continue;
                }

                try
                {
                    change.instance.NotifiedCovered = change.covered;
                    using (EnterCallback(change.instance))
                    {
                        change.instance.NotifyCoverage(change.covered);
                    }
                }
                catch (Exception failure)
                {
                    change.instance.SetFailure(failure);
                    UIErrors.Report(failure);
                    CloseAfterFailure(change.instance, DismissReason.OpenFailed);
                }
            }

            foreach (var change in changes)
            {
                if (presentationDirty)
                {
                    break;
                }

                if (change.focused)
                {
                    NotifyFocusChange(change.instance, true);
                }
            }
        }

        private void NotifyFocusChange(ViewInstance instance, bool desired)
        {
            if (instance.Focused != desired || (instance.State != ViewState.Open && instance.State != ViewState.Closing))
            {
                return;
            }

            if (instance.NotifiedFocused == desired)
            {
                return;
            }

            try
            {
                instance.NotifiedFocused = desired;
                using (EnterCallback(instance))
                {
                    if (instance.View is IFocusView native && native.IsAlive)
                    {
                        native.SetFocused(desired);
                    }

                    instance.NotifyFocus(desired);
                }
            }
            catch (Exception failure)
            {
                instance.SetFailure(failure);
                UIErrors.Report(failure);
                CloseAfterFailure(instance, DismissReason.OpenFailed);
            }
        }

        internal void InputStateChanged(ViewInstance instance)
        {
            AssertThread();
            if (instance.State == ViewState.Open)
            {
                // 原生激活回调中的输入变化只更新门控与焦点，不能重排正在改变状态的层级。
                RecomputePresentation(updateOrder: false);
            }
        }

        private void MaintainNativeFocus()
        {
            var modal = activeOrder.LastOrDefault(entry => entry.State == ViewState.Open && entry.HostVisible && entry.HostInteractable && entry.Route.Policy.Modal);
            var target = modal;
            if (target == null)
            {
                entries.TryGetValue(focused, out target);
            }

            if (target == null || !(target.View is IFocusView native) || !native.IsAlive)
            {
                return;
            }

            try
            {
                using (EnterCallback(target))
                {
                    native.ConstrainFocus();
                }
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }

        public bool BringToFront(ViewHandle handle)
        {
            AssertThread();
            if (IsReentrant)
            {
                return false;
            }

            if (!entries.TryGetValue(handle, out var entry) || entry.State != ViewState.Open)
            {
                return false;
            }

            entry.Order = ++order;
            if (entry.Route.Policy.EnterHistory)
            {
                history.Remove(handle);
                history.Add(handle);
            }

            RecomputePresentation();
            return entry.State == ViewState.Open;
        }

        public bool TryGetViewModel<TViewModel>(ViewHandle handle, out TViewModel model)
                    where TViewModel : ViewModel
        {
            AssertThread();
            model = entries.TryGetValue(handle, out var entry) && entry.State == ViewState.Open ? entry.Model as TViewModel : null;
            return model != null;
        }

        public ViewState GetState(ViewHandle handle)
        {
            AssertThread();
            if (entries.TryGetValue(handle, out var instance))
            {
                return instance.State;
            }

            if (TryGetTerminal(handle, out var outcome))
            {
                return outcome.Status == CloseStatus.Failed ? ViewState.Failed : ViewState.Destroyed;
            }

            return IsExpired(handle) ? ViewState.UnknownOrExpired : ViewState.Unknown;
        }

        private bool IsExpired(ViewHandle handle) => handle.Host == host && handle.Id > 0 && handle.Id <= nextHandle;
    }
}
