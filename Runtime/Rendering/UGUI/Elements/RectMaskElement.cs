using System;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>适配原生 RectMask2D 裁剪属性，不拥有引擎生成的遮罩材质。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectMask2D))]
    public sealed class RectMaskElement : Element
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

        /// <summary>是否启用裁剪；关闭不会隐藏 GameObject，只会停止遮罩效果。</summary>
        public bool MaskEnabled
        {
            get => Target.enabled;
            set
            {
                var mask = Target;
                if (mask.enabled.Equals(value))
                {
                    return;
                }

                mask.enabled = value;
                if (IsAlive && mask != null)
                {
                    NotifyChanged();
                }
            }
        }

        /// <summary>裁剪边距，顺序为左、下、右、上，允许有限负值。</summary>
        public Vector4 Padding
        {
            get => Target.padding;
            set
            {
                if (!Finite(value.x) || !Finite(value.y) || !Finite(value.z) || !Finite(value.w))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                var mask = Target;
                if (mask.padding.Equals(value))
                {
                    return;
                }

                mask.padding = value;
                if (IsAlive && mask != null)
                {
                    NotifyChanged();
                }
            }
        }

        /// <summary>水平和垂直柔化像素，必须非负。</summary>
        public Vector2Int Softness
        {
            get => Target.softness;
            set
            {
                if (value.x < 0 || value.y < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                var mask = Target;
                if (mask.softness.Equals(value))
                {
                    return;
                }

                mask.softness = value;
                if (IsAlive && mask != null)
                {
                    NotifyChanged();
                }
            }
        }

        protected override void OnInitialize()
        {
            target = RequireComponent<RectMask2D>();
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
