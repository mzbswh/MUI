using System;

namespace MUI.UGUI
{
    public enum ResourceSourceState
    {
        Empty,
        Loading,
        Displayed,
        Failed,
        Cancelled
    }

    /// <summary>资源键请求与当前显示分别记录；失败不会改写已显示资源。</summary>
    public readonly struct ResourceSourceSnapshot
    {
        public ResourceSourceSnapshot(string requestedKey, string displayedKey,
            ResourceSourceState state, Exception failure, bool isFrozen)
            : this(requestedKey, displayedKey, state, failure, isFrozen, null, 0)
        {
        }

        public ResourceSourceSnapshot(string requestedKey, string displayedKey,
            ResourceSourceState state, Exception failure, bool isFrozen, Exception cleanupFailure, long cleanupFailureCount)
        {
            if (cleanupFailureCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cleanupFailureCount));
            }

            RequestedKey = requestedKey;
            DisplayedKey = displayedKey;
            State = state;
            Failure = failure;
            IsFrozen = isFrozen;
            CleanupFailure = cleanupFailure;
            CleanupFailureCount = cleanupFailureCount;
        }

        public string RequestedKey
        {
            get;
        }

        public string DisplayedKey
        {
            get;
        }

        public ResourceSourceState State
        {
            get;
        }

        public Exception Failure
        {
            get;
        }

        public bool IsFrozen
        {
            get;
        }

        /// <summary>首个归还或回滚错误；独立于当前请求的加载结果，不撤销已提交显示。</summary>
        public Exception CleanupFailure
        {
            get;
        }

        /// <summary>归还、回滚及最终目标清空失败的累计次数，达到 long.MaxValue 后保持饱和。</summary>
        public long CleanupFailureCount
        {
            get;
        }
    }
}
