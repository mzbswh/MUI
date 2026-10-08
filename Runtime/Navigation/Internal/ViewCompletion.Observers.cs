using System;
using System.Collections.Generic;
using System.Threading;

namespace MUI.Navigation
{
    internal sealed partial class ViewCompletion<T>
    {
        private readonly List<Subscription> observers = new List<Subscription>();
        private readonly int observerThread = Thread.CurrentThread.ManagedThreadId;
        private bool observersPublished;

        internal IDisposable Observe(LifetimeScope lifetime, Action<T> handler)
        {
            if (Thread.CurrentThread.ManagedThreadId != observerThread)
            {
                throw new InvalidOperationException("结果订阅必须在创建界面的 UI 线程登记。");
            }
            if (lifetime == null)
            {
                throw new ArgumentNullException(nameof(lifetime));
            }
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            if (lifetime.IsEnded)
            {
                throw new ObjectDisposedException(nameof(lifetime));
            }
            var subscription = new Subscription(this, lifetime, handler);
            try
            {
                subscription.AttachCancellation(this, lifetime.Token.Register(subscription.Dispose));
            }
            catch { subscription.Dispose(); throw; }

            bool deliver;
            T result;
            lock (gate)
            {
                if (subscription.Owner != this)
                {
                    return subscription;
                }
                deliver = observersPublished;
                result = value;
                if (!deliver)
                {
                    observers.Add(subscription);
                }
            }
            if (deliver)
            {
                subscription.Deliver(result);
            }
            return subscription;
        }

        /// <summary>由导航在终态登记或超时结果提交后派发，不能在写入结果的内部步骤中调用业务代码。</summary>
        internal void PublishObservers()
        {
            Subscription[] snapshot;
            T result;
            lock (gate)
            {
                if (!completed || observersPublished)
                {
                    return;
                }
                observersPublished = true;
                result = value;
                snapshot = observers.ToArray();
                observers.Clear();
            }
            foreach (var subscription in snapshot)
            {
                subscription.Deliver(result);
            }
        }

        private sealed class Subscription : IDisposable
        {
            internal ViewCompletion<T> Owner;
            private LifetimeScope lifetime;
            private Action<T> handler;
            private CancellationTokenRegistration cancellation;

            internal Subscription(ViewCompletion<T> owner, LifetimeScope lifetime, Action<T> handler)
            {
                Owner = owner;
                this.lifetime = lifetime;
                this.handler = handler;
            }

            internal void Deliver(T value)
            {
                var owner = Owner;
                if (owner == null)
                {
                    return;
                }
                Action<T> callback;
                LifetimeScope target;
                CancellationTokenRegistration registration;
                lock (owner.gate)
                {
                    if (Owner != owner)
                    {
                        return;
                    }
                    callback = handler;
                    target = lifetime;
                    registration = Detach(owner);
                }
                // 撤销令牌登记必须在锁外执行，避免与正在进入 Dispose 的取消回调互等。
                registration.Dispose();
                if (target.IsEnded)
                {
                    return;
                }
                try
                {
                    callback(value);
                }
                catch (Exception error)
                {
                    UIErrors.Report(error);
                }
            }

            public void Dispose()
            {
                var owner = Owner;
                if (owner == null)
                {
                    return;
                }
                CancellationTokenRegistration registration = default;
                lock (owner.gate)
                {
                    if (Owner == owner)
                    {
                        registration = Detach(owner);
                    }
                }
                registration.Dispose();
            }

            internal void AttachCancellation(ViewCompletion<T> owner, CancellationTokenRegistration registration)
            {
                lock (owner.gate)
                {
                    if (Owner == owner)
                    {
                        cancellation = registration;
                        return;
                    }
                }
                // 注册时令牌可能已经取消并内联撤销订阅，此时不能遗留新取得的登记。
                registration.Dispose();
            }

            private CancellationTokenRegistration Detach(ViewCompletion<T> owner)
            {
                owner.observers.Remove(this);
                Owner = null;
                lifetime = null;
                handler = null;
                var registration = cancellation;
                cancellation = default;
                return registration;
            }
        }
    }
}
