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
                    ConfigureResourceSource(ConfigureSynchronousMaterialSource, ConfigureMaterialSource);
                }

                if (materialResources == null)
                {
                    throw new InvalidOperationException("请先配置资源键加载器和生命周期。");
                }

                materialResources.SetSource(value);
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
            Action<Lifetime, ISynchronousResourceLoader> configureSynchronous,
            Action<Lifetime, IResourceLoader> configureAsync)
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
                if (context.SynchronousLoader != null)
                {
                    configureSynchronous(context.Lifetime, context.SynchronousLoader);
                }
                else
                {
                    configureAsync(context.Lifetime, context.Loader);
                }
            }
            finally
            {
                configuringDefaultResource = previous;
            }
        }

        private protected Action<Task<bool>> CreateResourcePreparationTracker(object owner, Lifetime lifetime, string propertyName)
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

        /// <summary>在绑定前配置纯同步资源键加载；绑定期间直接对象属性不能同时写入。</summary>
        public void ConfigureSynchronousMaterialSource(Lifetime owner, ISynchronousResourceLoader loader)
        {
            CreateSynchronousMaterialSlot(owner, loader);
            materialResources.ConfigureSource(owner, () => NotifyChanged(nameof(MaterialSource)),
                CreateResourcePreparationTracker(materialResources, owner, nameof(MaterialSource)));
        }

        /// <summary>在绑定前配置异步资源键加载；所属生命周期跟踪加载并等待释放。</summary>
        public void ConfigureMaterialSource(Lifetime owner, IResourceLoader loader)
        {
            CreateMaterialSlot(owner, loader);
            materialResources.ConfigureSource(owner, () => NotifyChanged(nameof(MaterialSource)),
                CreateResourcePreparationTracker(materialResources, owner, nameof(MaterialSource)));
        }

        /// <summary>创建并托管同步材质槽；使用 Replace/Clear 更新，所属生命周期负责最终释放。</summary>
        private ResourceSlot<Material> CreateSynchronousMaterialSlot(
            Lifetime owner, ISynchronousResourceLoader loader)
        {
            if (loader == null)
            {
                throw new ArgumentNullException(nameof(loader));
            }

            return CreateMaterialResources(owner, true, () =>
                ResourceSlot<Material>.CreateSynchronous(loader, AssignResourceMaterial, owner.Token));
        }

        /// <summary>创建并托管异步材质槽；替换及最终清理均由调用者等待，拒绝同步生命周期。</summary>
        private ResourceSlot<Material> CreateMaterialSlot(Lifetime owner, IResourceLoader loader)
        {
            if (loader == null)
            {
                throw new ArgumentNullException(nameof(loader));
            }

            return CreateMaterialResources(owner, false, () =>
                new ResourceSlot<Material>(loader, AssignResourceMaterial, owner.Token));
        }

        private ResourceSlot<Material> CreateMaterialResources(
                    Lifetime owner, bool synchronous, Func<ResourceSlot<Material>> create)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            RequireAlive();
            RequireUnmanagedMaterial();
            if (!synchronous && owner.Mode == LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("同步生命周期不能创建异步材质资源槽。");
            }

            return owner.Run(_ =>
            {
                Initialize();
                var registration = new ElementResourceOwner<Material>(synchronous, ReleaseMaterialOwner);
                // 先登记空持有者，创建失败也不会遗留无人等待的异步槽。
                if (synchronous)
                {
                    owner.OwnDisposable(registration);
                }
                else
                {
                    owner.Own(registration);
                }

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
                throw new InvalidOperationException("材质已由资源槽管理，请更新现有槽，或先释放其所属生命周期。");
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

            materialResources.AssignNative(() => WriteMaterial(graphic, material));
            materialResources.CommitSource(clearing);
            if (IsAlive)
            {
                materialResources.Notify(() => NotifyChanged(nameof(Material)));
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
