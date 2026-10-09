using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.UGUI
{
    public sealed partial class View
    {
        private LifetimeScope cleanupScope;
        private CleanupResponsibility cleanupResponsibility;
        private CleanupResponsibility inputCleanup;
        private Exception cleanupFailure;
        private Func<bool> activationCleanupConfirmed;

        /// <summary>最终视图责任；恢复只确认已登记依赖，不重放未知控件或原生回调。</summary>
        public CleanupResponsibility CleanupResponsibility
        {
            get
            {
                UnityMainThread.Require();
                return cleanupResponsibility ?? (cleanupResponsibility = new CleanupResponsibility(
                    ReleaseViewAsync, "View.Cleanup", true, Thread.CurrentThread.ManagedThreadId));
            }
        }

        /// <summary>原生同步兜底只观察已完成结果，不阻塞主线程，也不重新执行首次失败的清理。</summary>
        public void Dispose()
        {
            var disposal = DisposeAsync();
            if (!disposal.IsCompleted)
            {
                throw new InvalidOperationException("View cleanup is still pending; observe DisposeAsync instead.");
            }
            disposal.GetAwaiter().GetResult();
        }

        /// <summary>最终清理的共享首次结果；原生销毁由节点拥有者在责任确认后执行。</summary>
        public ValueTask DisposeAsync()
        {
            UnityMainThread.Require();
            return CleanupResponsibility.DisposeAsync();
        }

        private LifetimeScope GetCleanupScope() => cleanupScope ?? (cleanupScope = new LifetimeScope());

        private async ValueTask ReleaseViewAsync()
        {
            UnityMainThread.Require();
            if (!disposed)
            {
                disposed = true;
                RegisterFinalCleanup();
                try
                {
                    await inputCleanup.DisposeAsync();
                }
                catch (Exception error)
                {
                    cleanupFailure = error;
                }
            }

            // 激活由 ViewInstance 或项目持有。最终控件清理不能先解除在途资源仍使用的原生引用。
            if (nativeDestroyed && !activationCleanupConfirmed())
            {
                var completion = activeActivation == null ? null : activeActivation.DisposalCompletion;
                if (completion != null)
                {
                    // 原生销毁兜底只等待拥有者已启动的清理；不提前释放激活，也不重试历史失败。
                    await completion;
                }
            }
            if (!activationCleanupConfirmed())
            {
                throw cleanupFailure ?? new InvalidOperationException("View is retained until its activation cleanup is confirmed.");
            }

            var recovering = cleanupScope.IsDisposed && cleanupScope.IsCleanupConfirmed;
            try
            {
                await cleanupScope.DisposeAsync();
            }
            catch (Exception error)
            {
                if (!recovering)
                {
                    cleanupFailure = cleanupFailure ?? error;
                    throw;
                }
                // 只有进入本次尝试前已经确认的依赖允许跳过历史失败，首次任务保持原值。
            }
            finally
            {
                if (cleanupScope.IsDisposed)
                {
                    activeResourceContext = null;
                    resourceLoader = null;
                    childViews = null;
                    activeActivation = null;
                    childActivationToken = default;
                    InputStateChanged = null;
                    InputGesturesInvalidated = null;
                    ChildTickActivityChanged = null;
                    nativeGestures.Clear();
                    elements.Clear();
                    index = null;
                }
            }
            cleanupFailure = null;
            activationCleanupConfirmed = null;
        }

        private static async Task ObserveNativeCleanupAsync(ValueTask cleanup)
        {
            try
            {
                await cleanup;
            }
            catch (Exception error)
            {
                UnityErrorLogging.Report(error);
            }
        }

        private void RegisterFinalCleanup()
        {
            var children = childViews;
            var resources = activeResourceContext;
            activationCleanupConfirmed = () => (children == null || children.IsCleanupConfirmed) &&
                (resources == null || resources.Scope.IsCleanupConfirmed);
            var lifetime = GetCleanupScope();
            // 这些登记晚于控件：先结束视图自身的原生状态，再按扫描逆序归还控件。
            lifetime.OnDispose(ResetFocusState);
            lifetime.OnDispose(ReleaseModalBarrier);
            lifetime.OnDispose(() =>
            {
                if (inputGate != null)
                {
                    inputGate.Changed -= ApplyGates;
                    inputGate.Dispose();
                }
            });
            lifetime.OnDispose(EndVisualRetention);
            lifetime.OnDispose(ClearLocalBackHandlers);
            inputCleanup = new CleanupResponsibility(() =>
            {
                InvalidateInputGestures();
                if (children != null)
                {
                    children.TickActivityChanged -= NotifyChildTicks;
                    children.Cancel();
                }
                ApplyGates();
                return default;
            }, "View.InputCleanup", releaseThreadId: Thread.CurrentThread.ManagedThreadId);
            lifetime.Own(inputCleanup);
        }
    }
}
