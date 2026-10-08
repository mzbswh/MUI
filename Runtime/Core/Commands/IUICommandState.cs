using System;
using System.ComponentModel;

namespace MUI
{
    /// <summary>同步与异步命令共用的可观察状态，不规定执行方式。</summary>
    public interface IUICommandState : INotifyPropertyChanged
    {
        bool CanExecute
        {
            get;
        }

        bool IsExecuting
        {
            get;
        }

        Exception Error
        {
            get;
        }

        void NotifyCanExecuteChanged();

        void Cancel();
    }
}
