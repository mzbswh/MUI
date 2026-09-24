using System;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>在子视图准备前，将各自拥有凭证的界面挂载到内容区域。</summary>
    public sealed class ContentViewProvider : IVersionedViewProvider, ISynchronousViewProvider
    {
        private readonly object provider;
        private readonly Transform parent;
        private readonly bool visible;
        private readonly bool constrainSorting;

        public ContentViewProvider(IViewProvider provider, Transform parent, bool visible = true, bool constrainSorting = false)
                    : this((object)provider, parent, visible, constrainSorting)
        {
        }

        private ContentViewProvider(object provider, Transform parent, bool visible, bool constrainSorting)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            this.parent = parent;
            this.visible = visible;
            this.constrainSorting = constrainSorting;
        }

        /// <summary>装饰器透传底层代际，不掩盖资源失效；固定提供方使用自身身份。</summary>
        public object ContentVersion
        {
            get
            {
                var version = provider is IViewContentVersion versioned ? versioned.ContentVersion : provider;
                if (version == null || version.GetType().IsValueType)
                {
                    throw new InvalidOperationException("Provider content version must be a non-null reference token.");
                }

                return version;
            }
        }

        /// <summary>包装仅实现同步契约的提供方，无需实现异步接口。</summary>
        public static ContentViewProvider CreateSynchronous(ISynchronousViewProvider provider, Transform parent,
            bool visible = true, bool constrainSorting = false)
        {
            return new ContentViewProvider((object)provider, parent, visible, constrainSorting);
        }

        public SyncCreateAvailability GetSyncAvailability(ViewResource resource)
        {
            if (parent == null || !(provider is ISynchronousViewProvider synchronous))
            {
                return SyncCreateAvailability.Unsupported;
            }

            return synchronous.GetSyncAvailability(resource);
        }

        public IViewLease Create(ViewResource resource) => CreateSynchronous(resource);

        ISynchronousViewLease ISynchronousViewProvider.Create(ViewResource resource) => CreateSynchronous(resource);

        private SynchronousViewLease CreateSynchronous(ViewResource resource)
        {
            if (!(provider is ISynchronousViewProvider synchronous))
            {
                throw new NotSupportedException("Content requires a provider with synchronous creation and release.");
            }

            if (parent == null)
            {
                throw new ObjectDisposedException("Content host");
            }

            var version = ContentVersion ?? throw new InvalidOperationException("Provider content version cannot be null.");
            if (parent == null)
            {
                throw new ObjectDisposedException("Content host");
            }

            var lease = synchronous.Create(resource);
            if (lease == null)
            {
                throw new InvalidOperationException("Content provider returned no lease.");
            }

            try
            {
                Mount(lease.View, version, CancellationToken.None);
                return new SynchronousViewLease(lease.View, _ => lease.Dispose());
            }
            catch (Exception failure)
            {
                try
                {
                    lease.Dispose();
                }
                catch (Exception cleanup)
                {
                    throw new SynchronousResourceLoadException(failure, cleanup);
                }

                throw;
            }
        }

        public async ValueTask<IViewLease> CreateAsync(ViewResource resource, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (parent == null)
            {
                throw new ObjectDisposedException("Content host");
            }

            var version = ContentVersion ?? throw new InvalidOperationException("Provider content version cannot be null.");
            if (!(provider is IViewProvider asynchronous))
            {
                throw new NotSupportedException("This content provider only supports synchronous creation.");
            }

            token.ThrowIfCancellationRequested();
            if (parent == null)
            {
                throw new ObjectDisposedException("Content host");
            }

            var lease = await asynchronous.CreateAsync(resource, token);
            if (lease == null)
            {
                throw new InvalidOperationException("Content provider returned no lease.");
            }

            try
            {
                Mount(lease.View, version, token);
                return lease;
            }
            catch (Exception failure)
            {
                try
                {
                    await lease.DisposeAsync();
                }
                catch (Exception cleanup)
                {
                    throw new AggregateException(failure, cleanup);
                }

                throw;
            }
        }

        /// <summary>共享挂载校验与代际复核；此方法不加载资源、不启动异步工作。</summary>
        private void Mount(IView instance, object version, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (parent == null)
            {
                throw new OperationCanceledException("Content host was destroyed.", token);
            }

            var view = instance as View;
            if (view == null || !view.IsAlive)
            {
                throw new InvalidOperationException("Content requires a live uGUI View.");
            }

            if (view.transform == parent || parent.IsChildOf(view.transform))
            {
                throw new InvalidOperationException("Provider returned an ancestor of the content host.");
            }

            if (constrainSorting)
            {
                foreach (var canvas in view.GetComponentsInChildren<Canvas>(true))
                {
                    if (canvas.overrideSorting)
                    {
                        throw new InvalidOperationException("Content Canvas cannot override the parent region's sorting.");
                    }
                }
            }

            view.SetHostState(false, false);
            view.Visible = visible;
            view.transform.SetParent(parent, false);
            view.transform.localPosition = Vector3.zero;
            var currentVersion = ContentVersion;
            token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(version, currentVersion))
            {
                throw new OperationCanceledException("Content changed while preparing its host.");
            }

            // 原生挂载和代际读取可能触发项目回调，交出资源前复核其实际归属。
            if (parent == null || view == null || !view.IsAlive || view.transform.parent != parent)
            {
                throw new InvalidOperationException("Content host or mounted view changed during preparation.");
            }
        }
    }
}
