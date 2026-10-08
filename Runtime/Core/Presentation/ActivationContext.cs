using System;
using System.Threading;

namespace MUI
{
    /// <summary>一次激活的类型化数据与所有权，不能跨缓存激活复用。</summary>
    public sealed class ActivationContext<TArgs, TResult>
    {
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly ICommandTarget target;
        private readonly IInputView inputView;
        private ActivationInputBlockers inputBlockers;

        internal ActivationContext(TArgs args, LifetimeScope lifetime, ICommandTarget target, IInputView inputView = null)
        {
            Args = args;
            Scope = lifetime;
            this.target = target;
            this.inputView = inputView;
        }

        public TArgs Args
        {
            get; private set;
        }

        public LifetimeScope Scope
        {
            get;
        }

        public CancellationToken Token => Scope.Token;

        public int InputBlockerCount => inputBlockers == null ? 0 : inputBlockers.Count;

        internal void SetArgs(TArgs args) => Args = args;

        public void Apply(Action update)
        {
            if (update == null)
            {
                throw new ArgumentNullException(nameof(update));
            }

            RequireValid();
            update();
        }

        /// <summary>阻挡本投影输入直到释放，激活清理也会释放此阻挡。</summary>
        public IDisposable BlockInput(string reason)
        {
            RequireValid();
            if (inputView == null)
            {
                throw new NotSupportedException("This renderer does not support input blockers.");
            }

            if (inputBlockers == null)
            {
                inputBlockers = new ActivationInputBlockers(Scope);
            }

            return inputBlockers.Block(inputView.InputGate, reason);
        }

        /// <summary>只允许本次激活请求关闭；旧上下文不能关闭重新激活后的同一实例。</summary>
        public void RequestClose()
        {
            RequireValid();
            target.RequestClose();
        }

        /// <summary>在本次激活仍有效时提交结果。</summary>
        public void Complete(TResult result)
        {
            RequireValid();
            target.Complete(result);
        }

        private void RequireValid()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("Activation context requires its creating UI thread.");
            }

            Token.ThrowIfCancellationRequested();
        }
    }
}
