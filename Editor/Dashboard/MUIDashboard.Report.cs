using System.Linq;
using System.Text;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public sealed partial class MUIDashboard
    {
        private string ExportSnapshot()
        {
            var report = new StringBuilder(SnapshotSummary());
            report.AppendLine().AppendLine(L.Get("editor.MUIDashboard.Report.a800e96c6e") + snapshot.HostId.ToString("N"));
            report.AppendLine(L.Get("editor.MUIDashboard.Report.8d5d681c62") + string.Join(", ", snapshot.RecentHistory.Select(handle => "#" + handle.Id)));
            report.AppendLine().AppendLine(L.Get("editor.MUIDashboard.Report.d5bb561464"));
            foreach (var item in snapshot.Instances.OrderByDescending(item => item.PresentationIndex).ThenByDescending(item => item.Handle.Id))
            {
                report.AppendLine(L.Format("editor.MUIDashboard.Report.090aca7127", item.Handle.Id, SafeReportText(item.RouteKey), item.State, item.PresentationIndex, item.Layer));
                report.AppendLine(L.Format("editor.MUIDashboard.Report.2aadcc0c7c", item.HostVisible, item.HostInteractable, item.Focused, item.HiddenBy.Id, item.BlockedBy.Id));
                report.AppendLine(L.Format("policy.runtime.status", item.RouteInstanceCount, item.Policy.MaxInstances, item.TickStatus));
                foreach (var dependency in item.DependencyResolutions)
                {
                    report.AppendLine(L.Format("policy.dependency.entry", SafeReportText(dependency.RouteKey), dependency.IsRequired,
                        dependency.MissingPolicy, dependency.Kind, SafeReportText(dependency.Failure)));
                }
                if (item.RenderOrderStart.HasValue)
                {
                    report.AppendLine(L.Format("sorting.allocated", item.RenderOrderStart.Value,
                        item.RenderOrderStart.Value + item.RenderOrderSpan - 1, item.RenderOrderStart.Value + 1));
                }
                report.AppendLine(L.Format("scope.summary", L.Value(item.Policy.PageRole), L.Value(item.Policy.OwnerDeparture)));
                report.AppendLine(L.Get("scope.parent") + ": " + item.ParentPage);
                var policy = item.Policy;
                report.AppendLine(L.Format("editor.MUIDashboard.Report.516bc8adb6", SafeReportText(policy.ConfigurationName), SafeReportText(policy.PresetName), SafeReportText(policy.LayerName), policy.Coverage, policy.Modal));
            }
            report.AppendLine(L.Format("editor.MUIDashboard.Report.dc709181e1", snapshot.InstancesTruncated, snapshot.HistoryTruncated, snapshot.CleanupResponsibilitiesTruncated));
            foreach (var item in snapshot.CleanupResponsibilities)
            {
                report.AppendLine(L.Format("editor.MUIDashboard.Report.ae0eb0ae7a", item.Id, SafeReportText(item.Owner), item.State, item.DiagnosticId));
            }
            report.AppendLine(L.Get("policy.cache.history"));
            foreach (var decision in snapshot.CacheDiagnostics)
            {
                report.AppendLine(L.Format("policy.cache.entry", decision.TimestampUtc.ToLocalTime(), SafeReportText(decision.RouteKey), decision.Decision));
            }
            return report.ToString();
        }

        private static string SafeReportText(string value)
        {
            if (value == null)
            {
                return L.Get("editor.MUIDashboard.Report.7409a60806");
            }
            var text = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                text.Append(char.IsControl(character) ? ' ' : character);
            }
            return text.ToString();
        }
    }
}
