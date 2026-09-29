using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace MUI.Navigation
{
    /// <summary>追踪记录类型；生命周期通知与请求边界分别解释。</summary>
    public enum NavigationTraceKind
    {
        Lifecycle,
        OperationStarted,
        OperationFinished,
        PreparationStarted,
        PreparationFinished,
        BatchCloseItem,
        OperationStageStarted,
        OperationStageFinished
    }

    /// <summary>统一候选准备及其中的绑定、Presenter 阶段。</summary>
    public enum NavigationPreparationStage
    {
        RequiredDependencies,
        ModelCreation,
        ResourceCreation,
        ActivationPreparation,
        AttachedDependencies,
        PresenterCreate,
        Binding,
        PresenterOpen,
        PresenterOpenAsync
    }

    /// <summary>提交后的转场与实例清理阶段；与候选准备阶段分别计时。</summary>
    public enum NavigationOperationStage
    {
        EnterTransition,
        ExitTransition,
        InstanceCleanup,
        ViewResourceRelease
    }

    /// <summary>事件入队时的元数据，不保留路由工厂、参数、结果对象或异常引用。</summary>
    public readonly struct NavigationTraceEntry
    {
        internal NavigationTraceEntry(NavigationEvent value) : this()
        {
            Timestamp = Stopwatch.GetTimestamp();
            Sequence = value.Sequence;
            Handle = value.Handle;
            Kind = value.Kind;
            CommitVersion = value.CommitVersion;
            RouteKey = Limit(value.Route.Key);
            Reason = value.Reason;
            Outcome = string.Empty;
            var error = value.DependencyFailure?.Error;
            if (value.CloseOutcome.HasValue)
            {
                var result = value.CloseOutcome.Value;
                Outcome = result.Status + "/" + result.Cleanup;
                error = result.Error;
            }
            else if (value.ArgsUpdateOutcome.HasValue)
            {
                var result = value.ArgsUpdateOutcome.Value;
                Outcome = result.Status + "/" + result.Rejection + "/" + result.Cleanup + "/恢复失败=" + result.RecoveryFailed;
                error = result.Error;
            }
            else if (value.RebindOutcome.HasValue)
            {
                var result = value.RebindOutcome.Value;
                Outcome = result.Status + "/" + result.Rejection + "/" + result.Cleanup + "/恢复失败=" + result.RecoveryFailed;
                error = result.Error;
            }
            ErrorType = error == null ? string.Empty : Limit(error.GetType().FullName);
            Dependency = value.DependencyFailure?.Dependency ?? default;
            DependencyStage = value.DependencyFailure?.Stage;
            DependencyRouteKey = value.DependencyFailure.HasValue ? Limit(value.DependencyFailure.Value.Target.Key) : string.Empty;
        }

        internal NavigationTraceEntry(Guid host, long operationId, string operationName,
                    string routeKey, long startedAt, bool finished, ViewHandle handle, string outcome, Exception error, ViewHandle related = default) : this()
        {
            Timestamp = Stopwatch.GetTimestamp();
            TraceKind = finished ? NavigationTraceKind.OperationFinished : NavigationTraceKind.OperationStarted;
            HostId = host;
            OperationId = operationId;
            OperationName = operationName;
            RouteKey = Limit(routeKey);
            Handle = handle;
            Outcome = outcome;
            RelatedHandle = related;
            ErrorType = error == null ? string.Empty : Limit(error.GetType().FullName);
            DependencyRouteKey = string.Empty;
            ElapsedMilliseconds = finished ? (Timestamp - startedAt) * 1000.0 / Stopwatch.Frequency : (double?)null;
        }

        internal NavigationTraceEntry(Guid host, long operationId, ViewHandle handle, string routeKey,
                    NavigationPreparationStage stage, long startedAt, bool finished, bool completed) : this()
        {
            Timestamp = Stopwatch.GetTimestamp();
            TraceKind = finished ? NavigationTraceKind.PreparationFinished : NavigationTraceKind.PreparationStarted;
            HostId = host;
            OperationId = operationId;
            OperationName = "Prepare";
            PreparationStage = stage;
            Handle = handle;
            RouteKey = Limit(routeKey);
            Outcome = finished ? completed ? "完成" : "未完成（异常、取消或同步能力不足）" : string.Empty;
            ErrorType = string.Empty;
            DependencyRouteKey = string.Empty;
            ElapsedMilliseconds = finished ? (Timestamp - startedAt) * 1000.0 / Stopwatch.Frequency : (double?)null;
        }

        internal NavigationTraceEntry(Guid host, long operationId, string operationName, ViewHandle handle,
            string routeKey, NavigationOperationStage stage, long startedAt, bool finished, bool completed,
            Exception error) : this()
        {
            Timestamp = Stopwatch.GetTimestamp();
            TraceKind = finished ? NavigationTraceKind.OperationStageFinished : NavigationTraceKind.OperationStageStarted;
            HostId = host;
            OperationId = operationId;
            OperationName = operationName;
            OperationStage = stage;
            Handle = handle;
            RouteKey = Limit(routeKey);
            Outcome = finished ? completed ? "完成" : "未完成" : string.Empty;
            ErrorType = error == null ? string.Empty : Limit(error.GetType().FullName);
            DependencyRouteKey = string.Empty;
            ElapsedMilliseconds = finished ? (Timestamp - startedAt) * 1000.0 / Stopwatch.Frequency : (double?)null;
        }

        /// <summary>批次返回时复制的逐项结果，不表示该项实际完成时刻。</summary>
        internal NavigationTraceEntry(Guid host, long operationId, string operationName,
            BatchCloseItem item, int index) : this()
        {
            Timestamp = Stopwatch.GetTimestamp();
            TraceKind = NavigationTraceKind.BatchCloseItem;
            HostId = host;
            OperationId = operationId;
            OperationName = operationName;
            BatchItemIndex = index;
            Handle = item.Handle;
            RouteKey = string.Empty;
            Outcome = item.Outcome.HasValue ? item.Outcome.Value.Status + "/" + item.Outcome.Value.Cleanup : "未请求";
            var error = item.Outcome.HasValue ? item.Outcome.Value.Error : null;
            ErrorType = error == null ? string.Empty : Limit(error.GetType().FullName);
            DependencyRouteKey = string.Empty;
        }

        /// <summary>请求涉及的另一实例；替换完成记录中表示源页面，不触发其清理任务。</summary>
        public ViewHandle RelatedHandle
        {
            get;
        }

        public NavigationTraceKind TraceKind
        {
            get;
        }

        /// <summary>请求编号所属的宿主；生命周期条目通过 Handle.Host 定位。</summary>
        public Guid HostId
        {
            get;
        }

        /// <summary>宿主内递增的请求编号，生命周期条目为 0；不因追踪重启而复用。</summary>
        public long OperationId
        {
            get;
        }

        public string OperationName
        {
            get;
        }

        /// <summary>请求或阶段完成才有值；请求包含排队，阶段包含其内部等待及嵌套准备，不可直接累加。</summary>
        public double? ElapsedMilliseconds
        {
            get;
        }

        public NavigationPreparationStage? PreparationStage
        {
            get;
        }

        public NavigationOperationStage? OperationStage
        {
            get;
        }

        /// <summary>在原批次结果中的零起始序号，仅逐项结果记录有值。</summary>
        public int? BatchItemIndex
        {
            get;
        }

        /// <summary>单调时钟值；换算频率读取快照 TimestampFrequency，不是 UTC 时间。</summary>
        public long Timestamp
        {
            get;
        }

        public long Sequence
        {
            get;
        }

        public ViewHandle Handle
        {
            get;
        }

        /// <summary>仅 TraceKind 为 Lifecycle 时有效。</summary>
        public NavigationEventKind Kind
        {
            get;
        }

        public long CommitVersion
        {
            get;
        }

        /// <summary>名称最多保留 256 字符，超长以省略号标记。</summary>
        public string RouteKey
        {
            get;
        }

        public DismissReason? Reason
        {
            get;
        }

        public string Outcome
        {
            get;
        }

        /// <summary>仅异常类型名，不读取可能包含业务数据的消息、堆栈或自定义属性。</summary>
        public string ErrorType
        {
            get;
        }

        public ViewHandle Dependency
        {
            get;
        }

        public DependencyFailureStage? DependencyStage
        {
            get;
        }

        public string DependencyRouteKey
        {
            get;
        }

        internal static string Limit(string text)
        {
            if (text == null)
            {
                return string.Empty;
            }
            if (text.Length <= 256)
            {
                return text;
            }

            var length = char.IsHighSurrogate(text[254]) ? 254 : 255;
            return text.Substring(0, length) + "…";
        }
    }

    /// <summary>一次手动采集的有界导航时间线；包含生命周期和已接入的请求边界，不等同于各准备阶段耗时。</summary>
    public sealed class NavigationTraceSnapshot
    {
        internal NavigationTraceSnapshot(bool recording, int capacity, long overwritten,
                    long droppedNotifications, NavigationTraceEntry[] entries)
        {
            IsRecording = recording;
            Capacity = capacity;
            OverwrittenCount = overwritten;
            DroppedNotificationCount = droppedNotifications;
            Entries = Array.AsReadOnly(entries);
        }

        public bool IsRecording
        {
            get;
        }

        public int Capacity
        {
            get;
        }

        public long TimestampFrequency => Stopwatch.Frequency;

        /// <summary>本轮记录因环形容量覆盖的旧条目数量。</summary>
        public long OverwrittenCount
        {
            get;
        }

        /// <summary>宿主生命周期通知队列的累计丢弃数，独立于追踪覆盖数。</summary>
        public long DroppedNotificationCount
        {
            get;
        }

        public IReadOnlyList<NavigationTraceEntry> Entries
        {
            get;
        }

        /// <summary>导出纯文本；不读取业务对象，控制字符转换为空格，避免伪造报告行。</summary>
        public string ExportText()
        {
            var text = new StringBuilder();
            text.AppendLine($"导航生命周期追踪：记录中={IsRecording}；容量={Capacity}；旧记录覆盖={OverwrittenCount}；通知丢弃={DroppedNotificationCount}");
            text.AppendLine("时间为相对首条保留事件的毫秒数，不代表操作耗时。路由键与异常类型名最多保留 256 字符。");
            var origin = Entries.Count == 0 ? 0 : Entries[0].Timestamp;
            foreach (var entry in Entries)
            {
                var milliseconds = (entry.Timestamp - origin) * 1000.0 / TimestampFrequency;
                text.Append(milliseconds.ToString("F3", CultureInfo.InvariantCulture)).Append(" ms | #")
                    .Append(entry.Sequence).Append(" | ").Append(entry.TraceKind)
                    .Append("/").Append(entry.TraceKind == NavigationTraceKind.Lifecycle ? entry.Kind.ToString() : entry.OperationName)
                    .Append(" | ").Append(entry.Handle)
                    .Append(" | 版本=").Append(entry.CommitVersion).Append(" | 路由=").Append(Safe(entry.RouteKey))
                    .Append(" | 原因=").Append(entry.Reason).Append(" | 结果=").Append(entry.Outcome)
                    .Append(" | 异常类型=").Append(Safe(entry.ErrorType));
                if (entry.OperationId != 0)
                {
                    text.Append(" | 请求=").Append(entry.HostId.ToString("N")).Append(":").Append(entry.OperationId);
                }
                if (entry.BatchItemIndex.HasValue)
                {
                    text.Append(" | 批次项序号=").Append(entry.BatchItemIndex.Value)
                        .Append("（时间为批次结果采集时刻）");
                }
                if (entry.RelatedHandle.IsValid)
                {
                    text.Append(" | 关联实例=").Append(entry.RelatedHandle);
                }
                if (entry.PreparationStage.HasValue)
                {
                    text.Append(" | 准备阶段=").Append(entry.PreparationStage);
                }
                if (entry.OperationStage.HasValue)
                {
                    text.Append(" | 操作阶段=").Append(entry.OperationStage);
                }
                if (entry.ElapsedMilliseconds.HasValue)
                {
                    text.Append(entry.PreparationStage.HasValue || entry.OperationStage.HasValue
                            ? " | 阶段耗时毫秒=" : " | 请求总耗时毫秒=")
                        .Append(entry.ElapsedMilliseconds.Value.ToString("F3", CultureInfo.InvariantCulture));
                }
                if (entry.DependencyStage.HasValue)
                {
                    text.Append(" | 依赖=").Append(entry.Dependency).Append("/").Append(Safe(entry.DependencyRouteKey))
                        .Append("/").Append(entry.DependencyStage);
                }
                text.AppendLine();
            }
            return text.ToString();
        }

        private static string Safe(string value)
        {
            var text = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                text.Append(char.IsControl(character) ? ' ' : character);
            }
            return text.ToString();
        }
    }
}
