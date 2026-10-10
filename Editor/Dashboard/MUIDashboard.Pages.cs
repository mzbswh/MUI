using System;
using System.Linq;
using System.Text;
using MUI.Navigation;
using MUI.UGUI;
using UnityEditor;
using UnityEngine.UIElements;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public sealed partial class MUIDashboard
    {
        private void RebuildPages()
        {
            if (pageList == null)
            {
                return;
            }
            updating = true;
            try
            {
                pages.Clear();
                if (snapshot != null)
                {
                    var query = search.value ?? string.Empty;
                    pages.AddRange(snapshot.Instances.Where(item => item.RouteKey.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        item.Handle.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                        .OrderByDescending(item => item.PresentationIndex).ThenByDescending(item => item.Handle.Id));
                }
                pageList.ClearSelection();
                pageList.Rebuild();
                RefreshEmptyPages();
                var index = pages.FindIndex(item => item.Handle == selectedHandle);
                if (index >= 0)
                {
                    pageList.SetSelection(index);
                    ShowPage(pages[index]);
                }
                else
                {
                    selectedHandle = default;
                    viewDiagnostics = null;
                    ShowOverview();
                }
            }
            finally
            {
                updating = false;
            }
        }

        private void ShowOverview()
        {
            details.Clear();
            if (snapshot == null)
            {
                ShowNoSnapshot();
                return;
            }
            AddText(SnapshotSummary());
            if (snapshot.RenderOrder != null)
            {
                AddText(L.Format("sorting.pool", snapshot.RenderOrder.Minimum, snapshot.RenderOrder.Maximum,
                    snapshot.ReservedRenderOrders, snapshot.RenderOrder.PreferredGap, snapshot.RenderOrder.PreferredLayerGap));
            }
            AddText(L.Get("policy.cache.history"), heading: true);
            foreach (var decision in snapshot.CacheDiagnostics)
            {
                AddText(L.Format("policy.cache.entry", decision.TimestampUtc.ToLocalTime(), decision.RouteKey, decision.Decision));
            }
            AddText(L.Get("editor.MUIDashboard.Pages.a800e96c6e") + snapshot.HostId.ToString("N"));
            AddHandle(L.Get("editor.MUIDashboard.Pages.b4929391ad"), snapshot.Focused);
            AddText(L.Get("editor.MUIDashboard.Pages.24c1f7ab67"), heading: true);
            foreach (var handle in snapshot.RecentHistory)
            {
                AddHandle(L.Get("editor.MUIDashboard.Pages.452c7b10d5"), handle);
            }
            AddText(L.Get("editor.MUIDashboard.Pages.498b52a901"), heading: true);
            foreach (var item in snapshot.CleanupResponsibilities)
            {
                AddText(L.Format("editor.MUIDashboard.Pages.81145b2e23", item.Id, item.Owner, item.State, item.Attempts, item.CanRetry, item.Context));
                if (item.Failure != null)
                {
                    AddText(L.Format("editor.MUIDashboard.Pages.0d996b74d4", item.DiagnosticId, item.Failure.Message));
                }
            }
        }

        private void ShowPage(ViewInstanceSnapshot item)
        {
            details.Clear();
            AddText($"#{item.Handle.Id} {item.RouteKey}", heading: true);
            AddText(L.Format("editor.MUIDashboard.Pages.d2d0fcf76e", item.State, item.Cleanup, item.Operations, item.ResourceKey, item.ResourceVersion));
            AddText(L.Format("editor.MUIDashboard.Pages.ff516ae1fc", item.Handle, item.CommitVersion, item.HistoryIndex, item.DependencyFailureCount));
            AddText(L.Format("editor.MUIDashboard.Pages.3b0f2c1681", item.PresentationIndex, item.Policy.LayerName ?? L.Get("editor.MUIDashboard.Pages.d7d27662cb"), item.Layer, item.Order) +
                L.Format("editor.MUIDashboard.Pages.e0d917c75f", item.HostVisible, item.HostInteractable, item.Focused));
            AddText(L.Format("scope.summary", L.Value(item.Policy.PageRole), L.Value(item.Policy.OwnerDeparture)));
            AddHandle(L.Get("scope.parent"), item.ParentPage);
            AddHandle(L.Get("editor.MUIDashboard.Pages.81dfa1082d"), item.HiddenBy);
            AddHandle(L.Get("editor.MUIDashboard.Pages.c67c6dbd0d"), item.BlockedBy);
            AddText(L.Format("editor.MUIDashboard.Pages.fcab780068", item.PreparationComplete, item.ActivationCommitted, item.ExecutingCommandCount) +
                L.Format("editor.MUIDashboard.Pages.364909905c", (item.HasReadiness ? L.Value(item.ReadinessStatus) : L.Get("editor.MUIDashboard.Pages.98d5200f51")), item.IsReadinessDegraded, item.HasFailure));
            if (item.RenderOrderStart.HasValue)
            {
                AddText(L.Format("sorting.allocated", item.RenderOrderStart.Value,
                    item.RenderOrderStart.Value + item.RenderOrderSpan - 1, item.RenderOrderStart.Value + 1));
            }
            AddText(L.Format("policy.runtime.status", item.RouteInstanceCount, item.Policy.MaxInstances, item.TickStatus));
            foreach (var dependency in item.DependencyResolutions)
            {
                AddText(L.Format("policy.dependency.entry", dependency.RouteKey, dependency.IsRequired,
                    dependency.MissingPolicy, dependency.Kind, dependency.Failure ?? string.Empty));
            }
            var policy = item.Policy;
            AddText(L.Get("editor.MUIDashboard.Pages.cef9cad2de"), heading: true);
            AddText(L.Format("editor.MUIDashboard.Pages.dac2379812", policy.ConfigurationName ?? L.Get("editor.MUIDashboard.Pages.843ed771bf"), policy.PresetName ?? L.Get("editor.MUIDashboard.Pages.7409a60806")) +
                L.Format("editor.MUIDashboard.Pages.f017a89dcd", policy.Coverage, policy.Modal, policy.TakesFocus, policy.BackBehavior) +
                L.Format("editor.MUIDashboard.Pages.179b91a07b", policy.AllowMultiple, policy.MaxInstances, policy.Overflow, policy.ExistingInstance) +
                L.Format("editor.MUIDashboard.Pages.ba126df79f", policy.CacheMode, policy.TickPause, policy.PrepareTimeout.TotalSeconds, policy.CloseTimeout.TotalSeconds));
            var actions = new VisualElement();
            actions.AddToClassList("mui-actions");
            actions.Add(new Button(() => LocatePage(item.Handle)) { text = L.Get("editor.MUIDashboard.Pages.1a7b45484e") });
            actions.Add(new Button(() => CaptureView(item)) { text = L.Get("editor.MUIDashboard.Pages.a69ef9d4c5") });
            details.Add(actions);
            if (viewDiagnostics != null)
            {
                AddText(viewDiagnostics);
            }
            AddText(L.Get("editor.MUIDashboard.Pages.1d9b547915"), heading: true);
            AddText(L.Format("editor.MUIDashboard.Pages.32fa308cb7", item.HasExplicitOwner, item.OwnerCount, item.Dependencies.Count));
            foreach (var handle in item.Dependencies)
            {
                AddHandle(L.Get("editor.MUIDashboard.Pages.7767404865"), handle);
            }
            foreach (var handle in item.Owners)
            {
                AddHandle(L.Get("editor.MUIDashboard.Pages.3a3912b23e"), handle);
            }
            if (item.OwnersTruncated)
            {
                AddText(L.Get("editor.MUIDashboard.Pages.92d0953bb6"));
            }
            AddText(L.Get("editor.MUIDashboard.Pages.74ae6efb2d"));
        }

        private void LocatePage(ViewHandle handle) => Run(() =>
        {
            if (!TryFindView(handle, out var view))
            {
                throw new InvalidOperationException(L.Get("editor.MUIDashboard.Pages.bba38edb8b"));
            }
            Selection.activeObject = view;
            EditorGUIUtility.PingObject(view.gameObject);
        });

        private bool TryFindView(ViewHandle handle, out View view)
        {
            view = null;
            return UnityEngine.Application.isPlaying && selectedHost != null && selectedHost.TryGetView(handle, out view);
        }

        private void CaptureView(ViewInstanceSnapshot item) => Run(() =>
        {
            if (!TryFindView(item.Handle, out var view))
            {
                throw new InvalidOperationException(L.Get("editor.MUIDashboard.Pages.ec9b640602"));
            }
            var input = view.CaptureInputSnapshot(64);
            var resources = view.CaptureResourcePreparationSnapshot(64);
            var report = new StringBuilder(L.Format("editor.MUIDashboard.Pages.eb0abcb297", DateTime.Now));
            report.AppendLine(L.Format("policy.hidden.status", view.HiddenMode));
            report.AppendLine(L.Format("editor.MUIDashboard.Pages.7cdf69142a", input.ViewAlive, input.ActiveInHierarchy, input.ViewVisible, input.ViewInputEnabled));
            report.AppendLine(L.Format("editor.MUIDashboard.Pages.d9ccaad9e7", input.LocalVisible, input.LocalInteractable, input.Restrictions));
            if (input.InputGate != null)
            {
                report.AppendLine(L.Format("editor.MUIDashboard.Pages.b69ebb0273", input.InputGate.BlockerCount, input.InputGate.IsDisposed));
                foreach (var reason in input.InputGate.Reasons)
                {
                    report.AppendLine(reason);
                }
                if (input.InputGate.IsTruncated)
                {
                    report.AppendLine(L.Get("editor.MUIDashboard.Pages.36ecf025c0"));
                }
            }
            report.AppendLine(L.Format("editor.MUIDashboard.Pages.9436cba475", resources.TotalCount, resources.PendingCount, resources.FailedCount, resources.Committed));
            foreach (var entry in resources.Entries)
            {
                report.AppendLine(entry.Target + " · " + L.Value(entry.State));
            }
            if (resources.IsTruncated)
            {
                report.AppendLine(L.Get("editor.MUIDashboard.Pages.cc6eee6c18"));
            }
            viewDiagnostics = report.ToString();
            ShowPage(item);
        });

        private void AddText(string text, bool heading = false)
        {
            var label = new Label(text);
            label.AddToClassList(heading ? "mui-dashboard-heading" : "mui-dashboard-text");
            details.Add(label);
        }

        private void AddHandle(string label, ViewHandle handle)
        {
            if (!handle.IsValid)
            {
                AddText(label + L.Get("editor.MUIDashboard.Pages.f554f1a3bf"));
                return;
            }
            details.Add(new Button(() =>
            {
                if (snapshot == null || !snapshot.Instances.Any(item => item.Handle == handle))
                {
                    SetMessage(L.Get("editor.MUIDashboard.Pages.dbc43ed047"), HelpBoxMessageType.Info);
                    return;
                }
                selectedHandle = handle;
                viewDiagnostics = null;
                search.SetValueWithoutNotify(string.Empty);
                RebuildPages();
                var index = pages.FindIndex(item => item.Handle == handle);
                pageList.ScrollToItem(index);
            })
            {
                text = $"{label}：#{handle.Id}"
            });
        }
    }
}
