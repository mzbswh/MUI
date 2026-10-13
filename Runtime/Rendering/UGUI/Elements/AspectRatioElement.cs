using System;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>适配原生 AspectRatioFitter，通过引擎布局流程更新，不强制即时重建。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(UnityEngine.UI.AspectRatioFitter))]
    [GenerateControlProperty(nameof(Target), "aspectMode", "Mode")]
    [GenerateControlProperty(nameof(Target), "aspectRatio", "AspectRatio")]
    public sealed partial class AspectRatioElement : Element
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
        partial void OnModeChanging(UnityEngine.UI.AspectRatioFitter.AspectMode value)
        {
            if (!Enum.IsDefined(typeof(UnityEngine.UI.AspectRatioFitter.AspectMode), value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        /// <summary>目标宽高比，必须为大于零的有限值。</summary>
        partial void OnAspectRatioChanging(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        protected override void OnInitialize()
        {
            target = RequireComponent<UnityEngine.UI.AspectRatioFitter>();
        }
    }
}
