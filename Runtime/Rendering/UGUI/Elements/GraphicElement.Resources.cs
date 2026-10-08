using System;
using System.Threading.Tasks;
using MUI.Resources;
using UnityEngine;

namespace MUI.UGUI
{
    public abstract partial class GraphicElement : IBindingPropertyPolicy
    {
        private ElementResourceOwner<Material> materialResources;
        private ViewResourceContext resourceContext;
        private bool configuringDefaultResource;

        /// <summary>成功显示的资源键；赋值请求加载，空值清空，失败保留旧资源。</summary>
        public string MaterialSource
        {
            get
            {
                RequireAlive();
                return materialResources == null ? null : materialResources.Source;
            }
            set
            {
                RequireAlive();
                if (materialResources == null)
                {
                    ConfigureResourceSource(ConfigureMaterialSource);
                }

                if (materialResources == null)
                {
                    throw new InvalidOperationException("请先配置资源键加载器和生命周期。");
                }

                materialResources.SetSource(value);
            }
        }

        /// <summary>最新请求键、当前显示键及加载结果；重新设置请求键可显式重试。</summary>
        public ResourceSourceSnapshot MaterialSourceSnapshot
        {
            get
            {
                RequireAlive();
                return materialResources == null ? default : materialResources.SourceSnapshot;
            }
        }

        internal void BeginResourceActivation(ViewResourceContext context) => resourceContext = context;

        internal void EndResourceActivation(ViewResourceContext context)
        {
            if (ReferenceEquals(resourceContext, context))
            {
                resourceContext = null;
            }
        }

        /// <summary>仅在 Source 第一次写入时使用 View 的激活配置，直接资源绑定不占用槽。</summary>
        protected void ConfigureResourceSource(
            Action<LifetimeScope, IResourceLoader> configureAsync)
        {
            var context = resourceContext;
            if (context == null)
            {
                return;
            }

            var previous = configuringDefaultResource;
            configuringDefaultResource = true;
            try
            {
                using (UIErrors.BeginContext(context.DiagnosticContext))
                {
                    configureAsync(context.Scope, context.Loader);
                }
            }
            finally
            {
                configuringDefaultResource = previous;
            }
        }

        private protected Action<Task<bool>> CreateResourcePreparationTracker(object owner, LifetimeScope lifetime, string propertyName)
        {
            return !configuringDefaultResource || resourceContext == null
                ? null : resourceContext.CreatePreparationTracker(owner, lifetime, Name + "." + propertyName);
        }

        string IBindingPropertyPolicy.GetBindingWriteTarget(string propertyName, BindingMode mode) =>
                    GetBindingWriteTarget(propertyName, mode);

        protected virtual string GetBindingWriteTarget(string propertyName, BindingMode mode)
        {
            if (propertyName != nameof(MaterialSource))
            {
                return propertyName;
            }

            if (mode != BindingMode.OneWay && mode != BindingMode.OneTime)
            {
                throw new InvalidOperationException("资源键仅支持 OneWay 或 OneTime 绑定。");
            }

            return nameof(Material);
        }

        /// <summary>在绑定前配置异步资源键加载；所属生命周期跟踪加载并等待释放。</summary>
        public void ConfigureMaterialSource(LifetimeScope owner, IResourceLoader loader)
        {
            CreateMaterialSlot(owner, loader);
            materialResources.ConfigureSource(owner, () => NotifyChanged(nameof(MaterialSource)),
                () => NotifyChanged(nameof(MaterialSourceSnapshot)),
                CreateResourcePreparationTracker(materialResources, owner, nameof(MaterialSource)));
        }

        /// <summary>创建并托管异步材质槽；所属生命周期等待替换和最终清理。</summary>
        private ResourceSlot<Material> CreateMaterialSlot(LifetimeScope owner, IResourceLoader loader)
        {
            if (loader == null)
            {
                throw new ArgumentNullException(nameof(loader));
            }

            return CreateMaterialResources(owner, () =>
                new ResourceSlot<Material>(loader, AssignResourceMaterial, owner.Token));
        }

        private ResourceSlot<Material> CreateMaterialResources(
                    LifetimeScope owner, Func<ResourceSlot<Material>> create)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            RequireAlive();
            RequireUnmanagedMaterial();

            return owner.Run(_ =>
            {
                Initialize();
                var registration = new ElementResourceOwner<Material>(ReleaseMaterialOwner);
                // 先登记空持有者，创建失败也不会遗留无人等待的异步槽。
                owner.Own(registration);

                registration.Slot = create();
                materialResources = registration;
                return registration.Slot;
            });
        }

        /// <summary>派生控件的操作会间接替换材质时，也必须检查资源槽的独占持有。</summary>
        protected void RequireUnmanagedMaterial()
        {
            if (materialResources != null)
            {
                throw new InvalidOperationException("材质已有资源拥有者，不能再次配置或间接替换其持有的材质。");
            }
        }

        private void AssignResourceMaterial(Material material)
        {
            // 区分真正的清空和已销毁的 Unity 对象，避免提交无效候选。
            if (!ReferenceEquals(material, null) && (material == null || !IsAlive || graphic == null))
            {
                throw new InvalidOperationException("不能向已销毁的材质控件赋值，或使用已销毁的 Material。");
            }

            if (graphic == null)
            {
                return;
            }

            var clearing = ReferenceEquals(material, null);
            if (ReadMaterial(graphic) == material)
            {
                materialResources.CommitSource(clearing);
                return;
            }

            materialResources.AssignNative(() => WriteMaterial(graphic, material),
                () =>
                {
                    if (!clearing || graphic == null || !IsBuiltInGraphicMaterialTarget(graphic))
                    {
                        return false;
                    }

                    // 内置 Graphic 先清空 m_Material 再通知；再次清空成功后解除渲染器引用。
                    graphic.material = null;
                    graphic.canvasRenderer.Clear();
                    return true;
                }, () => SuspendResourceDisplay(graphic));
            materialResources.CommitSource(clearing);
            if (IsAlive)
            {
                materialResources.Notify(() => NotifyChanged(nameof(Material)));
            }
        }

        private bool IsBuiltInGraphicMaterialTarget(UnityEngine.UI.MaskableGraphic target)
        {
            var elementType = GetType();
            var graphicType = target.GetType();
            return (elementType == typeof(ImageElement) && graphicType == typeof(UnityEngine.UI.Image)) ||
                (elementType == typeof(RawImageElement) && graphicType == typeof(UnityEngine.UI.RawImage)) ||
                (elementType == typeof(TextElement) && graphicType == typeof(UnityEngine.UI.Text));
        }

        /// <summary>赋值后的原生引用无法确认时停止渲染；保留相关凭证，交由所属生命周期清理。</summary>
        protected static void SuspendResourceDisplay(UnityEngine.UI.MaskableGraphic target)
        {
            if (target == null)
            {
                return;
            }

            try
            {
                target.enabled = false;
            }
            finally
            {
                if (target != null)
                {
                    target.canvasRenderer.Clear();
                }
            }
        }

        private void ReleaseMaterialOwner(ElementResourceOwner<Material> owner)
        {
            if (ReferenceEquals(materialResources, owner))
            {
                materialResources = null;
            }
        }
    }
}
