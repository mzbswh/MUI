using System;

namespace MUI.Notifications
{
    /// <summary>单个展示位的不可变状态；不持有队列或控件的所有权。</summary>
    public readonly struct NotificationSnapshot
    {
        /// <summary>创建快照；有通知时 ID 必须为正，无通知时 ID 必须为零，等待数量不能为负。</summary>
        public NotificationSnapshot(long id, Notification current, int pendingCount = 0)
        {
            if (pendingCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pendingCount));
            }

            if (current == null ? id != 0 : id <= 0)
            {
                throw new ArgumentException("A visible notification requires a positive ID; an empty snapshot requires ID zero.", nameof(id));
            }

            Id = id;
            Current = current;
            PendingCount = pendingCount;
        }

        /// <summary>当前通知身份；空展示位为零。</summary>
        public long Id
        {
            get;
        }

        /// <summary>当前通知；没有展示项时为 null。</summary>
        public Notification Current
        {
            get;
        }

        /// <summary>等待项数量，不包含展示项。</summary>
        public int PendingCount
        {
            get;
        }

        /// <summary>是否存在当前通知。</summary>
        public bool IsVisible => Current != null;
    }
}
