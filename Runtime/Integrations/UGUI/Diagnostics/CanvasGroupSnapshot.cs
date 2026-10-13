using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>根 CanvasGroup 的实际属性副本，不持有 Unity 对象，也不推断最终射线命中。</summary>
    public readonly struct CanvasGroupSnapshot
    {
        internal CanvasGroupSnapshot(CanvasGroup group)
        {
            Exists = group != null;
            Enabled = Exists && group.enabled;
            Alpha = Exists ? group.alpha : 0;
            Interactable = Exists && group.interactable;
            BlocksRaycasts = Exists && group.blocksRaycasts;
            IgnoreParentGroups = Exists && group.ignoreParentGroups;
        }

        public bool Exists
        {
            get;
        }

        public bool Enabled
        {
            get;
        }

        public float Alpha
        {
            get;
        }

        public bool Interactable
        {
            get;
        }

        public bool BlocksRaycasts
        {
            get;
        }

        public bool IgnoreParentGroups
        {
            get;
        }
    }
}
