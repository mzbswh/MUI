using System;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>适配原生 LayoutElement，通过引擎布局流程更新，不强制即时重建。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(UnityEngine.UI.LayoutElement))]
    [GenerateControlProperty(nameof(Target), "ignoreLayout", "IgnoreLayout")]
    [GenerateControlProperty(nameof(Target), "minWidth", "MinWidth")]
    [GenerateControlProperty(nameof(Target), "minHeight", "MinHeight")]
    [GenerateControlProperty(nameof(Target), "preferredWidth", "PreferredWidth")]
    [GenerateControlProperty(nameof(Target), "preferredHeight", "PreferredHeight")]
    [GenerateControlProperty(nameof(Target), "flexibleWidth", "FlexibleWidth")]
    [GenerateControlProperty(nameof(Target), "flexibleHeight", "FlexibleHeight")]
    [GenerateControlProperty(nameof(Target), "layoutPriority", "LayoutPriority")]
    public sealed partial class LayoutSizeElement : Element
    {
        private UnityEngine.UI.LayoutElement target;

        private UnityEngine.UI.LayoutElement Target
        {
            get
            {
                RequireAlive();
                if (target == null)
                {
                    target = RequireComponent<UnityEngine.UI.LayoutElement>();
                }

                return target;
            }
        }

        /// <summary>最小宽度；负值沿原生语义表示不提供该约束。</summary>
        partial void OnMinWidthChanging(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        /// <summary>最小高度；负值沿原生语义表示不提供该约束。</summary>
        partial void OnMinHeightChanging(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        /// <summary>首选宽度；负值沿原生语义表示不提供该约束。</summary>
        partial void OnPreferredWidthChanging(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        /// <summary>首选高度；负值沿原生语义表示不提供该约束。</summary>
        partial void OnPreferredHeightChanging(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        /// <summary>额外宽度的分配权重；负值表示不提供该约束。</summary>
        partial void OnFlexibleWidthChanging(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        /// <summary>额外高度的分配权重；负值表示不提供该约束。</summary>
        partial void OnFlexibleHeightChanging(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        protected override void OnInitialize()
        {
            target = RequireComponent<UnityEngine.UI.LayoutElement>();
        }
    }
}
