using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Pages
{
    public readonly struct PageHandle : IEquatable<PageHandle>
    {
        internal PageHandle(Guid host, long id)
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

        public bool Equals(PageHandle other) => Host == other.Host && Id == other.Id;

        public override bool Equals(object obj) => obj is PageHandle other && Equals(other);

        public override int GetHashCode() => unchecked(Host.GetHashCode() * 397 ^ Id.GetHashCode());

        public static bool operator ==(PageHandle left, PageHandle right) => left.Equals(right);

        public static bool operator !=(PageHandle left, PageHandle right) => !left.Equals(right);

        public override string ToString() => Host.ToString("N") + ":" + Id;
    }

    public readonly struct PageHandle<TResult>
    {
        private readonly ViewCompletion<PageResult<TResult>> result;
        private readonly ViewCompletion<PageReadiness> readiness;
        private readonly ViewCompletion<PageTransition> enterTransition;
        private readonly ViewCompletion<PageTransition> exitTransition;

        internal PageHandle(PageHandle identity, ViewCompletion<PageResult<TResult>> result, ViewCompletion<PageReadiness> readiness,
            ViewCompletion<PageTransition> enterTransition, ViewCompletion<PageTransition> exitTransition)
        {
            Identity = identity;
            this.result = result;
            this.readiness = readiness;
            this.enterTransition = enterTransition;
            this.exitTransition = exitTransition;
        }

        public PageHandle Identity
        {
            get;
        }

        public bool IsValid => Identity.IsValid && result != null && readiness != null;

        public PageReadiness Readiness
        {
            get
            {
                if (!IsValid)
                {
                    throw new InvalidOperationException("Invalid PageHandle.");
                }

                return readiness.TryGetResult(out var current) ? current : new PageReadiness(PageReadinessStatus.Pending);
            }
        }

        /// <summary>直接读取已发布的业务结果；尚未结束时返回 false，不创建或等待任务。</summary>
        public bool TryGetResult(out PageResult<TResult> value)
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("Invalid PageHandle.");
            }

            return result.TryGetResult(out value);
        }

        /// <summary>
        /// 在所属 UI 线程订阅一次结果，不创建任务。已发布结果立即回调；否则在导航结果通知阶段回调。
        /// 派发检查时 LifetimeScope 已结束则跳过，也可提前 Dispose 撤销；并发取消不抢占已取得执行资格的回调。
        /// 异常交给 UIErrors，不中断其他订阅者。
        /// </summary>
        public IDisposable ObserveResult(LifetimeScope lifetime, Action<PageResult<TResult>> observer)
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("Invalid PageHandle.");
            }
            return result.Observe(lifetime, observer);
        }

        /// <summary>
        /// 在所属 UI 线程订阅一次就绪结果，不创建任务；就绪前关闭也会通知。
        /// 与结果订阅使用相同的 LifetimeScope 撤销和异常隔离规则，Ready 不代表输入门控一定开放。
        /// </summary>
        public IDisposable ObserveReadiness(LifetimeScope lifetime, Action<PageReadiness> observer)
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("Invalid PageHandle.");
            }
            return readiness.Observe(lifetime, observer);
        }

        /// <summary>取消只结束本次等待，共享激活结果保持不变。</summary>
        public ValueTask<PageReadiness> WaitUntilReadyAsync(CancellationToken cancellationToken = default)
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("Invalid PageHandle.");
            }

            return new ValueTask<PageReadiness>(PageReadiness.WaitAsync(readiness.Task, cancellationToken));
        }

        public ValueTask<PageResult<TResult>> WaitForResultAsync(CancellationToken cancellationToken = default)
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("Invalid PageHandle.");
            }

            return new ValueTask<PageResult<TResult>>(AsyncWait.WithCancellation(result.Task, cancellationToken));
        }

        /// <summary>等待本次进入视觉结束；提前关闭返回 Interrupted，取消等待不终止动画。</summary>
        public Task<PageTransition> WaitForEnterAsync(CancellationToken cancellationToken = default)
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("Invalid PageHandle.");
            }

            return AsyncWait.WithCancellation(enterTransition.Task, cancellationToken);
        }

        /// <summary>等待退出画面移除及屏障收尾，不发起关闭，也不等待资源清理。取消只影响本次等待。</summary>
        public Task<PageTransition> WaitForExitAsync(CancellationToken cancellationToken = default)
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("Invalid PageHandle.");
            }

            return AsyncWait.WithCancellation(exitTransition.Task, cancellationToken);
        }
    }
}
