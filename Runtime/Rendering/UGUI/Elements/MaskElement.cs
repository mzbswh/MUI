using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>适配原生 Mask 裁剪属性，不拥有引擎生成的遮罩材质。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Mask))]
    public sealed class MaskElement : Element
    {
        private Mask target;

        private Mask Target
        {
            get
            {
                RequireAlive();
                if (target == null)
                {
                    target = RequireComponent<Mask>();
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

        /// <summary>是否绘制用于裁剪的图形，裁剪本身仍可继续生效。</summary>
        public bool ShowMaskGraphic
        {
            get => Target.showMaskGraphic;
            set
            {
                var mask = Target;
                if (mask.showMaskGraphic.Equals(value))
                {
                    return;
                }

                mask.showMaskGraphic = value;
                if (IsAlive && mask != null)
                {
                    NotifyChanged();
                }
            }
        }

        protected override void OnInitialize()
        {
            target = RequireComponent<Mask>();
        }
    }
}
