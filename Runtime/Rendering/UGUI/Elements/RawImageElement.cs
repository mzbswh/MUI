using System;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>
    /// 将原生 RawImage 的纹理、颜色和采样区域适配为可绑定属性。
    /// 纹理为借用引用，其创建、更新与释放仍由资源所有者负责。
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(UnityEngine.UI.RawImage))]
    public sealed partial class RawImageElement : GraphicElement
    {
        private UnityEngine.UI.RawImage target;

        private UnityEngine.UI.RawImage Target
        {
            get
            {
                RequireAlive();
                if (target == null)
                {
                    target = RequireComponent<UnityEngine.UI.RawImage>();
                }

                return target;
            }
        }

        /// <summary>借用 Texture 或 RenderTexture；赋值不转移资源所有权。</summary>
        public Texture Texture
        {
            get => Target.texture;
            set
            {
                var image = Target;
                if (!ReferenceEquals(value, null) && value == null)
                {
                    throw new ArgumentException("不能使用已销毁的纹理。", nameof(value));
                }

                if (textureResources != null)
                {
                    textureResources.SetBorrowed(value);
                    return;
                }

                if (image.texture == value)
                {
                    return;
                }

                image.texture = value;
                NotifyChanged();
            }
        }

        /// <summary>纹理采样区域；允许越界平铺和负宽高翻转，但拒绝非有限数值。</summary>
        public Rect UVRect
        {
            get => Target.uvRect;
            set
            {
                var image = Target;
                if (!IsFinite(value.x) || !IsFinite(value.y) ||
                    !IsFinite(value.width) || !IsFinite(value.height))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "纹理采样区域必须使用有限数值。");
                }

                if (image.uvRect.Equals(value))
                {
                    return;
                }

                image.uvRect = value;
                NotifyChanged();
            }
        }

        protected override UnityEngine.UI.MaskableGraphic GraphicComponent => Target;

        protected override AccessibilityRole DefaultAccessibilityRole => AccessibilityRole.Image;

        protected override void OnInitialize()
        {
            target = RequireComponent<UnityEngine.UI.RawImage>();
            base.OnInitialize();
            OnDispose(() =>
            {
                // 仅解除借用；不能销毁项目共享纹理或归还他人创建的 RenderTexture。
                if (target != null)
                {
                    target.texture = null;
                }
            });
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
