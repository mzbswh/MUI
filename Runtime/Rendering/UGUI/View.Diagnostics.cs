using System;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class View
    {
        /// <summary>在 Unity 主线程读取首帧资源准备账本，不启动加载、初始化或等待任务。</summary>
        public ViewResourcePreparationSnapshot CaptureResourcePreparationSnapshot(int maxEntries = 128)
        {
            UnityMainThread.Require();
            if (maxEntries < 1 || maxEntries > 4096)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEntries));
            }

            if (this == null)
            {
                throw new ObjectDisposedException(nameof(View));
            }

            if (activeResourceContext != null)
            {
                return activeResourceContext.CapturePreparationSnapshot(maxEntries);
            }

            return new ViewResourcePreparationSnapshot(waitForResourceSources, false, false, false, false,
                0, 0, 0, 0, 0, new System.Collections.Generic.List<ResourcePreparationEntry>());
        }

        /// <summary>
        /// 在 Unity 主线程采集自身门控和根 CanvasGroup；不调用 Initialize、ApplyGates 或 InputGate 的惰性创建入口。
        /// 已逻辑释放但 Unity 对象仍存在时可查询；Unity 对象已销毁时拒绝查询。
        /// </summary>
        public ViewInputSnapshot CaptureInputSnapshot(int maxBlockerReasons = 128)
        {
            UnityMainThread.Require();
            if (maxBlockerReasons < 1 || maxBlockerReasons > 4096)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBlockerReasons), "输入原因采集上限必须在 1 至 4096 之间。");
            }
            if (this == null)
            {
                throw new ObjectDisposedException(nameof(View));
            }
            var gateSnapshot = inputGate == null ? null : inputGate.CaptureSnapshot(maxBlockerReasons);
            var actualGroup = group != null ? group : GetComponent<CanvasGroup>();
            return new ViewInputSnapshot(index != null, disposed, IsAlive, gameObject.activeInHierarchy,
                hostVisible, hostInteractable, localVisible, localInteractable, retainingVisuals,
                IsVisible, IsInputEnabled, gateSnapshot, new CanvasGroupSnapshot(actualGroup));
        }
    }
}
