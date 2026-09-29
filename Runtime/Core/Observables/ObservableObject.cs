using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MUI
{
    /// <summary>表现状态基类，UI 状态必须在 UI 线程修改。</summary>
    public abstract partial class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
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
            if (notificationDepth != 0 || flushingNotifications)
            {
                QueueNotification(propertyName);
                return;
            }

            PropertyChanged?.Invoke(this, PropertyChangedEventArgsCache.Get(propertyName));
        }
    }
}
