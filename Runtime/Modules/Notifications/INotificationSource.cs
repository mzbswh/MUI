using System;

namespace MUI.Notifications
{
    /// <summary>
    /// UI 线程上的单条通知表现来源，实现方负责调度、发布和时钟。
    /// 旧 UI 仍可能引用的 ID 不能复用；Snapshot 读取无副作用，事件访问器仅订阅或退订。
    /// 状态发布必须隔离观察者异常。
    /// </summary>
    public interface INotificationSource
    {
        /// <summary>在所属 UI 线程发布完整的新状态。</summary>
        event Action<NotificationSnapshot> Changed;

        /// <summary>当前展示位及等待数量。</summary>
        NotificationSnapshot Snapshot
        {
            get;
        }

        /// <summary>只关闭匹配 ID 的通知；不存在、已过期或不允许关闭时返回 false。</summary>
        bool Dismiss(long id);
    }
}
