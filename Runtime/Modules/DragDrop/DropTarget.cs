using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.DragDrop
{
    /// <summary>业务接收目标，将生命周期、可用性查询及明确模式的提交函数组合为接收契约。</summary>
    public sealed class DropTarget<TPayload>
    {
        /// <summary>创建业务目标；不在构造时执行检查或提交。</summary>
        /// <param name="lifetime">目标激活周期，提交期间的工作登记于此，结束时联动取消。</param>
        /// <param name="commit">
        /// true 表示业务已确认提交，false 必须保证没有提交修改。
        /// 取消是合作式信号；已成功提交时应返回 true，不能用取消伪装失败。
        /// </param>
        /// <param name="canAccept">快速、无副作用的同步查询；省略时允许接收。最终提交仍需校验业务条件。</param>
        public DropTarget(Lifetime lifetime, Func<TPayload, CancellationToken, ValueTask<bool>> commit,
            Func<TPayload, bool> canAccept = null)
        {
            Lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
            if (lifetime.Mode == LifetimeMode.Synchronous)
            {
                throw new ArgumentException("同步目标请使用 CreateSynchronous。", nameof(lifetime));
            }
            Commit = commit ?? throw new ArgumentNullException(nameof(commit));
            CanAccept = canAccept ?? (_ => true);
        }

        private DropTarget(Lifetime lifetime, Func<TPayload, CancellationToken, bool> commit,
                    Func<TPayload, bool> canAccept)
        {
            Lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
            if (lifetime.Mode != LifetimeMode.Synchronous)
            {
                throw new ArgumentException("同步目标需要同步生命周期。", nameof(lifetime));
            }
            SynchronousCommit = commit ?? throw new ArgumentNullException(nameof(commit));
            CanAccept = canAccept ?? (_ => true);
        }

        /// <summary>目标提交模式，由构造入口确定，不探测异步委托是否立即完成。</summary>
        public LifetimeMode Mode => Lifetime.Mode;

        internal Lifetime Lifetime
        {
            get;
        }

        internal Func<TPayload, CancellationToken, ValueTask<bool>> Commit
        {
            get;
        }

        internal Func<TPayload, CancellationToken, bool> SynchronousCommit
        {
            get;
        }

        internal Func<TPayload, bool> CanAccept
        {
            get;
        }

        /// <summary>创建同步提交目标；false 表示没有提交修改，已提交成功应返回 true。</summary>
        public static DropTarget<TPayload> CreateSynchronous(Lifetime lifetime,
            Func<TPayload, CancellationToken, bool> commit, Func<TPayload, bool> canAccept = null) =>
            new DropTarget<TPayload>(lifetime, commit, canAccept);
    }
}
