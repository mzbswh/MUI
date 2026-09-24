using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private PreloadBatch preloadBatch;
        private readonly int preloadCapacity;
        private int preloadReservations;
        private readonly Lifetime preloadCleanup;
        private readonly List<Exception> preloadCleanupErrors = new List<Exception>();
        private readonly HashSet<Task> preloadClearings = new HashSet<Task>();

        /// <summary>容量包含准备中、驻留和待释放的持有权；清空不会提前归还容量。</summary>
        public int PreloadReservationCount
        {
            get
            {
                AssertThread();
                return preloadReservations;
            }
        }

        private ValueTask<PreloadOutcome> PreloadAsyncUntraced(Route route, CancellationToken cancellationToken = default)
        {
            AssertThread();
            RequireAsyncNavigation();
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            if (IsShutdown)
            {
                return PreloadResult(PreloadStatus.HostClosed);
            }

            if (IsReentrant)
            {
                return PreloadResult(PreloadStatus.Reentrant);
            }

            if (IsClearingInactiveContent)
            {
                return PreloadResult(PreloadStatus.InactiveContentClearing);
            }

            if (!(provider is IPreloadViewProvider preloader))
            {
                return PreloadResult(PreloadStatus.Unsupported);
            }

            Register(route);
            RefreshPreloadProviderVersion();
            if (IsShutdown)
            {
                return PreloadResult(PreloadStatus.HostClosed);
            }

            var batch = preloadBatch;
            if (batch.Entries.TryGetValue(route.Resource, out var existing))
            {
                return new ValueTask<PreloadOutcome>(WaitForPreloadAsync(existing.Task, cancellationToken));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return PreloadResult(PreloadStatus.Cancelled);
            }

            if (preloadReservations >= preloadCapacity)
            {
                return PreloadResult(PreloadStatus.CapacityExceeded);
            }

            var completion = new TaskCompletionSource<PreloadOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            batch.Entries.Add(route.Resource, completion);
            ++preloadReservations;
            // 调用提供方前先登记，确保 Clear/Shutdown 等待迟到的持有凭证。
            _ = batch.Lifetime.RunAsync(token => LoadPreloadAsync(batch, preloader, route.Resource, token, cancellationToken, completion));
            return new ValueTask<PreloadOutcome>(completion.Task);
        }

        private async ValueTask<bool> LoadPreloadAsync(PreloadBatch batch,
                    IPreloadViewProvider preloader,
                    ViewResource resource,
                    CancellationToken ownerToken,
                    CancellationToken callerToken,
                    TaskCompletionSource<PreloadOutcome> completion)
        {
            IPreloadLease lease = null;
            var adopted = false;
            var cleanupFailed = false;
            var versionChanged = false;
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ownerToken, callerToken, shutdown.Token))
            {
                try
                {
                    using (EnterCallback(null))
                    {
                        lease = await preloader.PreloadAsync(resource, linked.Token);
                    }

                    AssertThread();
                    if (lease == null)
                    {
                        throw new InvalidOperationException("Provider returned no preload lease.");
                    }

                    linked.Token.ThrowIfCancellationRequested();
                    if (!ReferenceEquals(preloadBatch, batch))
                    {
                        throw new OperationCanceledException(ownerToken);
                    }

                    if (!resource.Equals(lease.Resource))
                    {
                        throw new InvalidOperationException("Preload resource identity mismatch.");
                    }

                    versionChanged = !ReferenceEquals(batch.ProviderVersion, CaptureProviderVersion());
                    if (versionChanged)
                    {
                        throw new OperationCanceledException("Preload provider content changed.");
                    }

                    batch.Lifetime.Own(lease);
                    ++batch.OwnedCount;
                    adopted = true;
                    lease = null;
                    completion.TrySetResult(new PreloadOutcome(PreloadStatus.Ready));
                }
                catch (Exception failure)
                {
                    if (lease != null)
                    {
                        try
                        {
                            using (EnterCallback(null))
                            {
                                await lease.DisposeAsync();
                            }
                        }
                        catch (Exception cleanup)
                        {
                            cleanupFailed = true;
                            preloadCleanupErrors.Add(cleanup);
                            failure = new AggregateException("Preload and late cleanup failed.", failure, cleanup);
                        }
                    }

                    var status = cleanupFailed ? PreloadStatus.Failed : IsShutdown ? PreloadStatus.HostClosed : batch.Lifetime.IsEnded || versionChanged ? PreloadStatus.Superseded : failure is OperationCanceledException ? PreloadStatus.Cancelled : PreloadStatus.Failed;
                    completion.TrySetResult(new PreloadOutcome(status, status == PreloadStatus.Failed ? failure : null));
                }
                finally
                {
                    if (!adopted)
                    {
                        batch.Entries.Remove(resource);
                        if (!cleanupFailed)
                        {
                            --preloadReservations;
                        }
                    }
                }
            }

            return true;
        }

        public ValueTask ClearPreloadsAsync()
        {
            AssertThread();
            RequireAsyncNavigation();
            if (IsReentrant)
            {
                return new ValueTask(Task.FromException(new InvalidOperationException("Cannot await preload clearing from a provider or lifecycle callback.")));
            }

            if (IsShutdown)
            {
                return ShutdownAsync();
            }

            return new ValueTask(StartPreloadClear());
        }

        /// <summary>访问预加载或维护时观察资源代际，不复用后端已失效的 Ready 记录。</summary>
        private void RefreshPreloadProviderVersion()
        {
            if (IsShutdown)
            {
                return;
            }

            var version = CaptureProviderVersion();
            if (IsShutdown)
            {
                return;
            }

            if (preloadBatch.ProviderVersion != null && !ReferenceEquals(preloadBatch.ProviderVersion, version))
            {
                _ = ObservePreloadInvalidationAsync(StartPreloadClear());
            }

            preloadBatch.ProviderVersion = version;
        }

        private static async Task ObservePreloadInvalidationAsync(Task clearing)
        {
            try
            {
                await clearing;
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }

        private Task StartPreloadClear()
        {
            RequireAsyncNavigation();
            var previous = preloadBatch;
            preloadBatch = new PreloadBatch();
            // 在取消回调执行前登记清理，回调导致宿主退出时也不能遗漏旧批次。
            var task = preloadCleanup.RunAsync(_ => ClearPreloadBatchAsync(previous)).AsTask();
            preloadClearings.Add(task);
            _ = UntrackPreloadClearAsync(task);
            return task;
        }

        private async Task UntrackPreloadClearAsync(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception)
            {
                // 错误由原等待者和关闭诊断处理；此处只负责移出在途目录。
            }
            finally
            {
                preloadClearings.Remove(task);
            }
        }

        private async ValueTask<bool> ClearPreloadBatchAsync(PreloadBatch batch)
        {
            try
            {
                using (EnterCallback(null))
                {
                    await batch.Lifetime.DisposeAsync();
                }

                preloadReservations -= batch.OwnedCount;
                batch.Entries.Clear();
                return true;
            }
            catch (Exception error)
            {
                // 释放失败的资源仍占用容量，错误在关闭时报告。
                preloadCleanupErrors.Add(error);
                throw;
            }
        }

        private async Task FinishPreloadShutdownAsync(Task latestClear)
        {
            try
            {
                await latestClear;
            }
            catch (Exception)
            { /* 已计入清理错误记录。 */
            }

            await preloadCleanup.DisposeAsync();
            await preloadBatch.Lifetime.DisposeAsync();
            if (preloadCleanupErrors.Count != 0)
            {
                throw new AggregateException("Preload cleanup failed.", preloadCleanupErrors);
            }
        }

        private static async Task<PreloadOutcome> WaitForPreloadAsync(Task<PreloadOutcome> task, CancellationToken token)
        {
            try
            {
                var result = await AsyncWait.WithCancellation(task, token);
                return new PreloadOutcome(result.Status, result.Error, reusedReservation: true);
            }
            catch (OperationCanceledException)
            {
                return new PreloadOutcome(PreloadStatus.WaitCancelled, reusedReservation: true);
            }
        }

        private static ValueTask<PreloadOutcome> PreloadResult(PreloadStatus status) => new ValueTask<PreloadOutcome>(new PreloadOutcome(status));

        private sealed class PreloadBatch
        {
            public readonly Lifetime Lifetime = new Lifetime();
            public readonly Dictionary<ViewResource, TaskCompletionSource<PreloadOutcome>> Entries = new Dictionary<ViewResource, TaskCompletionSource<PreloadOutcome>>();
            public int OwnedCount;
            public object ProviderVersion;
        }
    }
}
