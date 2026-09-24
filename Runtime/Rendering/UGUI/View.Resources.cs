using System;
using MUI.Resources;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class View
    {
        private ISynchronousResourceLoader synchronousResourceLoader;
        private IResourceLoader resourceLoader;
        private ViewResourceContext activeResourceContext;
        [SerializeField]
        [Tooltip("未显式配置加载器时，借用最近父 View 的活动加载器；资源仍随自身激活释放。关闭后不会继续向祖先查找。")]
        private bool inheritParentResources = true;
        [SerializeField]
        [Tooltip("等待本 View 默认加载器的异步资源键绑定完成后再提交显示；后续更新仍渐进加载。")]
        private bool waitForResourceSources;

        /// <summary>将默认资源键绑定纳入首次准备；须在激活前配置，默认关闭。</summary>
        public bool WaitForResourceSources
        {
            get => waitForResourceSources;
            set
            {
                RequireResourceConfigurationIdle();
                waitForResourceSources = value;
            }
        }

        /// <summary>未显式配置加载器时，借用最近父 View 的活动加载器；资源仍归自己的激活所有。</summary>
        public bool InheritParentResources
        {
            get => inheritParentResources;
            set
            {
                RequireResourceConfigurationIdle();
                inheritParentResources = value;
            }
        }

        /// <summary>
        /// 在激活前配置纯同步资源键加载器。仅绑定到 Source 属性的控件会创建资源槽，
        /// 槽随每次激活结束而释放；加载器由项目持有，不由 View 销毁。
        /// </summary>
        public void ConfigureSynchronousResources(ISynchronousResourceLoader loader)
        {
            if (loader == null)
            {
                throw new ArgumentNullException(nameof(loader));
            }

            RequireResourceConfigurationIdle();
            synchronousResourceLoader = loader;
            resourceLoader = null;
        }

        /// <summary>在激活前配置异步资源键加载器；不能用于纯同步激活。</summary>
        public void ConfigureResources(IResourceLoader loader)
        {
            if (loader == null)
            {
                throw new ArgumentNullException(nameof(loader));
            }

            RequireResourceConfigurationIdle();
            resourceLoader = loader;
            synchronousResourceLoader = null;
        }

        private void RequireResourceConfigurationIdle()
        {
            RequireAlive();
            if (retainingVisuals || releasingVisuals || (childViews != null && !childViews.IsDisposedSuccessfully))
            {
                throw new InvalidOperationException("资源加载器只能在 View 激活前或上次激活成功清理后配置。");
            }
        }

        private ViewResourceContext CreateResourceContext(Lifetime activation)
        {
            var synchronousLoader = synchronousResourceLoader;
            var asynchronousLoader = resourceLoader;
            if (synchronousLoader == null && asynchronousLoader == null && inheritParentResources)
            {
                var parent = transform.parent;
                var parentView = parent == null ? null : parent.GetComponentInParent<View>(true);
                var inherited = parentView == null ? null : parentView.activeResourceContext;
                // 最近的父 View 就是继承边界，不越过它借用更远祖先的配置。
                if (inherited != null && !inherited.Lifetime.IsEnded)
                {
                    synchronousLoader = inherited.SynchronousLoader;
                    asynchronousLoader = inherited.Loader;
                }
            }

            if (synchronousLoader == null && asynchronousLoader == null)
            {
                return null;
            }

            if (activation.Mode == LifetimeMode.Synchronous && synchronousLoader == null)
            {
                throw new InvalidOperationException("纯同步 View 激活需要同步资源加载器。");
            }

            return new ViewResourceContext(activation, synchronousLoader, asynchronousLoader, waitForResourceSources);
        }

        private void BeginResourceContext(Lifetime activation, ViewResourceContext context)
        {
            if (context == null)
            {
                activeResourceContext = null;
                return;
            }

            // 早于控件资源登记：逆序清理时先归还资源，再解除供继承使用的加载器引用。
            activation.OnDispose(() =>
            {
                if (!ReferenceEquals(activeResourceContext, context))
                {
                    return;
                }

                context.ReleasePreparation();
                activeResourceContext = null;
                foreach (var element in elements)
                {
                    if (element is GraphicElement graphic)
                    {
                        graphic.EndResourceActivation(context);
                    }
                }
            });
            activeResourceContext = context;
        }
    }
}
