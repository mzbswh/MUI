using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>宿主仅提供实例资格、参数存储与回调保护，公共执行器负责候选事务及输入收尾。</summary>
    internal interface IArgsUpdateHost<TArgs>
    {
        TArgs Args
        {
            get;
        }

        IArgsUpdatePresenter<TArgs> ArgsUpdater
        {
            get;
        }

        IInputView ArgsInput
        {
            get;
        }

        CancellationToken ArgsLifetimeToken
        {
            get;
        }

        bool CanUpdateArgs
        {
            get;
        }

        void RequireArgsThread();

        void SetArgs(TArgs args);

        void ArgsCommitted();

        void InvokeArgsCallback(Action callback);

        ValueTask InvokeArgsCallbackAsync(Func<ValueTask> callback);

        void ArgsFaulted(Exception error);

        /// <summary>完成信号发布前记录清理结果；只更新内部状态，不调用外部代码。</summary>
        void RecordArgsUpdateOutcome(ArgsUpdateOutcome outcome);
    }
}
