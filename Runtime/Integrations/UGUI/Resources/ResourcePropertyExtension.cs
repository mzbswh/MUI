using MUI.Unity;
using System;
using MUI.Resources;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>按需资源属性。只管理加载与借用寿命，不包装原生控件的普通属性。</summary>
    public abstract class ResourcePropertyExtension<TComponent, TAsset> : ControlPropertyExtension<TComponent>
        where TComponent : Component where TAsset : UnityEngine.Object
    {
        private ResourceSourceOwner<TAsset> resources;
        private readonly TComponent target;
        private readonly string sourceProperty;
        private readonly string nativeProperty;

        protected ResourcePropertyExtension(NativeControl owner, string sourceProperty, string nativeProperty) : base(owner)
        {
            target = Component;
            this.sourceProperty = sourceProperty;
            this.nativeProperty = nativeProperty;
            owner.RegisterWriteAlias(GetType().FullName + "." + sourceProperty, nativeProperty);
        }

        protected string Source
        {
            get { RequireAlive(); return resources == null ? null : resources.Source; }
            set
            {
                RequireAlive();
                if (resources == null)
                {
                    var view = target.GetComponentInParent<View>(true);
                    var context = view == null ? null : view.ActiveResourceContext;
                    if (context == null) throw new InvalidOperationException("请先配置 View 的资源加载器并激活 View。");
                    using (UIErrors.BeginContext(context.DiagnosticContext))
                        Configure(context.Scope, context.Loader, context);
                }
                resources.SetSource(value);
            }
        }

        protected ResourceSourceSnapshot Snapshot
        {
            get { RequireAlive(); return resources == null ? default : resources.SourceSnapshot; }
        }

        public void Configure(LifetimeScope lifetime, IResourceLoader loader) => Configure(lifetime, loader, null);

        private void Configure(LifetimeScope lifetime, IResourceLoader loader, ViewResourceContext context)
        {
            if (lifetime == null) throw new ArgumentNullException(nameof(lifetime));
            if (loader == null) throw new ArgumentNullException(nameof(loader));
            RequireAlive();
            if (resources != null) throw new InvalidOperationException("资源属性已有生命周期拥有者。");
            lifetime.Run(_ =>
            {
                var registration = new ResourceSourceOwner<TAsset>(released =>
                {
                    if (ReferenceEquals(resources, released)) resources = null;
                });
                lifetime.Own(registration);
                registration.Slot = new ResourceSlot<TAsset>(loader, Assign, lifetime.Token);
                resources = registration;
                registration.ConfigureSource(lifetime, () => Changed(sourceProperty),
                    () => Changed(sourceProperty + "Snapshot"),
                    context == null ? null : context.CreatePreparationTracker(registration, lifetime, Owner.Name + "." + sourceProperty));
            });
        }

        private void RequireAlive()
        {
            UnityMainThread.Require();
            if (!Owner.IsAlive) throw new ObjectDisposedException(Owner.Name);
        }

        private void Assign(TAsset asset)
        {
            var clearing = ReferenceEquals(asset, null);
            if (!clearing && (asset == null || !Owner.IsAlive || target == null))
                throw new InvalidOperationException("不能把资源赋给已销毁的组件或使用已销毁的资源。");
            if (target == null) return;
            resources.AssignNative(() => AssignAsset(target, asset),
                () => clearing && ConfirmCleared(target), () => SuspendDisplay(target));
            resources.CommitSource(clearing);
            if (Owner.IsAlive) resources.Notify(() => Owner.PublishGeneratedPropertyChange(nativeProperty));
        }

        protected abstract void AssignAsset(TComponent component, TAsset asset);
        protected abstract bool ConfirmCleared(TComponent component);
        protected abstract void SuspendDisplay(TComponent component);
    }
}
