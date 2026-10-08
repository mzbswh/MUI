using System;

namespace MUI.UGUI
{
    /// <summary>View 门控及根 CanvasGroup 中观察到的输入限制，不代表完整射线命中结果。</summary>
    [Flags]
    public enum ViewInputRestriction
    {
        None = 0,
        Disposed = 1,
        NotInitialized = 2,
        InactiveHierarchy = 4,
        HiddenByHost = 8,
        HiddenLocally = 16,
        HostInputDisabled = 32,
        LocalInputDisabled = 64,
        LocalGateBlocked = 128,
        LocalGateDisposed = 256,
        RetainingVisuals = 512,
        MissingCanvasGroup = 1024,
        CanvasGroupInputDisabled = 2048,
        CanvasGroupRaycastsDisabled = 4096,
        ViewUnavailable = 8192
    }

    /// <summary>
    /// View 门控与根 CanvasGroup 状态的只读快照，不初始化 View，不持有 Unity 对象。
    /// 不包含祖先组、Graphic 射线过滤、Selectable、EventSystem 或其他遮挡物的最终命中判定。
    /// </summary>
    public sealed class ViewInputSnapshot
    {
        internal ViewInputSnapshot(bool initialized, bool disposed, bool alive, bool activeInHierarchy,
                    bool hostVisible, bool hostInteractable, bool localVisible, bool localInteractable,
                    bool retainingVisuals, bool viewVisible, bool viewInputEnabled,
                    InputGateSnapshot inputGate, CanvasGroupSnapshot canvasGroup)
        {
            Initialized = initialized;
            Disposed = disposed;
            ViewAlive = alive;
            ActiveInHierarchy = activeInHierarchy;
            HostVisible = hostVisible;
            HostInteractable = hostInteractable;
            LocalVisible = localVisible;
            LocalInteractable = localInteractable;
            RetainingVisuals = retainingVisuals;
            ViewVisible = viewVisible;
            ViewInputEnabled = viewInputEnabled;
            InputGate = inputGate;
            CanvasGroup = canvasGroup;
            var restrictions = ViewInputRestriction.None;
            if (!alive)
            {
                restrictions |= ViewInputRestriction.ViewUnavailable;
            }
            if (disposed)
            {
                restrictions |= ViewInputRestriction.Disposed;
            }
            if (!initialized)
            {
                restrictions |= ViewInputRestriction.NotInitialized;
            }
            if (!activeInHierarchy)
            {
                restrictions |= ViewInputRestriction.InactiveHierarchy;
            }
            if (!hostVisible)
            {
                restrictions |= ViewInputRestriction.HiddenByHost;
            }
            if (!localVisible)
            {
                restrictions |= ViewInputRestriction.HiddenLocally;
            }
            if (!hostInteractable)
            {
                restrictions |= ViewInputRestriction.HostInputDisabled;
            }
            if (!localInteractable)
            {
                restrictions |= ViewInputRestriction.LocalInputDisabled;
            }
            if (retainingVisuals)
            {
                restrictions |= ViewInputRestriction.RetainingVisuals;
            }
            if (inputGate != null && inputGate.IsDisposed)
            {
                restrictions |= ViewInputRestriction.LocalGateDisposed;
            }
            if (inputGate != null && inputGate.BlockerCount != 0)
            {
                restrictions |= ViewInputRestriction.LocalGateBlocked;
            }
            if (!canvasGroup.Exists)
            {
                restrictions |= ViewInputRestriction.MissingCanvasGroup;
            }
            else if (canvasGroup.Enabled)
            {
                // 被禁用的 CanvasGroup 不以这些值阻挡射线或 Selectable；保留属性供排查。
                if (!canvasGroup.Interactable)
                {
                    restrictions |= ViewInputRestriction.CanvasGroupInputDisabled;
                }
                if (!canvasGroup.BlocksRaycasts)
                {
                    restrictions |= ViewInputRestriction.CanvasGroupRaycastsDisabled;
                }
            }
            Restrictions = restrictions;
        }

        public bool Initialized
        {
            get;
        }

        public bool Disposed
        {
            get;
        }

        public bool ViewAlive
        {
            get;
        }

        public bool ActiveInHierarchy
        {
            get;
        }

        public bool HostVisible
        {
            get;
        }

        public bool HostInteractable
        {
            get;
        }

        public bool LocalVisible
        {
            get;
        }

        public bool LocalInteractable
        {
            get;
        }

        public bool RetainingVisuals
        {
            get;
        }

        /// <summary>View 自身可见性计算结果，不等于像素确实可见。</summary>
        public bool ViewVisible
        {
            get;
        }

        /// <summary>View 自身输入资格，不等于特定控件能够收到点击。</summary>
        public bool ViewInputEnabled
        {
            get;
        }

        /// <summary>未创建门控时为 null；诊断不会因读取而创建门控。</summary>
        public InputGateSnapshot InputGate
        {
            get;
        }

        public CanvasGroupSnapshot CanvasGroup
        {
            get;
        }

        /// <summary>各层观察到的限制条件，可同时存在；部分条件可能被画面保留等状态覆盖。</summary>
        public ViewInputRestriction Restrictions
        {
            get;
        }
    }
}
