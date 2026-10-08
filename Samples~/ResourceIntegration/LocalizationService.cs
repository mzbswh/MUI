using System;
using System.Threading;

namespace MUI.Samples.ResourceIntegration
{
    /// <summary>UI 线程上的文本格式化与订阅服务；同步发布项目提供的目录，不加载或释放目录资源。</summary>
    public sealed partial class LocalizationService : ISynchronousDisposable
    {
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private LocalizationCatalog catalog;
        private Action changed;
        private bool publishing;
        private bool disposed;
        private long version;

        /// <summary>借用项目已准备的目录；服务不加载或释放目录资源。</summary>
        public LocalizationService(LocalizationCatalog catalog)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        /// <summary>当前项目所提供目录的规范语言名。</summary>
        public string Locale
        {
            get
            {
                RequireAlive();
                return catalog == null ? null : catalog.Locale;
            }
        }

        /// <summary>目录应用代际，订阅使用它识别格式化期间状态是否改变。</summary>
        public long Version
        {
            get
            {
                RequireAlive();
                return version;
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

        /// <summary>应用项目已准备的目录并刷新控件；不执行加载、缓存或卸载。</summary>
        public void SetCatalog(LocalizationCatalog catalog)
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

        /// <summary>使用当前目录格式化文本；所有回退目录均缺键时抛出异常。</summary>
        public LocalizedTextValue Format(LocalizedMessage message)
        {
            RequireAlive();
            if (catalog == null)
            {
                throw new InvalidOperationException("语言目录不可用。");
            }

            return catalog.Format(message);
        }

        /// <summary>订阅并立即应用一次文本；调用方负责释放订阅，每个文本目标应只有一个写入者。</summary>
        public LocalizedTextSubscription Observe(LocalizedMessage message, Action<LocalizedTextValue> apply)
        {
            RequireAlive();
            return new LocalizedTextSubscription(this, message, apply);
        }

        /// <summary>订阅并立即应用文本，将退订托管给 owner；owner 结束后停止写入目标。</summary>
        public LocalizedTextSubscription Observe(LifetimeScope owner, LocalizedMessage message, Action<LocalizedTextValue> apply)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (apply == null)
            {
                throw new ArgumentNullException(nameof(apply));
            }

            if (owner.IsEnded)
            {
                throw new ObjectDisposedException(nameof(owner));
            }

            var subscription = Observe(message, value =>
            {
                if (!owner.IsEnded)
                {
                    apply(value);
                }
            });
            try
            {
                return owner.OwnDisposable(subscription);
            }
            catch
            {
                subscription.Dispose();
                throw;
            }
        }

        internal void Subscribe(Action listener)
        {
            RequireAlive();
            changed += listener;
        }

        internal void CheckAccess() => RequireAlive();

        internal void Unsubscribe(Action listener)
        {
            RequireThread();
            changed -= listener;
        }

        private void Apply(LocalizationCatalog value)
        {
            catalog = value;
            ++version;
            if (disposed || value == null)
            {
                return;
            }

            publishing = true;
            try
            {
                var handlers = changed;
                if (handlers == null)
                {
                    return;
                }

                foreach (Action handler in handlers.GetInvocationList())
                {
                    try
                    {
                        handler();
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
            changed = null;
            catalog = null;
        }

        private void RequireMutable()
        {
            RequireAlive();
            if (publishing)
            {
                throw new InvalidOperationException("Schedule a language change after the current text publication finishes.");
            }
        }

        private void RequireAlive()
        {
            RequireThread();
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(LocalizationService));
            }
        }

        private void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("Localization requires its owning UI thread.");
            }
        }
    }
}
