using System;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>图形控件共用的颜色、材质、射线和遮罩适配，不负责具体图像内容。</summary>
    public abstract partial class GraphicElement : Element
    {
        private UnityEngine.UI.MaskableGraphic graphic;

        /// <summary>派生控件提供自己适配的原生图形；不创建或克隆材质。</summary>
        protected abstract UnityEngine.UI.MaskableGraphic GraphicComponent
        {
            get;
        }

        private UnityEngine.UI.MaskableGraphic Graphic
        {
            get
            {
                RequireAlive();
                if (graphic == null)
                {
                    graphic = GraphicComponent;
                }

                if (graphic == null)
                {
                    throw new MissingComponentException("图形控件缺少有效的 MaskableGraphic。");
                }

                return graphic;
            }
        }

        /// <summary>原生图形的颜色与透明度。</summary>
        public Color Color
        {
            get => Graphic.color;
            set
            {
                var target = Graphic;
                if (target.color == value)
                {
                    return;
                }

                target.color = value;
                NotifyChanged();
            }
        }

        /// <summary>借用材质；null 恢复原生默认材质，不转移持有权或修改材质内容。</summary>
        public Material Material
        {
            get => ReadMaterial(Graphic);
            set
            {
                var target = Graphic;
                if (materialResources != null)
                {
                    materialResources.SetBorrowed(value);
                    return;
                }

                if (!ReferenceEquals(value, null) && value == null)
                {
                    throw new ArgumentException("不能使用已销毁的材质。", nameof(value));
                }

                if (ReadMaterial(target) == value)
                {
                    return;
                }

                WriteMaterial(target, value);
                NotifyChanged();
            }
        }

        /// <summary>是否参与 uGUI 射线检测。</summary>
        public bool RaycastTarget
        {
            get => Graphic.raycastTarget;
            set
            {
                var target = Graphic;
                if (target.raycastTarget == value)
                {
                    return;
                }

                target.raycastTarget = value;
                NotifyChanged();
            }
        }

        /// <summary>是否受父级遮罩影响。</summary>
        public bool Maskable
        {
            get => Graphic.maskable;
            set
            {
                var target = Graphic;
                if (target.maskable == value)
                {
                    return;
                }

                target.maskable = value;
                NotifyChanged();
            }
        }

        /// <summary>读取实际渲染使用的共享材质；特殊图形后端可以改用自己的材质入口。</summary>
        protected virtual Material ReadMaterial(UnityEngine.UI.MaskableGraphic target) => target.material;

        /// <summary>替换借用材质；null 恢复后端默认值，清理时不能访问 Element 的存活守卫。</summary>
        protected virtual void WriteMaterial(UnityEngine.UI.MaskableGraphic target, Material value) => target.material = value;

        protected override void OnInitialize()
        {
            graphic = Graphic;
            OnDispose(() =>
            {
                resourceContext = null;
                // 仅解除原生材质借用；资源凭证负责真正归还，不能销毁项目共享材质。
                if (graphic != null)
                {
                    WriteMaterial(graphic, null);
                }
            });
        }
    }
}
