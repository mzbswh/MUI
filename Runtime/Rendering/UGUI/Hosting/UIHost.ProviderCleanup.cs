using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using MUI.Navigation;

namespace MUI.UGUI
{
    public sealed partial class UIHost
    {
        /// <summary>独立于原生宿主组件的提供方持有权；恢复只确认依赖，不重放未知项目回调。</summary>
        private sealed class HostProviderCleanup
        {
            private Navigator navigator;
            private object provider;
            private Func<ValueTask> release;
            private CleanupResponsibility providerResponsibility;
            private Exception registrationFailure;
            private Task navigationCompletion;
            private bool releaseStarted;
            private bool releaseSucceeded;

            internal HostProviderCleanup(Navigator navigator, IAsyncDisposable asynchronous, IDisposable synchronous)
            {
                this.navigator = navigator;
                provider = (object)asynchronous ?? synchronous;
                if (asynchronous != null)
                {
                    release = asynchronous.DisposeAsync;
                }
                else
                {
                    release = () =>
                    {
                        synchronous.Dispose();
                        return default;
                    };
                }
                var hostId = navigator.CaptureSnapshot(1, 1).HostId;
                using (UIErrors.BeginContext(new UIErrorContext(hostId, 0, null, "UIHost.Shutdown", "ProviderRelease")))
                {
                    Responsibility = new CleanupResponsibility(ReleaseAsync, "UIHost.Provider", true,
                        Thread.CurrentThread.ManagedThreadId);
                }
            }

            internal CleanupResponsibility Responsibility
            {
                get;
            }

            internal bool IsConfirmed => Responsibility.CaptureSnapshot().State == CleanupResponsibilityState.Completed;

            internal ValueTask DisposeAsync(Task navigationCompletion)
            {
                this.navigationCompletion = navigationCompletion;
                using (UIErrors.BeginContext(Responsibility.CaptureSnapshot().Context))
                {
                    return Responsibility.DisposeAsync();
                }
            }

            private async ValueTask ReleaseAsync()
            {
                UnityMainThread.Require();
                if (!releaseStarted)
                {
                    try
                    {
                        await navigationCompletion;
                    }
                    catch (Exception)
                    {
                        // 导航首次错误由宿主结果保留；当前责任判断决定提供方是否仍被占用。
                        if (!navigator.IsShutdown)
                        {
                            throw;
                        }
                    }
                    if (navigator.HasOutstandingProviderUse(Responsibility.Id))
                    {
                        throw new InvalidOperationException("UIHost retained its provider until navigation resources are confirmed returned.");
                    }
                    CaptureProviderResponsibility();
                    releaseStarted = true;
                    try
                    {
                        await release();
                        releaseSucceeded = true;
                        release = null;
                    }
                    catch (Exception error)
                    {
                        if (registrationFailure != null)
                        {
                            throw new AggregateException("Provider cleanup registration and release failed.", registrationFailure, error);
                        }
                        throw;
                    }
                    if (registrationFailure != null)
                    {
                        ExceptionDispatchInfo.Capture(registrationFailure).Throw();
                    }
                }
                else if (!releaseSucceeded && providerResponsibility.CaptureSnapshot().State != CleanupResponsibilityState.Completed)
                {
                    throw new InvalidOperationException("UIHost provider cleanup still has an unconfirmed release responsibility.");
                }
                // 依赖与提供方均已确认；解除引用不改写首次 Shutdown 或释放任务的结果。
                provider = null;
                release = null;
                navigator = null;
                navigationCompletion = null;
                providerResponsibility = null;
                registrationFailure = null;
            }

            private void CaptureProviderResponsibility()
            {
                if (!(provider is ICleanupResponsibilitySource source))
                {
                    providerResponsibility = new CleanupResponsibility(release, "UIHost.ProviderRelease",
                        releaseThreadId: Thread.CurrentThread.ManagedThreadId);
                    release = providerResponsibility.DisposeAsync;
                    return;
                }
                try
                {
                    providerResponsibility = source.CleanupResponsibility;
                    if (providerResponsibility == null)
                    {
                        throw new InvalidOperationException("Owned provider returned no cleanup responsibility.");
                    }
                }
                catch (Exception error)
                {
                    registrationFailure = error;
                    providerResponsibility = new CleanupResponsibility(release, "UIHost.ProviderRelease",
                        releaseThreadId: Thread.CurrentThread.ManagedThreadId);
                    release = providerResponsibility.DisposeAsync;
                }
            }
        }
    }
}
