using System;
using System.Collections.Generic;
using System.Threading;

namespace MUI.Notifications
{
    /// <summary>
    /// 所属 UI 线程上的单条展示队列，按优先级调度等待项，不抢占当前通知。
    /// 仅管理通知状态；时钟由调用方推进，不拥有导航、控件或业务任务。
    /// </summary>
    public sealed class NotificationQueue : INotificationSource, IDisposable
    {
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly int capacity;
        private readonly List<Entry> pending = new List<Entry>();
        private Entry current;
        private long nextId;
        private bool disposed;
        private bool publishing;
        private bool publishPending;
        private long version;

        /// <param name="capacity">展示项与等待项的合计上限；满额时拒绝新请求。</param>
        public NotificationQueue(int capacity = 32)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            this.capacity = capacity;
        }

        /// <summary>当前通知或等待数量变化时发布完整状态；观察者异常被隔离。</summary>
        public event Action<NotificationSnapshot> Changed;

        /// <summary>无副作用地读取当前状态；队列释放后仍可读取空状态。</summary>
        public NotificationSnapshot Snapshot
        {
            get
            {
                RequireThread();
                return Capture();
            }
        }

        /// <summary>
        /// 尝试入队。去重覆盖展示项与等待项，命中时返回原 ID，不修改文案、优先级或时长。
        /// Accepted 表示已完成入队提交；观察者可能在返回前关闭该项，不保证返回时仍可见。
        /// </summary>
        public NotificationPostResult Post(Notification notification)
        {
            RequireThread();
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            if (disposed)
            {
                return new NotificationPostResult(NotificationPostStatus.Disposed, 0);
            }

            if (notification.Key != null)
            {
                if (current != null && SameKey(current, notification.Key))
                {
                    return new NotificationPostResult(NotificationPostStatus.Duplicate, current.Id);
                }

                foreach (var entry in pending)
                {
                    if (SameKey(entry, notification.Key))
                    {
                        return new NotificationPostResult(NotificationPostStatus.Duplicate, entry.Id);
                    }
                }
            }

            if (pending.Count + (current == null ? 0 : 1) >= capacity)
            {
                return new NotificationPostResult(NotificationPostStatus.CapacityExceeded, 0);
            }

            if (nextId == long.MaxValue)
            {
                throw new InvalidOperationException("Notification IDs exhausted.");
            }

            var candidate = new Entry
            {
                Id = ++nextId,
                Value = notification,
                ExpiresIn = notification.ExpiresAfter,
                DisplayRemaining = notification.DisplayDuration
            };
            pending.Add(candidate);
            Promote();
            Publish();
            // 观察者可在通知中关闭候选项；返回值描述已完成的入队事实。
            return new NotificationPostResult(NotificationPostStatus.Accepted, candidate.Id);
        }

        /// <summary>按 ID 移除展示项或等待项；迟到的关闭请求不能误关后来的通知。</summary>
        public bool Dismiss(long id)
        {
            RequireThread();
            if (disposed || id <= 0)
            {
                return false;
            }

            if (current != null && current.Id == id)
            {
                current = null;
                Promote();
                Publish();
                return true;
            }

            for (var i = 0; i < pending.Count; ++i)
            {
                if (pending[i].Id != id)
                {
                    continue;
                }

                pending.RemoveAt(i);
                Publish();
                return true;
            }

            return false;
        }

        /// <summary>每帧使用非缩放秒数推进一次。先扣除等待项的总有效期，再更新当前项并提升下一项。
        /// 新提升项从本帧开始计算展示时长，不消耗本帧剩余时间。</summary>
        public void Advance(double unscaledDeltaTime)
        {
            RequireThread();
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(NotificationQueue));
            }

            if (double.IsNaN(unscaledDeltaTime) || double.IsInfinity(unscaledDeltaTime) || unscaledDeltaTime < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
            }

            if (unscaledDeltaTime == 0)
            {
                return;
            }

            var changed = false;
            for (var i = pending.Count - 1; i >= 0; --i)
            {
                pending[i].ExpiresIn = Remaining(pending[i].ExpiresIn, unscaledDeltaTime);
                if (pending[i].ExpiresIn > 0)
                {
                    continue;
                }

                pending.RemoveAt(i);
                changed = true;
            }

            if (current != null)
            {
                current.ExpiresIn = Remaining(current.ExpiresIn, unscaledDeltaTime);
                current.DisplayRemaining = Remaining(current.DisplayRemaining, unscaledDeltaTime);
                if (current.ExpiresIn == 0 || current.DisplayRemaining == 0)
                {
                    current = null;
                    changed = true;
                }
            }

            Promote();
            if (changed)
            {
                Publish();
            }
        }

        /// <summary>清空展示项和等待项并通知观察者；保留队列可用性与递增 ID。</summary>
        public void Clear()
        {
            RequireThread();
            if (disposed || (current == null && pending.Count == 0))
            {
                return;
            }

            current = null;
            pending.Clear();
            Publish();
        }

        /// <summary>幂等清空并结束队列，发布空状态后释放观察者；之后 Post 返回 Disposed。</summary>
        public void Dispose()
        {
            RequireThread();
            if (disposed)
            {
                return;
            }

            disposed = true;
            current = null;
            pending.Clear();
            Publish();
        }

        // 只在展示位空闲时提升；使用严格大于比较，保留同优先级的入队顺序。
        private void Promote()
        {
            if (current != null || pending.Count == 0)
            {
                return;
            }

            var best = 0;
            for (var i = 1; i < pending.Count; ++i)
            {
                if (pending[i].Value.Priority > pending[best].Value.Priority)
                {
                    best = i;
                }
            }

            current = pending[best];
            pending.RemoveAt(best);
        }

        private NotificationSnapshot Capture()
        {
            return new NotificationSnapshot(
                current == null ? 0 : current.Id,
                current == null ? null : current.Value,
                pending.Count);
        }

        // 回调重入会递增代际，停止向剩余观察者发送旧快照，再发布最新状态。
        private void Publish()
        {
            ++version;
            publishPending = true;
            if (publishing)
            {
                return;
            }

            publishing = true;
            try
            {
                var passes = 0;
                while (publishPending)
                {
                    if (++passes > 32)
                    {
                        UIErrors.Report(new InvalidOperationException("Notification state did not stabilize after 32 publications."));
                        break;
                    }

                    publishPending = false;
                    var currentVersion = version;
                    var snapshot = Capture();
                    var handlers = Changed;
                    if (handlers == null)
                    {
                        continue;
                    }

                    foreach (Action<NotificationSnapshot> handler in handlers.GetInvocationList())
                    {
                        if (version != currentVersion)
                        {
                            break;
                        }

                        try
                        {
                            handler(snapshot);
                        }
                        catch (Exception error)
                        {
                            UIErrors.Report(error);
                        }
                    }
                }
            }
            finally
            {
                publishing = false;
                publishPending = false;
                if (disposed)
                {
                    Changed = null;
                }
            }
        }

        private static bool SameKey(Entry entry, string key) => string.Equals(entry.Value.Key, key, StringComparison.Ordinal);

        private static double Remaining(double value, double delta) => delta >= value ? 0 : value - delta;

        private void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("Notification queue requires its owning UI thread.");
            }
        }

        private sealed class Entry
        {
            public long Id;
            public Notification Value;
            public double ExpiresIn;
            public double DisplayRemaining;
        }
    }
}
