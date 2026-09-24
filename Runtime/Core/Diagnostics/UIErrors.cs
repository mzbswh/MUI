using System;

namespace MUI
{
    /// <summary>隔离的诊断输出入口，宿主可接入日志或遥测适配器。</summary>
    public static class UIErrors
    {
        public static event Action<Exception> Reported;

        public static void Report(Exception error)
        {
            if (error == null)
            {
                return;
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
                { /* 诊断不能中断生命周期清理。 */
                }
            }
        }

        public static void Reset() => Reported = null;
    }
}
