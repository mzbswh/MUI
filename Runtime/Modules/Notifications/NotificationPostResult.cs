namespace MUI.Notifications
{
    /// <summary>入队结果；重复项保留原请求，满额与释放状态不分配 ID。</summary>
    public enum NotificationPostStatus
    {
        Accepted,
        Duplicate,
        CapacityExceeded,
        Disposed
    }

    /// <summary>一次提交结果，不代表通知已经展示或仍然存活。</summary>
    public readonly struct NotificationPostResult
    {
        internal NotificationPostResult(NotificationPostStatus status, long id)
        {
            Status = status;
            Id = id;
        }

        /// <summary>本次入队的处理结果。</summary>
        public NotificationPostStatus Status
        {
            get;
        }

        /// <summary>接受的新项或命中的重复项 ID；零表示没有预留条目。</summary>
        public long Id
        {
            get;
        }
    }
}
