using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace MUI
{
    public abstract partial class ObservableObject
    {
        private int notificationDepth;
        private bool flushingNotifications;
        private List<string> pendingNotifications;
        private HashSet<string> pendingNames;

        /// <summary>合并属性通知直到最后一个作用域结束，属性值立即改变。</summary>
        public IDisposable DeferNotifications()
        {
            RequireOwningThread();
            ++notificationDepth;
            return new NotificationScope(this);
        }

        private void QueueNotification(string name)
        {
            if (pendingNotifications == null)
            {
                pendingNotifications = new List<string>();
                pendingNames = new HashSet<string>(StringComparer.Ordinal);
            }

            if (pendingNames.Contains(string.Empty))
            {
                return;
            }

            if (string.IsNullOrEmpty(name))
            {
                pendingNames.Clear();
                pendingNotifications.Clear();
                name = string.Empty;
            }

            if (pendingNames.Add(name))
            {
                pendingNotifications.Add(name);
            }
        }

        /// <summary>即使仍在作用域内也立即发布待处理通知，重入刷新加入当前排空流程。</summary>
        public void FlushNotifications()
        {
            RequireOwningThread();
            if (flushingNotifications || pendingNotifications == null || pendingNotifications.Count == 0)
            {
                return;
            }

            flushingNotifications = true;
            var errors = new List<Exception>();
            try
            {
                var passes = 0;
                while (pendingNotifications.Count != 0)
                {
                    if (++passes > 32)
                    {
                        pendingNotifications.Clear();
                        pendingNames.Clear();
                        errors.Add(new InvalidOperationException("Property notification batch did not stabilize after 32 passes."));
                        break;
                    }

                    var batch = pendingNotifications.ToArray();
                    pendingNotifications.Clear();
                    pendingNames.Clear();
                    foreach (var name in batch)
                    {
                        var handlers = PropertyChanged;
                        if (handlers == null)
                        {
                            continue;
                        }

                        var args = PropertyChangedEventArgsCache.Get(name);
                        foreach (PropertyChangedEventHandler handler in handlers.GetInvocationList())
                        {
                            try
                            {
                                handler(this, args);
                            }
                            catch (Exception error)
                            {
                                errors.Add(error);
                            }
                        }
                    }
                }
            }
            finally
            {
                flushingNotifications = false;
            }

            if (errors.Count != 0)
            {
                throw new AggregateException("Property notification batch failed.", errors);
            }
        }

        private sealed class NotificationScope : IDisposable
        {
            private ObservableObject owner;

            public NotificationScope(ObservableObject owner)
            {
                this.owner = owner;
            }

            public void Dispose()
            {
                var current = owner;
                if (current == null)
                {
                    return;
                }

                current.RequireOwningThread();
                owner = null;
                if (--current.notificationDepth == 0)
                {
                    current.FlushNotifications();
                }
            }
        }
    }
}
