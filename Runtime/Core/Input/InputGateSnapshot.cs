using System;
using System.Collections.Generic;

namespace MUI
{
    /// <summary>输入阻挡集合的有界只读副本，不持有阻挡令牌或其拥有者。</summary>
    public sealed class InputGateSnapshot
    {
        internal InputGateSnapshot(bool disposed, int blockerCount, string[] reasons)
        {
            IsDisposed = disposed;
            BlockerCount = blockerCount;
            Reasons = Array.AsReadOnly(reasons);
        }

        public bool IsDisposed
        {
            get;
        }

        public bool IsOpen => !IsDisposed && BlockerCount == 0;

        public int BlockerCount
        {
            get;
        }

        /// <summary>每个阻挡独立收录，即使多个阻挡使用相同原因，也不合并数量。</summary>
        public IReadOnlyList<string> Reasons
        {
            get;
        }

        public bool IsTruncated => Reasons.Count < BlockerCount;
    }
}
