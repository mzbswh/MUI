using System;
using System.Threading;

namespace MUI
{
    /// <summary>隔离的诊断输出入口，宿主可接入日志或遥测适配器。</summary>
    public static class UIErrors
    {
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

        /// <summary>隔离出口和观察者异常；同一线程的报告回调中再次报告会被忽略，避免递归。</summary>
        public static void Report(Exception error) => Report(error, null);

        internal static void Report(Exception error, Action<Exception> localFallback)
        {
            if (error == null || reporting)
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
    }
}
