using System;
using System.Collections.Generic;
using System.Threading;

namespace MUI.Themes
{
    /// <summary>
    /// 所属 UI 线程上的主题服务。先校验活动订阅所需的值，再提交目录并同步刷新目标。
    /// 目录由项目持有；控件写入失败会报告错误，不回滚已提交的整个主题。
    /// </summary>
    public sealed partial class ThemeService : ISynchronousDisposable
    {
        private readonly List<ISubscription> subscriptions = new List<ISubscription>();
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private ThemeCatalog catalog;
        private bool publishing;
        private bool disposed;

        /// <summary>借用项目已准备的目录；服务不加载或释放目录资源。</summary>
        public ThemeService(ThemeCatalog catalog)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        /// <summary>当前项目所提供目录的主题名。</summary>
        public string Name
        {
            get
            {
                RequireAlive();
                return catalog == null ? null : catalog.Name;
            }
        }

        /// <summary>显示数据服务始终同步释放，不启动或等待任务。</summary>
        public bool CanDisposeSynchronously
        {
            get
            {
                RequireThread();
                return !publishing;
            }
        }

        /// <summary>查找当前主题及回退目录；缺键或类型不匹配时抛出异常。</summary>
        public T Get<T>(ThemeToken<T> token)
        {
            RequireAlive();
            if (catalog == null)
            {
                throw new InvalidOperationException("主题目录不可用。");
            }

            return catalog.Get(token);
        }

        /// <summary>应用项目已准备的目录并刷新控件；不执行加载、缓存或卸载。</summary>
        public void SetCatalog(ThemeCatalog catalog)
        {
            RequireMutable();
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
            if (ReferenceEquals(this.catalog, catalog))
            {
                return;
            }
            Apply(catalog);
        }

        /// <summary>
        /// 立即应用一次当前值，并在后续切换时刷新。订阅交给 owner 托管，也可提前释放。
        /// 一个目标属性应只有一个绑定写入者；释放订阅不会恢复目标旧值。
        /// </summary>
        public IDisposable Observe<T>(Lifetime owner, ThemeToken<T> token, Action<T> apply)
        {
            RequireAlive();
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (owner.IsEnded)
            {
                throw new ObjectDisposedException(nameof(owner));
            }

            if (apply == null)
            {
                throw new ArgumentNullException(nameof(apply));
            }

            var subscription = new Subscription<T>(this, owner, token, apply);
            subscriptions.Add(subscription);
            try
            {
                var previousPublishing = publishing;
                publishing = true;
                try
                {
                    subscription.Publish(catalog);
                }
                finally
                {
                    publishing = previousPublishing;
                }

                return owner.OwnDisposable(subscription);
            }
            catch
            {
                subscription.Dispose();
                throw;
            }
        }

        private void Apply(ThemeCatalog next)
        {
            if (disposed || next == null)
            {
                catalog = next;
                return;
            }

            var snapshot = subscriptions.ToArray();
            // 写入目标前只做值查找，缺键或类型不匹配会使候选失败，保留原主题显示状态。
            foreach (var subscription in snapshot)
            {
                subscription.Validate(next);
            }

            catalog = next;
            publishing = true;
            try
            {
                foreach (var subscription in snapshot)
                {
                    try
                    {
                        subscription.Publish(next);
                    }
                    catch (Exception error)
                    {
                        UIErrors.Report(error);
                    }
                }
            }
            finally
            {
                publishing = false;
            }
        }

        /// <summary>只解除 UI 订阅及目录引用；项目继续负责目录资源持有权。</summary>
        public void Dispose()
        {
            RequireThread();
            if (publishing)
            {
                throw new InvalidOperationException("不能在目录发布回调中销毁服务。");
            }
            if (disposed)
            {
                return;
            }
            disposed = true;
            foreach (var subscription in subscriptions.ToArray())
            {
                subscription.Dispose();
            }
            catalog = null;
        }

        private void RequireMutable()
        {
            RequireAlive();
            if (publishing)
            {
                throw new InvalidOperationException("Schedule theme changes after the current publication.");
            }
        }

        private void RequireAlive()
        {
            RequireThread();
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(ThemeService));
            }
        }

        private void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("Theme service requires its owning UI thread.");
            }
        }

        private interface ISubscription : IDisposable
        {
            void Validate(ThemeCatalog candidate);

            void Publish(ThemeCatalog candidate);
        }

        private sealed class Subscription<T> : ISubscription
        {
            private ThemeService service;
            private Lifetime owner;
            private Action<T> apply;
            private readonly ThemeToken<T> token;

            internal Subscription(ThemeService service, Lifetime owner, ThemeToken<T> token, Action<T> apply)
            {
                this.service = service;
                this.owner = owner;
                this.token = token;
                this.apply = apply;
            }

            public void Validate(ThemeCatalog candidate)
            {
                if (service != null && !owner.IsEnded)
                {
                    candidate.Get(token);
                }
            }

            public void Publish(ThemeCatalog candidate)
            {
                if (service == null || owner.IsEnded)
                {
                    return;
                }

                if (candidate == null)
                {
                    throw new InvalidOperationException("主题目录不可用。");
                }

                apply(candidate.Get(token));
            }

            public void Dispose()
            {
                if (service == null)
                {
                    return;
                }

                service.RequireThread();
                service.subscriptions.Remove(this);
                service = null;
                owner = null;
                apply = null;
            }
        }
    }
}
