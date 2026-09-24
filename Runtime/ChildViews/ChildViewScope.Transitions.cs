using System;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewScope
    {
        /// <summary>同步停止激活并保留实例；返回时旧绑定、子激活和激活资源均已完成清理。</summary>
        public void Deactivate(ChildViewHandle handle)
        {
            RunSynchronousTransition(handle, false);
        }

        /// <summary>同步复用原实例准备新激活，成功到 Prepared；仍需显式 Commit 才能显示。</summary>
        public ChildViewHandle PrepareReactivation(ChildViewHandle handle)
        {
            return RunSynchronousTransition(handle, true);
        }

        private ChildViewHandle RunSynchronousTransition(ChildViewHandle handle, bool reactivate)
        {
            ValidateTransition(handle, reactivate ? ChildViewState.Inactive : ChildViewState.Active, ChildViewState.Retained);
            if (Mode != LifetimeMode.Synchronous || !handle.CanCloseSynchronously)
            {
                throw new InvalidOperationException("Synchronous child transitions require a synchronous scope and idle child cleanup.");
            }

            return owner.Run(_ => operations.Run(__ =>
            {
                try
                {
                    if (reactivate)
                    {
                        handle.PrepareReactivationCore();
                    }
                    else
                    {
                        handle.DeactivateCore();
                    }

                    RequireActive();
                    if (reactivate)
                    {
                        handle.SetParentState(visible, interactable);
                        RequireActive();
                        if (!handle.IsContentCurrent)
                        {
                            throw new OperationCanceledException("Child content changed before synchronous reactivation returned.");
                        }
                    }

                    if (handle.State != (reactivate ? ChildViewState.Prepared : ChildViewState.Inactive))
                    {
                        throw new OperationCanceledException("Child view closed before its synchronous transition returned.");
                    }

                    return handle;
                }
                catch (Exception failure)
                {
                    // 旧激活已退役；过渡失败后关闭同一实例，不能把半清理的内容交给缓存。
                    try
                    {
                        handle.Dispose();
                    }
                    catch (Exception cleanup)
                    {
                        throw new AggregateException("Synchronous child transition and final cleanup failed.", failure, cleanup);
                    }

                    throw;
                }
            }));
        }

        private void ValidateTransition(ChildViewHandle handle, ChildViewState first, ChildViewState second)
        {
            RequireActive();
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            if (!handle.BelongsTo(this) || (handle.State != first && handle.State != second)
                || handle.IsExecuting || HasVisualRetention)
            {
                throw new InvalidOperationException("Child transition requires this scope's idle handle in a compatible state.");
            }

            foreach (var sibling in handles)
            {
                if (sibling.IsLifecycleExecuting)
                {
                    throw new InvalidOperationException("Cannot transition a child view from a sibling lifecycle callback.");
                }
            }
        }
    }
}
