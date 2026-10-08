using System;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewHandle<TViewModel, TArgs>
        where TViewModel : ViewModel
    {
        internal override void EndRetainedDisplay()
        {
            Owner.RequireThread();
            if (State == ChildViewState.Retained && view is IVisualRetentionView retained)
            {
                // 新内容显示前先撤销旧画面；原激活已停用，底层门控保持隐藏。
                Invoke(retained.EndVisualRetention);
            }
        }

        public override void RetainAndDeactivate()
        {
            Owner.RequireActive();
            if (State == ChildViewState.Retained)
            {
                return;
            }

            if (State != ChildViewState.Active || !bindingsCommitted || committing ||
                IsLifecycleExecuting || Owner.HasVisualRetention)
            {
                throw new InvalidOperationException("Only a stable active childView can retain its visuals.");
            }

            if (!(view is IVisualRetentionView retained) || !(binding is IFreezableBindingContext))
            {
                throw new InvalidOperationException("Retained childViews require a retaining View and freezable bindings.");
            }

            // 先撤销业务资格，后调用渲染器和取消回调，避免重入发起新命令。
            State = ChildViewState.Retained;
            try
            {
                Owner.RefreshTicks();
                Invoke(retained.BeginVisualRetention);
                Owner.RequireActive();
                if (State != ChildViewState.Retained)
                {
                    throw new OperationCanceledException("ChildView closed during visual retention.");
                }

                StopForParentClose();
                Owner.RequireActive();
                if (State != ChildViewState.Retained)
                {
                    throw new OperationCanceledException("ChildView closed while deactivating retained content.");
                }

                Owner.RefreshTicks();
            }
            catch (Exception failure)
            {
                // 冻结可能已经不可逆，失败时不能把旧状态改回 Active。
                // 仍走同一句柄的关闭路径，禁止转移或复制已有资源凭证。
                earlyErrors.Add(failure);
                BeginCloseAndReport();
                throw;
            }
        }
    }
}
