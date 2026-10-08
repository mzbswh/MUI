using System;
using System.Collections.Generic;

namespace MUI.Loading
{
    /// <summary>
    /// 一个加载提示的持有令牌。释放会移除汇总项并归还输入阻挡，不代表取消实际业务工作。
    /// 活跃令牌的进度上报与释放必须在 LoadingScope 的所属线程执行。
    /// </summary>
    public sealed class LoadingOperation : IDisposable
    {
        private LoadingScope owner;
        private IDisposable blocker;
        internal LinkedListNode<LoadingOperation> OwnershipNode;

        internal LoadingOperation(LoadingScope owner, string message, double weight, bool blocksInput)
        {
            this.owner = owner;
            Message = message;
            Weight = weight;
            BlocksInput = blocksInput;
        }

        /// <summary>此操作注册时的提示文案。</summary>
        public string Message
        {
            get;
        }

        /// <summary>是否仍登记在加载组中；释放后不再接收进度。</summary>
        public bool IsActive => owner != null;

        internal double Weight
        {
            get;
        }

        internal bool BlocksInput
        {
            get;
        }

        internal double? Progress
        {
            get; set;
        }

        /// <summary>
        /// 上报 0 到 1 的有限进度；null 表示无法确定。任一活跃项不确定时，汇总进度也为 null。
        /// 已释放令牌的上报返回 false，且不再校验进度值。
        /// </summary>
        public bool Report(double? progress)
        {
            return owner != null && owner.Report(this, progress);
        }

        internal void AttachBlocker(IDisposable value)
        {
            if (owner == null)
            {
                value.Dispose();
            }
            else
            {
                blocker = value;
            }
        }

        internal void ReleaseBlocker()
        {
            var value = blocker;
            blocker = null;
            if (value != null)
            {
                value.Dispose();
            }
        }

        /// <summary>幂等释放提示及其输入阻挡；owner 后续清理再次调用时不会重复释放。</summary>
        public void Dispose()
        {
            var current = owner;
            if (current == null)
            {
                return;
            }

            current.RequireThread();
            owner = null;
            // 提前结束时立即撤销活动记录，长驻生命周期不保留历史提示令牌。
            if (OwnershipNode != null)
            {
                OwnershipNode.List.Remove(OwnershipNode);
                OwnershipNode = null;
            }
            current.Release(this);
        }
    }
}
