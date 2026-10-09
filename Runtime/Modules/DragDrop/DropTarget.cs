using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.DragDrop
{
    /// <summary>业务接收目标，将生命周期、可用性查询及提交函数组合为接收契约。</summary>
    public sealed class DropTarget<TPayload>
    {
        /// <summary>创建业务目标；不在构造时执行检查或提交。</summary>
        /// <param name="lifetime">目标激活周期，提交期间的工作登记于此，结束时联动取消。</param>
        /// <param name="commit">
        /// true 表示业务已确认提交，false 必须保证没有提交修改。
        /// 取消是合作式信号；已成功提交时应返回 true，不能用取消伪装失败。
        /// </param>
        /// <param name="canAccept">快速、无副作用的同步查询；省略时允许接收。最终提交仍需校验业务条件。</param>
        public DropTarget(LifetimeScope lifetime, Func<TPayload, CancellationToken, ValueTask<bool>> commit,
            Func<TPayload, bool> canAccept = null)
        {
            Scope = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
            Commit = commit ?? throw new ArgumentNullException(nameof(commit));
            CanAccept = canAccept ?? (_ => true);
        }

        internal LifetimeScope Scope
        {
            get;
        }

        internal Func<TPayload, CancellationToken, ValueTask<bool>> Commit
        {
            get;
        }

        internal Func<TPayload, bool> CanAccept
        {
            get;
        }
    }
}
