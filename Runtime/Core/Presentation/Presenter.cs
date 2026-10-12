namespace MUI
{
    /// <summary>页面与子视图共用的业务接入点，生命周期由框架实例驱动器调用。</summary>
    public abstract class Presenter<TViewModel, TArgs, TResult>
        where TViewModel : ViewModel
    {
        /// <summary>当前表现模型的简写；ViewModel 保留兼容。</summary>
        protected TViewModel VM => ViewModel;

        protected TViewModel ViewModel
        {
            get; private set;
        }

        protected LifetimeScope InstanceLifetime
        {
            get; private set;
        }

        protected ActivationContext<TArgs, TResult> Context
        {
            get; private set;
        }

        /// <summary>当前 Presenter 的命令契约，由生成器提供；不包含 ViewModel 自身的绑定。</summary>
        public virtual BindingManifest CommandManifest => null;

        /// <summary>显式启用更新；每个实例创建时读取一次，默认禁用。</summary>
        protected virtual UpdateOptions UpdateOptions => MUI.UpdateOptions.Disabled;

        /// <summary>由生成器将实例命令加入当前绑定会话，沿用其取消、预览和解绑协议。</summary>
        protected virtual void BuildCommandBindings(BindingBuilder<TViewModel> builder)
        {
        }

        internal void AttachCommandBindings(BindingContext binding)
        {
            if (CommandManifest == null || CommandManifest.Entries.Count == 0)
            {
                return;
            }

            if (!(binding is BindingContext<TViewModel> typed))
            {
                throw new System.InvalidOperationException("Presenter commands require BindingContext<TViewModel>.");
            }

            typed.SetPresenterCommandBindings(BuildCommandBindings);
        }

        protected virtual void OnCreate()
        {
        }

        /// <summary>打开前的同步准备；此时尚未提交显示。需要异步准备时实现 IAsyncOpenPreparation。</summary>
        protected virtual void OnPrepareOpen(TArgs args)
        {
        }

        /// <summary>
        /// 本次激活提交并首次显示后调用；页面还会等待进入效果结束。
        /// 每次激活调用一次，单纯隐藏后恢复不会重复调用。此回调不等待异步工作。
        /// </summary>
        protected virtual void OnOpened()
        {
        }

        internal void Opened() => OnOpened();

        protected virtual void OnUpdate(UpdateTime time)
        {
        }

        internal UpdateOptions GetUpdateOptions() => UpdateOptions;

        internal void Update(UpdateTime time) => OnUpdate(time);

        protected virtual void OnClose()
        {
        }

        protected virtual void OnDestroy()
        {
        }

        /// <summary>
        /// 显式换绑时撤销 previous 的订阅并连接当前 ViewModel，不重复打开或关闭。
        /// 提交失败由宿主故障关闭，钩子仍应能清理部分建立的订阅。
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

        internal void Create(TViewModel model, LifetimeScope lifetime)
        {
            ViewModel = model;
            InstanceLifetime = lifetime;
            OnCreate();
        }

        internal void PrepareOpen(ActivationContext<TArgs, TResult> context)
        {
            Context = context;
            OnPrepareOpen(context.Args);
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
