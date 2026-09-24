using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public readonly struct ViewHandle : IEquatable<ViewHandle>
    {
        internal ViewHandle(Guid host, long id)
        {
            Host = host;
            Id = id;
        }

        public Guid Host
        {
            get;
        }

        public long Id
        {
            get;
        }

        public bool IsValid => Host != Guid.Empty && Id > 0;

        public bool Equals(ViewHandle other) => Host == other.Host && Id == other.Id;

        public override bool Equals(object obj) => obj is ViewHandle other && Equals(other);

        public override int GetHashCode() => unchecked(Host.GetHashCode() * 397 ^ Id.GetHashCode());

        public static bool operator ==(ViewHandle left, ViewHandle right) => left.Equals(right);

        public static bool operator !=(ViewHandle left, ViewHandle right) => !left.Equals(right);

        public override string ToString() => Host.ToString("N") + ":" + Id;
    }

    public readonly struct ViewHandle<TResult>
    {
        private readonly ViewCompletion<ViewResult<TResult>> result;
        private readonly ViewCompletion<ViewReadiness> readiness;

        internal ViewHandle(ViewHandle identity, ViewCompletion<ViewResult<TResult>> result, ViewCompletion<ViewReadiness> readiness)
        {
            Identity = identity;
            this.result = result;
            this.readiness = readiness;
        }

        public ViewHandle Identity
        {
            get;
        }

        public bool IsValid => Identity.IsValid && result != null && readiness != null;

        public ViewReadiness Readiness
        {
            get
            {
                if (!IsValid)
                {
                    throw new InvalidOperationException("Invalid ViewHandle.");
                }

                return readiness.TryGetResult(out var current) ? current : new ViewReadiness(ViewReadinessStatus.Transitioning);
            }
        }

        /// <summary>直接读取已发布的业务结果；尚未结束时返回 false，不创建或等待任务。</summary>
        public bool TryGetResult(out ViewResult<TResult> value)
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("Invalid ViewHandle.");
            }

            return result.TryGetResult(out value);
        }

        /// <summary>
        /// 在所属 UI 线程订阅一次结果，不创建任务。已发布结果立即回调；否则在导航结果通知阶段回调。
        /// 派发检查时 Lifetime 已结束则跳过，也可提前 Dispose 撤销；并发取消不抢占已取得执行资格的回调。
        /// 异常交给 UIErrors，不中断其他订阅者。
        /// </summary>
        public IDisposable ObserveResult(Lifetime lifetime, Action<ViewResult<TResult>> observer)
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("Invalid ViewHandle.");
            }
            return result.Observe(lifetime, observer);
        }

        /// <summary>
        /// 在所属 UI 线程订阅一次就绪结果，不创建任务；就绪前关闭也会通知。
        /// 与结果订阅使用相同的 Lifetime 撤销和异常隔离规则，Ready 不代表输入门控一定开放。
        /// </summary>
        public IDisposable ObserveReadiness(Lifetime lifetime, Action<ViewReadiness> observer)
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("Invalid ViewHandle.");
            }
            return readiness.Observe(lifetime, observer);
        }

        /// <summary>取消只结束本次等待，共享激活结果保持不变。</summary>
        public ValueTask<ViewReadiness> WaitUntilReadyAsync(CancellationToken cancellationToken = default)
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("Invalid ViewHandle.");
            }

            return new ValueTask<ViewReadiness>(ViewReadiness.WaitAsync(readiness.Task, cancellationToken));
        }

        public ValueTask<ViewResult<TResult>> WaitForResultAsync(CancellationToken cancellationToken = default)
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("Invalid ViewHandle.");
            }

            return new ValueTask<ViewResult<TResult>>(AsyncWait.WithCancellation(result.Task, cancellationToken));
        }
    }
}
