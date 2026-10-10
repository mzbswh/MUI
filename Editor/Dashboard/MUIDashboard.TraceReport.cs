using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MUI.Navigation;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public sealed partial class MUIDashboard
    {
        private string ExportTrace()
        {
            var report = new StringBuilder();
            report.AppendLine(L.Format("trace.header", trace.IsRecording, trace.Capacity,
                trace.OverwrittenCount, trace.DroppedNotificationCount));
            report.AppendLine(L.Get("trace.hint"));
            var origin = trace.Entries.Count == 0 ? 0 : trace.Entries[0].Timestamp;
            foreach (var entry in trace.Entries)
            {
                var milliseconds = (entry.Timestamp - origin) * 1000.0 / trace.TimestampFrequency;
                report.Append(milliseconds.ToString("F3", CultureInfo.InvariantCulture)).Append(" ms | #")
                    .Append(entry.Sequence).Append(" | ").Append(L.Value(entry.TraceKind))
                    .Append('/').Append(entry.TraceKind == NavigationTraceKind.Lifecycle ? L.Value(entry.Kind) : entry.OperationName)
                    .Append(" | ").Append(entry.Handle);
                AppendTraceField(report, "trace.version", entry.CommitVersion);
                AppendTraceField(report, "trace.route", SafeReportText(entry.RouteKey));
                AppendTraceField(report, "trace.reason", entry.Reason);
                AppendTraceField(report, "trace.outcome", Regex.Replace(entry.Outcome ?? string.Empty, @"[^/;=]+",
                    match => L.Get("trace.token." + match.Value, match.Value)));
                AppendTraceField(report, "trace.diagnosticId", entry.DiagnosticId);
                AppendTraceField(report, "trace.errorType", SafeReportText(entry.ErrorType));
                if (entry.OperationId != 0)
                {
                    AppendTraceField(report, "trace.request", entry.HostId.ToString("N") + ":" + entry.OperationId);
                }
                if (entry.BatchItemIndex.HasValue)
                {
                    AppendTraceField(report, "trace.batchIndex", entry.BatchItemIndex.Value);
                    report.Append(L.Get("trace.batchHint"));
                }
                if (entry.RelatedHandle.IsValid)
                {
                    AppendTraceField(report, "trace.relatedHandle", entry.RelatedHandle);
                }
                if (entry.PreparationStage.HasValue)
                {
                    AppendTraceField(report, "trace.preparationStage", entry.PreparationStage.Value);
                }
                if (entry.OperationStage.HasValue)
                {
                    AppendTraceField(report, "trace.operationStage", entry.OperationStage.Value);
                }
                if (entry.ElapsedMilliseconds.HasValue)
                {
                    AppendTraceField(report, entry.PreparationStage.HasValue || entry.OperationStage.HasValue
                        ? "trace.stageElapsed" : "trace.requestElapsed", entry.ElapsedMilliseconds.Value.ToString("F3", CultureInfo.InvariantCulture));
                }
                if (entry.DependencyStage.HasValue)
                {
                    AppendTraceField(report, "trace.dependency", entry.Dependency + "/" + SafeReportText(entry.DependencyRouteKey) +
                        "/" + L.Value(entry.DependencyStage.Value));
                }
                report.AppendLine();
            }
            return report.ToString();
        }

        private static void AppendTraceField(StringBuilder report, string key, object value)
            => report.Append(" | ").Append(L.Get(key)).Append('=').Append(L.Value(value));
    }
}
