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
            Debug.LogException(error);
        }
    }
}
