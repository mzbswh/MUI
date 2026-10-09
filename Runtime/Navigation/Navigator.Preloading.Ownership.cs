using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private readonly HashSet<PreloadReservation> preloadReturns = new HashSet<PreloadReservation>();

        // 查询只观察稳定责任；成功的叶重试不重写首次清理结果，也不重放批次回调。
        private int ConfirmedPreloadReturnCount
        {
            get
            {
                var confirmed = 0;
                foreach (var reservation in preloadReturns)
                {
                    if (reservation.IsReturnConfirmed)
                    {
                        ++confirmed;
                    }
                }
                return confirmed;
            }
        }

        private void RefreshPreloadReservations()
        {
            preloadReservations -= preloadReturns.RemoveWhere(reservation => reservation.IsReturnConfirmed);
        }

        /// <summary>凭证到达后持有同一归还责任；作用域直接确认叶责任，不另建未知释放回调。</summary>
        private sealed class PreloadReservation : IAsyncDisposable, ICleanupResponsibilitySource
        {
            private readonly Navigator owner;
            private readonly string label;
            private IAsyncDisposable release;
            private Task firstRelease;
            private CleanupResponsibility cancellationDependency;
            private bool releaseStarted;
            private bool releaseSucceeded;

            internal PreloadReservation(Navigator owner, IAsyncDisposable resource, string label)
            {
                this.owner = owner;
                this.label = label;
                release = resource;
                try
                {
                    CleanupResponsibility = CleanupRegistry.GetResponsibility(resource, label);
                    if (CleanupResponsibility == null)
                    {
                        throw new InvalidOperationException("Preload cleanup adapter returned no responsibility.");
                    }
                }
                catch (Exception error)
                {
                    RegistrationFailure = error;
                    // 属性故障不丢弃凭证，仍通过一次不可安全重试的回调执行实际归还。
                    CleanupResponsibility = new CleanupResponsibility(resource.DisposeAsync, label);
                    release = CleanupResponsibility;
                }
            }

            public CleanupResponsibility CleanupResponsibility
            {
                get;
            }

            internal Exception RegistrationFailure
            {
                get;
            }

            internal bool IsReturnConfirmed => releaseStarted && (releaseSucceeded ||
                CleanupResponsibility.CaptureSnapshot().State == CleanupResponsibilityState.Completed) &&
                (cancellationDependency == null || cancellationDependency.CaptureSnapshot().State == CleanupResponsibilityState.Completed);

            internal void RetainCancellationDependency(CleanupResponsibility dependency) => cancellationDependency = dependency;

            public ValueTask DisposeAsync() => new ValueTask(firstRelease ?? (firstRelease = ReleaseAsync()));

            private async Task ReleaseAsync()
            {
                releaseStarted = true;
                using (UIErrors.BeginPhase("PreloadRelease"))
                {
                    try
                    {
                        await CleanupRegistry.ReleaseAsync(release, label);
                        releaseSucceeded = true;
                        release = null;
                    }
                    catch (Exception error)
                    {
                        if (RegistrationFailure != null)
                        {
                            throw new AggregateException("Preload cleanup registration and release failed.", RegistrationFailure, error);
                        }
                        throw;
                    }
                    finally
                    {
                        owner.RefreshPreloadReservations();
                    }
                    if (RegistrationFailure != null)
                    {
                        ExceptionDispatchInfo.Capture(RegistrationFailure).Throw();
                    }
                }
            }
        }
    }
}
