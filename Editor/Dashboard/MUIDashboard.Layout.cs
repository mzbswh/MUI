using UnityEngine;
using UnityEngine.UIElements;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public sealed partial class MUIDashboard
    {
        private void UpdateResponsiveLayout(float width)
        {
            var compact = width < 720;
            rootVisualElement.Q<VisualElement>(className: "mui-dashboard").EnableInClassList("mui-dashboard-compact", compact);
            var split = rootVisualElement.Q<TwoPaneSplitView>("pagesPanel");
            var orientation = compact ? TwoPaneSplitViewOrientation.Vertical : TwoPaneSplitViewOrientation.Horizontal;
            if (split.orientation != orientation)
            {
                split.orientation = orientation;
                split.fixedPaneInitialDimension = compact ? 190 : 290;
            }
        }

        private void RefreshEmptyPages()
        {
            var empty = rootVisualElement.Q<VisualElement>("pagesEmpty");
            var hasSnapshot = snapshot != null;
            pageList.style.display = pages.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            empty.style.display = pages.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            rootVisualElement.Q<Label>("pagesEmptyTitle").text = L.Get(!hasSnapshot ? "dashboard.pages.waiting" :
                string.IsNullOrEmpty(search.value) ? "dashboard.pages.empty" : "dashboard.pages.noMatches");
            rootVisualElement.Q<Label>("pagesEmptyHint").text = L.Get(!hasSnapshot ? "dashboard.pages.waiting.help" :
                string.IsNullOrEmpty(search.value) ? "dashboard.pages.empty.help" : "dashboard.pages.noMatches.help");
        }

        private void ShowNoSnapshot()
        {
            var empty = new VisualElement();
            empty.AddToClassList("mui-empty");
            empty.Add(MUIEditorControls.Text(selectedHost == null ? "dashboard.noHost" :
                !Application.isPlaying ? "dashboard.editMode" : !selectedHost.IsInitialized ? "dashboard.notInitialized" :
                "dashboard.capture.ready", "mui-empty-title"));
            empty.Add(MUIEditorControls.Text(selectedHost == null ? "dashboard.noHost.help" :
                !Application.isPlaying ? "dashboard.editMode.help" : !selectedHost.IsInitialized ? "dashboard.notInitialized.help" :
                "dashboard.capture.help"));
            var actions = new VisualElement();
            actions.AddToClassList("mui-actions");
            var configuration = new Button(() => SelectTab("settings")) { text = L.Get("dashboard.openConfiguration") };
            actions.Add(configuration);
            empty.Add(actions);
            details.Add(empty);
        }

        private string SnapshotSummary() => L.Format("editor.MUIDashboard.6015cd5b5f", capturedAt, snapshot.IsShutdown, snapshot.CommitVersion) +
            L.Format("editor.MUIDashboard.ced796a048", snapshot.Instances.Count, snapshot.TotalInstances, snapshot.RecentHistory.Count, snapshot.HistoryCount, snapshot.Focused.Id) +
            L.Format("editor.MUIDashboard.fbfc4d6291", snapshot.PendingRequestCount, snapshot.PostedRequestCount, snapshot.CachedViewCount, snapshot.RetiringCachedViewCount, snapshot.PreloadReservationCount) +
            L.Format("editor.MUIDashboard.f0f6586364", snapshot.PendingCleanupCount, snapshot.UnconfirmedCleanupCount, snapshot.HasCleanupFailure);
    }
}
