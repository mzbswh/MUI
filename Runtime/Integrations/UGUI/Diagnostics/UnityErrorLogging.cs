using System;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>仅在没有项目错误出口时把运行时诊断转发到 Unity Console。</summary>
    internal static class UnityErrorLogging
    {
        private static int hostCount;

        public static void RegisterHost()
        {
            if (++hostCount == 1)
            {
                UIErrors.FallbackSink = ForwardToUnity;
            }
        }

        public static void UnregisterHost()
        {
            if (hostCount > 0 && --hostCount == 0)
            {
                if (UIErrors.FallbackSink == ForwardToUnity)
                {
                    UIErrors.FallbackSink = null;
                }
            }
        }

        public static void Report(Exception error)
        {
            if (error == null)
            {
                return;
            }

            UIErrors.Report(error, ForwardToUnity);
        }

        private static void ForwardToUnity(Exception error)
        {
            var id = UIErrors.GetDiagnosticId(error);
            var context = UIErrors.GetDiagnosticContext(error);
            // 一个出口记录同时携带标识、位置和原始异常栈，不再额外打印第二条上下文日志。
            Debug.LogError($"MUI error [{id:N}]; {context}\n{error}");
        }
    }
}
