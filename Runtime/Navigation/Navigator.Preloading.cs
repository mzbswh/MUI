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
        private readonly LifetimeScope preloadCleanup;
        private readonly List<Exception> preloadCleanupErrors = new List<Exception>();
        private long omittedPreloadCleanupErrors;
        private readonly HashSet<Task> preloadClearings = new HashSet<Task>();

        /// <summary>容量包含准备中、驻留和待释放的持有权；清空不会提前归还容量。</summary>
        public int PreloadReservationCount
        {
            get
            {
                AssertThread();
                return preloadReservations - ConfirmedPreloadReturnCount;
            }
        }

        private ValueTask<PreloadOutcome> PreloadAsyncUntraced(Route route, CancellationToken cancellationToken = default)
        {
            AssertThread();
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
            RefreshPreloadReservations();
            RefreshPreloadProviderVersion();
            if (IsShutdown)
            {
                return PreloadResult(PreloadStatus.HostClosed);
            }

            var batch = preloadBatch;
            if (batch.Entries.TryGetValue(route.Resource, out var existing))
            {
                return new ValueTask<PreloadOutcome>(existing.ObserveAsync(existing.Join(cancellationToken), cancellationToken, true));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return PreloadResult(PreloadStatus.Cancelled);
            }

            if (preloadReservations >= preloadCapacity)
            {
                return PreloadResult(PreloadStatus.CapacityExceeded);
            }

            var entry = new PreloadEntry();
            var consumer = entry.Join(cancellationToken);
            batch.Entries.Add(route.Resource, entry);
            ++preloadReservations;
            // 调用提供方前先登记，确保 Clear/Shutdown 等待迟到的持有凭证。
            _ = batch.Scope.RunAsync(token => LoadPreloadAsync(batch, preloader, route.Resource, token, entry));
            return new ValueTask<PreloadOutcome>(entry.ObserveAsync(consumer, cancellationToken, false));
        }

        private async ValueTask<bool> LoadPreloadAsync(PreloadBatch batch,
                    IPreloadViewProvider preloader,
                    ViewResource resource,
                    CancellationToken ownerToken,
                    PreloadEntry entry)
        {
            IAcquiredPreload ownedResource = null;
            PreloadReservation reservation = null;
            var adopted = false;
            var cleanupFailed = false;
            var versionChanged = false;
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ownerToken, entry.Token, shutdown.Token))
            {
                try
                {
                    linked.Token.ThrowIfCancellationRequested();
                    using (EnterCallback(null))
                    {
                        ownedResource = await preloader.PreloadAsync(resource, linked.Token);
                    }

                    if (ownedResource == null)
                    {
                        throw new InvalidOperationException("Provider returned no preload acquisition.");
                    }

                    using (EnterCallback(null))
                    {
                        reservation = new PreloadReservation(this, ownedResource, "Navigator.Preload");
                    }
                    preloadReturns.Add(reservation);
                    AssertThread();
                    if (reservation.RegistrationFailure != null)
                    {
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(reservation.RegistrationFailure).Throw();
                    }
                    linked.Token.ThrowIfCancellationRequested();
                    if (!ReferenceEquals(preloadBatch, batch))
                    {
                        throw new OperationCanceledException(ownerToken);
                    }

                    if (!resource.Equals(ownedResource.Resource))
                    {
                        throw new InvalidOperationException("Preload resource identity mismatch.");
                    }

                    versionChanged = !ReferenceEquals(batch.ProviderVersion, CaptureProviderVersion());
                    if (versionChanged)
                    {
                        throw new OperationCanceledException("Preload provider content changed.");
                    }

                    if (!entry.TryRetain(() => batch.Scope.Own(reservation)))
                    {
                        throw new OperationCanceledException("All preload requests were cancelled before residency commit.");
                    }
                    adopted = true;
                    ownedResource = null;
                }
                catch (Exception failure)
                {
                    var pendingRollback = failure as ResourceLoadException;
                    CleanupResponsibility cancellationDependency = null;
                    var cancellationFailure = await entry.EndAdmission();
                    if (cancellationFailure != null)
                    {
                        // 取消回调失败也不能据资源消失宣称整个准备责任已确认。
                        cancellationDependency = new CleanupResponsibility(
                            () => new ValueTask(Task.FromException(cancellationFailure)), "Navigator.PreloadCancellation");
                        try
                        {
                            await cancellationDependency.DisposeAsync();
                        }
                        catch (Exception)
                        {
                            // 已由稳定责任保存同一取消错误，继续独立凭证收尾。
                        }
                        cleanupFailed = true;
                        RecordPreloadCleanupFailure(cancellationFailure);
                        batch.Scope.RecordCleanupFailure(cancellationFailure, cancellationDependency);
                        reservation?.RetainCancellationDependency(cancellationDependency);
                        failure = new AggregateException("Preload cancellation failed.", failure, cancellationFailure);
                    }
                    if (ownedResource != null)
                    {
                        try
                        {
                            using (EnterCallback(null))
                            {
                                await reservation.DisposeAsync();
                            }
                        }
                        catch (Exception cleanup)
                        {
                            cleanupFailed = true;
                            RecordPreloadCleanupFailure(cleanup);
                            batch.Scope.RecordCleanupFailure(cleanup, reservation.CleanupResponsibility);
                            failure = new AggregateException("Preload and late cleanup failed.", failure, cleanup);
                        }
                    }
                    else if (pendingRollback != null)
                    {
                        // 没有交付凭证的后端回滚也保留一次责任；失败任务不具备安全重试声明。
                        var rollback = new CleanupResponsibility(async () => await pendingRollback.CleanupCompletion,
                            "Navigator.PreloadRollback");
                        reservation = new PreloadReservation(this, rollback, "Navigator.PreloadRollback");
                        reservation.RetainCancellationDependency(cancellationDependency);
                        preloadReturns.Add(reservation);
                        try
                        {
                            await reservation.DisposeAsync();
                        }
                        catch (Exception cleanup)
                        {
                            cleanupFailed = true;
                            RecordPreloadCleanupFailure(cleanup);
                            batch.Scope.RecordCleanupFailure(cleanup, reservation.CleanupResponsibility);
                            failure = new AggregateException("Preload and resource rollback failed.", failure, cleanup);
                        }
                    }

                    if (reservation == null && cancellationDependency != null)
                    {
                        reservation = new PreloadReservation(this, cancellationDependency, "Navigator.PreloadCancellation");
                        preloadReturns.Add(reservation);
                        try
                        {
                            await reservation.DisposeAsync();
                        }
                        catch (Exception)
                        {
                            // 没有交付凭证时，这次预留仍由未知取消责任持有。
                        }
                    }

                    var status = cleanupFailed ? PreloadStatus.Failed : IsShutdown ? PreloadStatus.HostClosed : batch.Scope.IsEnded || versionChanged ? PreloadStatus.Superseded : failure is OperationCanceledException ? PreloadStatus.Cancelled : PreloadStatus.Failed;
                    entry.Complete(new PreloadOutcome(status, status == PreloadStatus.Failed ? failure : null));
                }
                finally
                {
                    entry.Dispose();
                    if (!adopted)
                    {
                        batch.Entries.Remove(resource);
                        if (reservation == null && !cleanupFailed)
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
            if (IsReentrant)
            {
                return new ValueTask(Task.FromException(new InvalidOperationException("Cannot await preload clearing from a provider or lifecycle callback.")));
            }

            if (IsShutdown)
            {
                return ShutdownAsync();
            }

            return new ValueTask(ClearPreloadSnapshotAsync());
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
            var previous = preloadBatch;
            preloadBatch = new PreloadBatch();
            // 在取消回调执行前登记清理，回调导致宿主退出时也不能遗漏旧批次。
            Task task;
            try
            {
                task = preloadCleanup.RunAsync(_ => ClearPreloadBatchAsync(previous)).AsTask();
            }
            catch
            {
                preloadBatch = previous;
                throw;
            }

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
                    await batch.Scope.DisposeAsync();
                }

                return true;
            }
            catch (Exception error)
            {
                // 释放失败的资源仍占用容量，错误在关闭时报告。
                RecordPreloadCleanupFailure(error);
                throw;
            }
            finally
            {
                batch.Entries.Clear();
            }
        }

        private void RecordPreloadCleanupFailure(Exception error)
        {
            hasCleanupFailure = true;
            if (preloadCleanupErrors.Count < terminalCapacity)
            {
                preloadCleanupErrors.Add(error);
            }
            else if (omittedPreloadCleanupErrors < long.MaxValue)
            {
                ++omittedPreloadCleanupErrors;
            }
        }

        private async Task FinishPreloadShutdownAsync(Task latestClear)
        {
            var errors = new List<Exception>();
            try
            {
                await latestClear;
            }
            catch (Exception error)
            {
                if (!IsRecordedInactiveCleanup(error))
                {
                    errors.Add(error);
                }
            }

            try
            {
                await preloadCleanup.DisposeAsync();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            try
            {
                await preloadBatch.Scope.DisposeAsync();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            errors.AddRange(preloadCleanupErrors);
            preloadCleanupErrors.Clear();
            if (omittedPreloadCleanupErrors != 0)
            {
                errors.Add(new InvalidOperationException(
                    $"Additional preload cleanup failures omitted: {omittedPreloadCleanupErrors}."));
                omittedPreloadCleanupErrors = 0;
            }

            if (errors.Count != 0)
            {
                throw new AggregateException("Preload cleanup failed.", errors);
            }
        }

        private static ValueTask<PreloadOutcome> PreloadResult(PreloadStatus status) => new ValueTask<PreloadOutcome>(new PreloadOutcome(status));

        private sealed class PreloadBatch
        {
            public readonly LifetimeScope Scope = new LifetimeScope();
            public readonly Dictionary<ViewResource, PreloadEntry> Entries = new Dictionary<ViewResource, PreloadEntry>();
            public object ProviderVersion;
        }
    }
}
