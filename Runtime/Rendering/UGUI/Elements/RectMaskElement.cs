using System;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>适配原生 RectMask2D 裁剪属性，不拥有引擎生成的遮罩材质。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectMask2D))]
    [GenerateControlProperty(nameof(Target), "enabled", "MaskEnabled")]
    [GenerateControlProperty(nameof(Target), "padding", "Padding")]
    [GenerateControlProperty(nameof(Target), "softness", "Softness")]
    public sealed partial class RectMaskElement : Element
    {
        private RectMask2D target;

        private RectMask2D Target
        {
            get
            {
                RequireAlive();
                if (target == null)
                {
                    target = RequireComponent<RectMask2D>();
                }

                return target;
            }
        }

        /// <summary>裁剪边距，顺序为左、下、右、上，允许有限负值。</summary>
        partial void OnPaddingChanging(Vector4 value)
        {
            if (!Finite(value.x) || !Finite(value.y) || !Finite(value.z) || !Finite(value.w))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        /// <summary>水平和垂直柔化像素，必须非负。</summary>
        partial void OnSoftnessChanging(Vector2Int value)
        {
            if (value.x < 0 || value.y < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        protected override void OnInitialize()
        {
            target = RequireComponent<RectMask2D>();
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
