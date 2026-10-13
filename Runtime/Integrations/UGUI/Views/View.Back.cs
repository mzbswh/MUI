using System;
using System.Collections.Generic;
using System.Threading;

namespace MUI.UGUI
{
    public sealed partial class View : ILocalBackView
    {
        private readonly object localBackGate = new object();
        private readonly List<LocalBackRegistration> localBackHandlers = new List<LocalBackRegistration>();
        private bool dispatchingLocalBack;

        /// <summary>
        /// 为局部临时状态登记同步返回处理器，后登记者优先；返回 true 停止页面返回。
        /// 生命周期取消即撤销，也可提前 Dispose；完成撤销不在 LifetimeScope 中保留释放记录。
        /// 子控件应向所属导航 View 登记并使用自身激活 LifetimeScope，不能使用实例缓存周期。
        /// 登记和派发须在 Unity 主线程执行；撤销只操作托管登记，可从取消线程执行。
        /// 并发撤销不抢占已取得执行资格的回调。
        /// </summary>
        public IDisposable RegisterLocalBack(LifetimeScope activation, Func<bool> handler)
        {
            RequireAlive();
            if (activation == null)
            {
                throw new ArgumentNullException(nameof(activation));
            }
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            if (activation.IsEnded)
            {
                throw new ObjectDisposedException(nameof(activation));
            }

            var registration = new LocalBackRegistration { Owner = this, Scope = activation, Handler = handler };
            lock (localBackGate)
            {
                localBackHandlers.Add(registration);
            }
            try
            {
                registration.AttachCancellation(this, activation.Token.Register(registration.Dispose));
                return registration;
            }
            catch
            {
                registration.Dispose();
                throw;
            }
        }

        /// <summary>按入口快照派发；已撤销项跳过，新登记项留给下次，重入消费但不重复执行。</summary>
        public bool TryHandleLocalBack()
        {
            if (!IsInputEnabled)
            {
                return false;
            }
            if (dispatchingLocalBack)
            {
                return true;
            }

            dispatchingLocalBack = true;
            try
            {
                LocalBackRegistration[] snapshot;
                lock (localBackGate)
                {
                    snapshot = localBackHandlers.ToArray();
                }
                for (var i = snapshot.Length - 1; i >= 0; --i)
                {
                    var registration = snapshot[i];
                    Func<bool> handler;
                    LifetimeScope activation;
                    lock (localBackGate)
                    {
                        if (!ReferenceEquals(registration.Owner, this))
                        {
                            continue;
                        }
                        handler = registration.Handler;
                        activation = registration.Scope;
                    }
                    if (activation.IsEnded)
                    {
                        continue;
                    }
                    if (handler())
                    {
                        return true;
                    }
                    // 回调关闭或禁用了界面，即使返回 false 也不能让同一输入继续穿透。
                    if (!IsInputEnabled)
                    {
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                dispatchingLocalBack = false;
            }
        }

        private void ClearLocalBackHandlers()
        {
            LocalBackRegistration[] snapshot;
            lock (localBackGate)
            {
                snapshot = localBackHandlers.ToArray();
            }
            List<Exception> errors = null;
            foreach (var registration in snapshot)
            {
                try
                {
                    registration.Dispose();
                }
                catch (Exception error)
                {
                    if (errors == null)
                    {
                        errors = new List<Exception>();
                    }

                    errors.Add(error);
                }
            }

            if (errors != null)
            {
                throw new AggregateException("View local back cleanup failed.", errors);
            }
        }

        private sealed class LocalBackRegistration : IDisposable
        {
            internal View Owner;
            internal LifetimeScope Scope;
            internal Func<bool> Handler;
            private CancellationTokenRegistration cancellation;

            public void Dispose()
            {
                var owner = Owner;
                // 取消可能来自后台线程，此处只使用托管身份，不调用 Unity 对象运算符。
                if (ReferenceEquals(owner, null))
                {
                    return;
                }
                CancellationTokenRegistration detached = default;
                lock (owner.localBackGate)
                {
                    if (ReferenceEquals(Owner, owner))
                    {
                        owner.localBackHandlers.Remove(this);
                        Owner = null;
                        Handler = null;
                        Scope = null;
                        detached = cancellation;
                        cancellation = default;
                    }
                }
                // 不在列表锁内等待取消回调结束，避免与并发撤销互等。
                detached.Dispose();
            }

            internal void AttachCancellation(View owner, CancellationTokenRegistration registration)
            {
                lock (owner.localBackGate)
                {
                    if (ReferenceEquals(Owner, owner))
                    {
                        cancellation = registration;
                        return;
                    }
                }
                // Register 可能因令牌已取消而同步撤销登记，不能遗留刚取得的注册。
                registration.Dispose();
            }
        }
    }
}
