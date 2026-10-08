using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace MUI
{
    /// <summary>表现状态基类，UI 状态必须在 UI 线程修改。</summary>
    public abstract partial class ObservableObject : INotifyPropertyChanged
    {
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;

        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            RequireOwningThread();
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>用于观察者异常不能打断清理的基础设施状态。</summary>
        protected void OnPropertyChangedSafely(string propertyName, Action<Exception> reportError)
        {
            RequireOwningThread();
            if (reportError == null)
            {
                throw new ArgumentNullException(nameof(reportError));
            }

            var handlers = PropertyChanged;
            if (handlers == null)
            {
                return;
            }

            var args = PropertyChangedEventArgsCache.Get(propertyName);
            foreach (PropertyChangedEventHandler handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this, args);
                }
                catch (Exception error)
                {
                    reportError(error);
                }
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            RequireOwningThread();
            if (notificationDepth != 0 || flushingNotifications)
            {
                QueueNotification(propertyName);
                return;
            }

            PropertyChanged?.Invoke(this, PropertyChangedEventArgsCache.Get(propertyName));
        }

        /// <summary>自定义属性 setter 应在修改字段前调用。</summary>
        protected void RequireOwningThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != threadId)
            {
                throw new InvalidOperationException("ObservableObject must be updated on its owning UI thread.");
            }
        }
    }
}
