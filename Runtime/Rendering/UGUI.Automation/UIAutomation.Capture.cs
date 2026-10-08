using System;
using MUI.Navigation;
using UnityEngine;
using UnityEngine.Rendering;

namespace MUI.UGUI
{
    public sealed partial class UIAutomation
    {
        /// <summary>
        /// 在当前帧渲染结束后同步捕获整个 Game View，并附带采集前的界面输入状态。
        /// 必须由项目保证调用时机，不可直接在 Update/渲染中调用；不调度协程或任务。
        /// 不是指定 View 的裁剪图，也不自动隐藏其他界面；调用方拥有快照并负责 Dispose。
        /// </summary>
        public UIAutomationSnapshot CaptureSnapshot(int maxPixels = 16777216, int maxBlockerReasons = 128)
        {
            RequireThread();
            if (dispatching || evaluatingCondition)
            {
                throw new InvalidOperationException("不能在自动化派发或条件检查中捕获截图。");
            }
            if (maxPixels < 1 || maxPixels > 16777216)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPixels), "截图像素上限必须在 1 至 16777216 之间。");
            }
            if (!Application.isPlaying || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                throw new InvalidOperationException("截图要求正在运行且具有图形设备的游戏视图。");
            }
            CheckSnapshotSize(Screen.width, Screen.height, maxPixels);
            var input = QueryState(maxBlockerReasons);
            var frame = Time.frameCount;
            var capturedAt = DateTimeOffset.UtcNow;
            var texture = ScreenCapture.CaptureScreenshotAsTexture(1);
            if (texture == null)
            {
                throw new InvalidOperationException("Unity 未返回截图纹理，请确认渲染时机和平台截图支持。");
            }
            try
            {
                // 分辨率可能在原生采集时改变；复核结果，拒绝保留超出预算的纹理。
                CheckSnapshotSize(texture.width, texture.height, maxPixels);
                var snapshot = new UIAutomationSnapshot(texture, input, frame, capturedAt);
                texture = null;
                return snapshot;
            }
            finally
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }
        }

        /// <summary>导出调用方明确指定的导航器当前追踪快照，不启动记录，不输出业务参数。</summary>
        public string ExportTrace(INavigator navigator)
        {
            RequireThread();
            if (navigator == null)
            {
                throw new ArgumentNullException(nameof(navigator));
            }
            return navigator.CaptureLifecycleTrace().ExportText();
        }

        private static void CheckSnapshotSize(int width, int height, int maxPixels)
        {
            if (width < 1 || height < 1 || (long)width * height > maxPixels)
            {
                throw new InvalidOperationException("截图分辨率无效或超过允许的像素上限。");
            }
        }
    }
}
