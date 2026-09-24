using System;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    [DisallowMultipleComponent, RequireComponent(typeof(Image))]
    public sealed partial class ImageElement : GraphicElement
    {
        private Image target;

        private Image Target
        {
            get
            {
                RequireAlive();
                if (target == null)
                {
                    target = RequireComponent<Image>();
                }

                return target;
            }
        }

        /// <summary>借用的 Sprite，所有权归其资源凭证。</summary>
        public Sprite Sprite
        {
            get => Target.sprite;
            set
            {
                RequireUnmanagedSprite();
                if (Target.sprite == value)
                {
                    return;
                }

                Target.sprite = value;
                NotifyChanged();
            }
        }

        /// <summary>归一化填充值限制在 [0, 1]；可提前设置，只有 Filled 类型实际使用此值。</summary>
        public float FillAmount
        {
            get => Target.fillAmount;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Image fill must be finite.");
                }

                var image = Target;
                var next = Mathf.Clamp01(value);
                if (image.fillAmount.Equals(next))
                {
                    return;
                }

                image.fillAmount = next;
                NotifyChanged();
            }
        }

        protected override UnityEngine.UI.MaskableGraphic GraphicComponent => Target;

        protected override AccessibilityRole DefaultAccessibilityRole => AccessibilityRole.Image;

        protected override void OnInitialize()
        {
            target = RequireComponent<Image>();
            base.OnInitialize();
            OnDispose(() =>
            {
                // 资源由显式 Lifetime 归还；先解除原生控件对资源的借用。
                if (spriteResources != null && target != null)
                {
                    target.sprite = null;
                }
            });
        }
    }
}
