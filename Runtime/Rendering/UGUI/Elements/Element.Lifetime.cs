using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.UGUI
{
    public abstract partial class Element
    {
        private LifetimeScope cleanupScope;
        private CleanupResponsibility cleanupResponsibility;
        private Exception cleanupFailure;

        /// <summary>最终控件清理的稳定责任；显式恢复只确认叶责任，不重新执行未知回调。</summary>
        public CleanupResponsibility CleanupResponsibility
        {
            get
            {
                UnityMainThread.Require();
                return cleanupResponsibility ?? (cleanupResponsibility = new CleanupResponsibility(
                    ReleaseElementAsync, GetType().Name + ".Cleanup", true, Thread.CurrentThread.ManagedThreadId));
            }
        }

        /// <summary>
        /// 创建随此控件最终释放的属性。初始值不调用原生 setter；公开属性使用 Value 转发读写，
        /// propertyName 必须与该公开属性的通知名称一致。
        /// </summary>
        protected ElementProperty<T> CreateProperty<T>(string propertyName, T initialValue = default,
            Action<T, T> onValueChanged = null, IEqualityComparer<T> comparer = null)
        {
            RequireAlive();
            return Track(new ElementProperty<T>(this, propertyName, initialValue, onValueChanged, comparer));
        }

        /// <summary>仅登记成功时接管同步资源；同一资源不能重复登记，释放按统一作用域逆序执行。</summary>
        protected T Track<T>(T resource) where T : class, IDisposable
        {
            RequireAlive();
            return GetCleanupScope().OwnDisposable(resource);
        }

        /// <summary>
        /// 登记最终同步清理；初始化失败也执行。默认不重试未知回调，只有整个回调可幂等重复执行时
        /// 才声明 supportsIdempotentRetry。激活及异步资源沿其显式 LifetimeScope 管理。
        /// </summary>
        protected void TrackCleanup(Action callback, bool supportsIdempotentRetry = false, string owner = null)
        {
            RequireAlive();
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            GetCleanupScope().OnDisposeAsync(() =>
            {
                callback();
                return default;
            }, supportsIdempotentRetry, owner ?? GetType().Name + ".Callback", Thread.CurrentThread.ManagedThreadId);
        }

        /// <summary>原生同步收尾仅读取已完成结果；重复调用保持首次结果，不自动重试失败。</summary>
        public void Dispose()
        {
            var disposal = DisposeAsync();
            if (!disposal.IsCompleted)
            {
                throw new InvalidOperationException("Element final callbacks must complete synchronously.");
            }

            disposal.GetAwaiter().GetResult();
        }

        /// <summary>观察同一次最终清理；普通同步控件在调用帧完成，失败继续由统一账本持有。</summary>
        public ValueTask DisposeAsync()
        {
            UnityMainThread.Require();
            return CleanupResponsibility.DisposeAsync();
        }

        internal void PublishPropertyChange(string property)
        {
            RequireAlive();
            NotifyChanged(property);
        }

        private LifetimeScope GetCleanupScope() => cleanupScope ?? (cleanupScope = new LifetimeScope());

        private ValueTask ReleaseElementAsync()
        {
            if (disposed)
            {
                if (cleanupScope == null || cleanupScope.IsCleanupConfirmed)
                {
                    cleanupFailure = null;
                    return default;
                }

                throw cleanupFailure ?? new InvalidOperationException("Element cleanup dependencies are unconfirmed.");
            }

            disposed = true;
            try
            {
                if (cleanupScope != null)
                {
                    var disposal = cleanupScope.DisposeAsync();
                    if (!disposal.IsCompleted)
                    {
                        throw new InvalidOperationException("Element final callbacks must complete synchronously.");
                    }

                    disposal.GetAwaiter().GetResult();
                }
            }
            catch (Exception error)
            {
                cleanupFailure = error;
                throw;
            }
            finally
            {
                PropertyChanged = null;
            }

            return default;
        }
    }
}
