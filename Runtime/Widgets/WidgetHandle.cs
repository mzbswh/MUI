using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.Widgets
{
    /// <summary>
    /// 父界面内的视图实例句柄，负责局部状态和父级所有权，不拥有导航历史或顶层焦点。
    /// 与顶层页面共用 View、Presenter 和绑定生命周期，不要求业务继承专用的子视图基类。
    /// </summary>
    public abstract class WidgetHandle : IAsyncDisposable, ICommandTarget
    {
        private protected WidgetHandle(WidgetScope owner)
        {
            Owner = owner;
        }

        internal event Action<WidgetHandle> Closed;

        protected WidgetScope Owner
        {
            get;
        }

        public WidgetState State { get; protected set; } = WidgetState.Preparing;

        public bool IsActive => State == WidgetState.Active && Owner.IsActive;

        /// <summary>当前资源与绑定代际是否仍兼容；读取需在所属 UI 线程，提供方错误会抛出。</summary>
        public abstract bool IsContentCurrent
        {
            get;
        }

        public abstract ViewModel Model
        {
            get;
        }

        public abstract ViewResource Resource
        {
            get;
        }

        /// <summary>等待本实例的物理清理完成；逻辑关闭后此任务仍可能未完成。</summary>
        public abstract Task CleanupCompletion
        {
            get;
        }

        /// <summary>当前作用域和 View 取得凭证均已确认归还；首次清理失败任务仍保持原值。</summary>
        public abstract bool IsCleanupConfirmed
        {
            get;
        }

        internal abstract bool IsExecuting
        {
            get;
        }

        internal abstract bool IsLifecycleExecuting
        {
            get;
        }

        internal abstract bool IsTransitioning
        {
            get;
        }

        internal abstract bool LocalVisible
        {
            get;
        }

        internal abstract bool LocalInteractable
        {
            get;
        }

        internal abstract bool WantsTick
        {
            get;
        }

        internal abstract Func<bool> CaptureCleanupConfirmation();

        internal bool BelongsTo(WidgetScope scope) => ReferenceEquals(Owner, scope);

        protected void NotifyClosed()
        {
            var handlers = Closed;
            Closed = null;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<WidgetHandle> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this);
                }
                catch (Exception error)
                {
                    UIErrors.Report(error);
                }
            }
        }

        internal abstract void Tick(float deltaTime, float unscaledDeltaTime);

        internal abstract void CancelWork();

        internal abstract ValueTask PrepareReactivationCoreAsync(CancellationToken token);

        internal abstract ValueTask DeactivateCoreAsync(CancellationToken token);

        internal abstract void StopForParentClose();

        internal abstract IVisualRetentionView GetVisualRetentionView();

        internal abstract void EndRetainedDisplay();

        internal abstract void CommitParentBindings();

        internal abstract void SetParentState(bool visible, bool interactable);

        internal abstract Task BeginClose();

        internal abstract void BeginCloseAndReport();

        /// <summary>将已准备的子视图提交为活动子项；实际可见性仍受父级和局部门控约束。</summary>
        public abstract void Commit();

        /// <summary>设置子项局部可见性与输入限制，不能越过父级门控。</summary>
        public abstract void SetLocalState(bool visible, bool interactable);

        /// <summary>
        /// 停止本次业务激活并保留当前画面，供异步替换期间展示。
        /// 不是暂停：旧激活不会自动恢复，仍须最终关闭并等待资源清理。
        /// </summary>
        public abstract void RetainAndDeactivate();

        /// <summary>开始关闭并等待资源收尾，不能从本子视图自身命令或生命周期回调等待。</summary>
        public abstract ValueTask DisposeAsync();

        /// <summary>不等待完成，可安全地从本子视图自己的命令中调用。</summary>
        public void RequestClose()
        {
            Owner.RequireThread();
            if (!IsActive)
            {
                throw new OperationCanceledException("Widget is not active.");
            }

            BeginCloseAndReport();
        }

        /// <summary>子视图仅接受 Unit 完成值；道具点击等业务结果应通过业务回调传递。</summary>
        public void RequestClose<TResult>(TResult result)
        {
            if (typeof(TResult) != typeof(Unit))
            {
                throw new ArgumentException("Child childView completion accepts Unit only. Use a business callback for item selection.");
            }

            RequestClose();
        }
    }

    /// <summary>子视图的类型化访问契约；生命周期实现由 WidgetScope 创建并持有。</summary>
    public abstract class WidgetHandle<TViewModel, TArgs> : WidgetHandle where TViewModel : ViewModel
    {
        private protected WidgetHandle(WidgetScope owner) : base(owner) { }
        internal abstract ValueTask<PreparedViewRebind> PrepareStagedRebindAsync(TViewModel next, CancellationToken cancellationToken);
        public abstract TViewModel ViewModel
        {
            get;
        }
        public abstract TArgs Args
        {
            get;
        }
        public abstract bool IsUpdatingArgs
        {
            get;
        }
        public abstract bool IsRebinding
        {
            get;
        }
        public abstract bool CanRebind
        {
            get;
        }
        public abstract ValueTask<ArgsUpdateOutcome> UpdateArgsAsync(TArgs args, CancellationToken cancellationToken = default);
        public abstract ValueTask<RebindOutcome> RebindAsync(TViewModel model, CancellationToken cancellationToken = default);
    }
}
