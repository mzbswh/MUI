using System;

namespace MUI
{
    public enum PageLoadStatus
    {
        Loaded,
        EndReached,
        Failed,
        WaitCancelled,
        Inactive,
        /// <summary>旧页被查询重置取消，未提交到列表。</summary>
        Superseded,
        /// <summary>查询重置正在等待旧加载退出，暂不启动新页。</summary>
        Resetting
    }

    /// <summary>一次分页等待的结果；等待取消不会取消其他调用者共享的加载。</summary>
    public readonly struct PageLoadResult
    {
        public PageLoadResult(PageLoadStatus status, int addedCount = 0, Exception error = null, int evictedCount = 0)
        {
            if (!Enum.IsDefined(typeof(PageLoadStatus), status) || addedCount < 0 ||
                evictedCount < 0 || (status != PageLoadStatus.Loaded && (addedCount != 0 || evictedCount != 0)))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            Status = status;
            AddedCount = addedCount;
            Error = error;
            EvictedCount = evictedCount;
        }

        public PageLoadStatus Status
        {
            get;
        }

        public int AddedCount
        {
            get;
        }

        /// <summary>本次提交从相反一端移出的条目数，条目本身仍属于项目业务层。</summary>
        public int EvictedCount
        {
            get;
        }

        public Exception Error
        {
            get;
        }
    }
}
