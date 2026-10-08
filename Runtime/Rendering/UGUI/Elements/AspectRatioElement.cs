using System;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>适配原生 AspectRatioFitter，通过引擎布局流程更新，不强制即时重建。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(UnityEngine.UI.AspectRatioFitter))]
    public sealed class AspectRatioElement : Element
    {
        private UnityEngine.UI.AspectRatioFitter target;

        private UnityEngine.UI.AspectRatioFitter Target
        {
            get
            {
                RequireAlive();
                if (target == null)
                {
                    target = RequireComponent<UnityEngine.UI.AspectRatioFitter>();
                }

                return target;
            }
        }

        /// <summary>原生宽高比适配模式。</summary>
        public UnityEngine.UI.AspectRatioFitter.AspectMode Mode
        {
            get => Target.aspectMode;
            set
            {
                if (!Enum.IsDefined(typeof(UnityEngine.UI.AspectRatioFitter.AspectMode), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                var layout = Target;
                if (layout.aspectMode.Equals(value))
                {
                    return;
                }

                layout.aspectMode = value;
                if (IsAlive && layout != null)
                {
                    NotifyChanged();
                }
            }
        }

        /// <summary>目标宽高比，必须为大于零的有限值。</summary>
        public float AspectRatio
        {
            get => Target.aspectRatio;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                var layout = Target;
                if (layout.aspectRatio.Equals(value))
                {
                    return;
                }

                layout.aspectRatio = value;
                if (IsAlive && layout != null)
                {
                    NotifyChanged();
                }
            }
        }

        protected override void OnInitialize()
        {
            target = RequireComponent<UnityEngine.UI.AspectRatioFitter>();
        }
    }
}
