using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace MUI
{
    /// <summary>隔离的诊断输出入口，宿主可接入日志或遥测适配器。</summary>
    public static partial class UIErrors
    {
        // 弱键不会让诊断入口长期持有异常及其业务对象；记录本身只包含值类型。
        private static readonly ConditionalWeakTable<Exception, ErrorRecord> records =
            new ConditionalWeakTable<Exception, ErrorRecord>();
        private static Action<Exception> sink;
        private static Action<Exception> fallbackSink;
        [ThreadStatic] private static bool reporting;

        public static event Action<Exception> Reported;

        /// <summary>项目级错误出口；设置后由项目日志系统接管默认的 Unity Console 输出。</summary>
        public static Action<Exception> Sink
        {
            get => Volatile.Read(ref sink);
            set => Interlocked.Exchange(ref sink, value);
        }

        internal static Action<Exception> FallbackSink
        {
            get => Volatile.Read(ref fallbackSink);
            set => Interlocked.Exchange(ref fallbackSink, value);
        }

        /// <summary>
        /// 返回本次异常的稳定标识；仅查询不触发报告，null 对应 Guid.Empty。
        /// 传播同一异常实例时复用标识；重试产生的新异常具有独立标识。
        /// </summary>
        public static Guid GetDiagnosticId(Exception error)
        {
            // 单项聚合只为传播附加上下文，不构成一次新的异常发生。
            while (error is AggregateException aggregate && aggregate.InnerExceptions.Count == 1)
            {
                error = aggregate.InnerExceptions[0];
            }

            return error == null ? Guid.Empty : records.GetValue(error, CreateRecord).Id;
        }

        private static ErrorRecord CreateRecord(Exception error) => new ErrorRecord();

        /// <summary>同一异常实例只报告一次，聚合异常逐项报告其原因；隔离出口和观察者异常，阻止同线程递归报告。</summary>
        public static void Report(Exception error) => Report(error, null);

        internal static void Report(Exception error, Action<Exception> localFallback)
        {
            if (error == null || reporting)
            {
                return;
            }

            AttachContext(error, CurrentContext);

            if (error is AggregateException aggregate && aggregate.InnerExceptions.Count != 0)
            {
                // 聚合保存完整清理结果，出口逐项报告实际失败，避免重复打印已报告的原因。
                foreach (var inner in aggregate.InnerExceptions)
                {
                    Report(inner, localFallback);
                }
                return;
            }

            var record = records.GetValue(error, CreateRecord);
            if (Interlocked.Exchange(ref record.Reported, 1) != 0)
            {
                return;
            }

            reporting = true;
            try
            {
                Dispatch(error, localFallback);
            }
            finally
            {
                reporting = false;
            }
        }

        private static void Dispatch(Exception error, Action<Exception> localFallback)
        {
            var configured = Sink;
            var fallback = FallbackSink ?? localFallback;
            var target = configured ?? fallback;
            if (target != null)
            {
                try
                {
                    target(error);
                }
                catch (Exception outputError)
                {
                    // 项目日志故障时保留原始错误，也不能阻断其他诊断观察者。
                    if (configured != null && fallback != null && !ReferenceEquals(configured, fallback))
                    {
                        try
                        {
                            fallback(new AggregateException("UI error sink failed.", error, outputError));
                        }
                        catch (Exception)
                        {
                            // 默认出口故障也不能阻断 UI 清理。
                        }
                    }
                }
            }

            var handlers = Reported;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<Exception> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(error);
                }
                catch (Exception)
                {
                    // 诊断不能中断生命周期清理。
                }
            }
        }

        public static void Reset()
        {
            Sink = null;
            Reported = null;
            // 平台默认出口由活动宿主持有，不能被项目观察者重置顺带移除。
        }

        private sealed class ErrorRecord
        {
            internal readonly Guid Id = Guid.NewGuid();
            internal int Reported;
            internal UIErrorContext Context;
        }
    }
}
