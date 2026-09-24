namespace MUI
{
    /// <summary>页面与子视图共用的业务接入点，生命周期由框架实例驱动器调用。</summary>
    public abstract class Presenter<TViewModel, TArgs, TResult>
        where TViewModel : ViewModel
    {
        protected TViewModel ViewModel
        {
            get; private set;
        }

        protected Lifetime InstanceLifetime
        {
            get; private set;
        }

        protected ActivationContext<TArgs, TResult> Context
        {
            get; private set;
        }

        protected virtual void OnCreate()
        {
        }

        protected virtual void OnOpen(TArgs args)
        {
        }

        protected virtual void OnClose()
        {
        }

        protected virtual void OnDestroy()
        {
        }

        /// <summary>
        /// 显式换绑时撤销 previous 的订阅并连接当前 ViewModel，不重复打开或关闭。
        /// 失败恢复也会反向调用此钩子，必须能够清理部分建立的订阅。
        /// 此钩子不能等待当前视图关闭，也不能承诺回滚业务服务的外部副作用。
        /// </summary>
        protected virtual void OnViewModelChanged(TViewModel previous)
        {
        }

        internal void ChangeViewModel(TViewModel model)
        {
            var previous = ViewModel;
            ViewModel = model;
            OnViewModelChanged(previous);
        }

        protected virtual void OnFocus()
        {
        }

        protected virtual void OnUnfocus()
        {
        }

        protected virtual void OnCovered()
        {
        }

        protected virtual void OnRevealed()
        {
        }

        internal void Create(TViewModel model, Lifetime lifetime)
        {
            ViewModel = model;
            InstanceLifetime = lifetime;
            OnCreate();
        }

        internal void Open(ActivationContext<TArgs, TResult> context)
        {
            Context = context;
            OnOpen(context.Args);
        }

        internal void Close()
        {
            try
            {
                OnClose();
            }
            finally
            {
                Context = null;
            }
        }

        /// <summary>由宿主的显式参数事务更新上下文，不重新执行打开或关闭回调。</summary>
        internal void SetArgs(TArgs args)
        {
            if (Context == null)
            {
                throw new System.InvalidOperationException("Presenter has no active context for updating arguments.");
            }

            Context.SetArgs(args);
        }

        internal void Destroy()
        {
            try
            {
                OnDestroy();
            }
            finally
            {
                Context = null;
                ViewModel = null;
                InstanceLifetime = null;
            }
        }

        internal void Focus(bool focused)
        {
            if (focused)
            {
                OnFocus();
            }
            else
            {
                OnUnfocus();
            }
        }

        internal void Cover(bool covered)
        {
            if (covered)
            {
                OnCovered();
            }
            else
            {
                OnRevealed();
            }
        }
    }
}
