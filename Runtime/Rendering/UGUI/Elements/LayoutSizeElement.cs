using System;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>适配原生 LayoutElement，通过引擎布局流程更新，不强制即时重建。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(UnityEngine.UI.LayoutElement))]
    public sealed class LayoutSizeElement : Element
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

        /// <summary>是否从父布局计算中排除。</summary>
        public bool IgnoreLayout
        {
            get => Target.ignoreLayout;
            set
            {
                var layout = Target;
                if (layout.ignoreLayout.Equals(value))
                {
                    return;
                }

                layout.ignoreLayout = value;
                if (IsAlive && layout != null)
                {
                    NotifyChanged();
                }
            }
        }

        /// <summary>最小宽度；负值沿原生语义表示不提供该约束。</summary>
        public float MinWidth
        {
            get => Target.minWidth;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                var layout = Target;
                if (layout.minWidth.Equals(value))
                {
                    return;
                }

                layout.minWidth = value;
                if (IsAlive && layout != null)
                {
                    NotifyChanged();
                }
            }
        }

        /// <summary>最小高度；负值沿原生语义表示不提供该约束。</summary>
        public float MinHeight
        {
            get => Target.minHeight;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                var layout = Target;
                if (layout.minHeight.Equals(value))
                {
                    return;
                }

                layout.minHeight = value;
                if (IsAlive && layout != null)
                {
                    NotifyChanged();
                }
            }
        }

        /// <summary>首选宽度；负值沿原生语义表示不提供该约束。</summary>
        public float PreferredWidth
        {
            get => Target.preferredWidth;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                var layout = Target;
                if (layout.preferredWidth.Equals(value))
                {
                    return;
                }

                layout.preferredWidth = value;
                if (IsAlive && layout != null)
                {
                    NotifyChanged();
                }
            }
        }

        /// <summary>首选高度；负值沿原生语义表示不提供该约束。</summary>
        public float PreferredHeight
        {
            get => Target.preferredHeight;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                var layout = Target;
                if (layout.preferredHeight.Equals(value))
                {
                    return;
                }

                layout.preferredHeight = value;
                if (IsAlive && layout != null)
                {
                    NotifyChanged();
                }
            }
        }

        /// <summary>额外宽度的分配权重；负值表示不提供该约束。</summary>
        public float FlexibleWidth
        {
            get => Target.flexibleWidth;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                var layout = Target;
                if (layout.flexibleWidth.Equals(value))
                {
                    return;
                }

                layout.flexibleWidth = value;
                if (IsAlive && layout != null)
                {
                    NotifyChanged();
                }
            }
        }

        /// <summary>额外高度的分配权重；负值表示不提供该约束。</summary>
        public float FlexibleHeight
        {
            get => Target.flexibleHeight;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                var layout = Target;
                if (layout.flexibleHeight.Equals(value))
                {
                    return;
                }

                layout.flexibleHeight = value;
                if (IsAlive && layout != null)
                {
                    NotifyChanged();
                }
            }
        }

        /// <summary>多个布局约束提供方之间的优先级。</summary>
        public int LayoutPriority
        {
            get => Target.layoutPriority;
            set
            {
                var layout = Target;
                if (layout.layoutPriority.Equals(value))
                {
                    return;
                }

                layout.layoutPriority = value;
                if (IsAlive && layout != null)
                {
                    NotifyChanged();
                }
            }
        }

        protected override void OnInitialize()
        {
            target = RequireComponent<UnityEngine.UI.LayoutElement>();
        }
    }
}
