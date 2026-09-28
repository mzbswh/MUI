using System;
using System.Threading.Tasks;

namespace MUI.UGUI
{
    public sealed partial class VirtualListElement
    {
        private bool synchronousRefresh;
        private Exception synchronousRefreshFailure;
        private TaskCompletionSource<bool> synchronousRefreshCompletion;
        private bool synchronousRevealing;

        bool IChildViewElement.TryCompleteSynchronousPreparation()
        {
            if (running)
            {
                return false;
            }

            var failure = Error ?? synchronousRefreshFailure;
            if (failure != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }

            return !dirty;
        }

        /// <summary>仅兼容任务查询接口时创建信号；同步执行器不读取或等待该信号。</summary>
        private Task GetPendingChange()
        {
            if (!synchronousRefresh)
            {
                return pending ?? Task.CompletedTask;
            }

            if (synchronousRefreshCompletion != null)
            {
                return synchronousRefreshCompletion.Task;
            }

            if (!running && synchronousRefreshFailure == null)
            {
                return Task.CompletedTask;
            }

            synchronousRefreshCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (synchronousRefreshFailure != null)
            {
                synchronousRefreshCompletion.TrySetException(synchronousRefreshFailure);
                _ = synchronousRefreshCompletion.Task.Exception;
            }

            return synchronousRefreshCompletion.Task;
        }

        private void BeginSynchronousRefresh()
        {
            synchronousRefresh = true;
            synchronousRefreshFailure = null;
            synchronousRefreshCompletion = null;
        }

        private void FailSynchronousRefresh(Exception failure)
        {
            synchronousRefreshFailure = failure;
            if (synchronousRefreshCompletion != null)
            {
                synchronousRefreshCompletion.TrySetException(failure);
                _ = synchronousRefreshCompletion.Task.Exception;
            }
        }

        private void ValidateSynchronousSource(IReadOnlyObservableList<VirtualListItem> source)
        {
            if (lifetime != null && lifetime.Mode == LifetimeMode.Synchronous && source is IPagedListSource)
            {
                throw new InvalidOperationException("Synchronous virtual lists require a local synchronous data source.");
            }
        }

        private void RequireAsyncListAllowed()
        {
            if (lifetime != null && lifetime.Mode == LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("This virtual list does not allow asynchronous operations.");
            }
        }

        /// <summary>直接执行视口协调、同步嵌套绑定和单元回收，完成信号不负责调度工作。</summary>
        private void RefreshSynchronous()
        {
            BeginSynchronousRefresh();
            var activation = lifetime;
            running = true;
            try
            {
                activation.Run(token =>
                {
                    SetStatus(VirtualListStatus.Loading);
                    var remaining = (long)maxCells + 8;
                    while (IsAlive && ReferenceEquals(lifetime, activation) && scope != null && scope.IsActive)
                    {
                        token.ThrowIfCancellationRequested();
                        if (Error != null)
                        {
                            throw Error;
                        }

                        if (--remaining < 0)
                        {
                            throw new InvalidOperationException("Synchronous list refresh did not stabilize within its callback budget.");
                        }

                        if (dirty)
                        {
                            dirty = false;
                            foreach (var batch in RefreshCells())
                            {
                                // NestedViewElement 的同步设置器已完成绑定或直接抛出错误。
                                // 不通过 PendingChange 或异步 API 判断同步能力。
                                token.ThrowIfCancellationRequested();
                            }

                            continue;
                        }

                        SetStatus(snapshot.Count == 0 ? VirtualListStatus.Empty : VirtualListStatus.Ready);
                        if (!ReferenceEquals(lifetime, activation))
                        {
                            break;
                        }

                        if (!dirty && Error == null)
                        {
                            break;
                        }
                    }

                    if (ReferenceEquals(lifetime, activation) && (scope == null || !scope.IsActive))
                    {
                        SetStatus(VirtualListStatus.Inactive);
                    }
                });
                if (ReferenceEquals(lifetime, activation) && synchronousRefreshCompletion != null)
                {
                    synchronousRefreshCompletion.TrySetResult(true);
                }
            }
            catch (Exception failure)
            {
                if (ReferenceEquals(lifetime, activation))
                {
                    FailSynchronousRefresh(failure);
                    SetStatus(VirtualListStatus.Error, failure);
                }

                throw;
            }
            finally
            {
                if (ReferenceEquals(lifetime, activation))
                {
                    running = false;
                }
            }
        }

        /// <summary>同步定位并物化指定条目，可选地聚焦；测量无法在有界次数内稳定则返回失败。</summary>
        public VirtualListRevealOutcome ScrollToKey(object key, bool focus = false)
        {
            RequireListAlive();
            if (lifetime == null || lifetime.Mode != LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("ScrollToKey requires a synchronous virtual list.");
            }

            if (System.Threading.Thread.CurrentThread.ManagedThreadId != uiThread)
            {
                throw new InvalidOperationException("List reveal must run on its UI thread.");
            }

            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (scope == null || !scope.IsActive)
            {
                return RevealResult(VirtualListRevealStatus.Inactive);
            }

            if (running || synchronousRevealing)
            {
                return new VirtualListRevealOutcome(VirtualListRevealStatus.Failed,
                    new InvalidOperationException("Cannot reenter synchronous list reveal."));
            }

            if (!keyIndices.TryGetValue(key, out var index))
            {
                return RevealResult(VirtualListRevealStatus.NotFound);
            }

            var request = ++revealRevision;
            var revision = revealSourceRevision;
            var activation = lifetime;
            synchronousRevealing = true;
            try
            {
                return activation.Run(token =>
                {
                    for (long pass = 0; pass < (long)maxCells + 8; ++pass)
                    {
                        var invalid = InvalidRevealStatus(request, revision, activation, token);
                        if (invalid.HasValue)
                        {
                            return RevealResult(invalid.Value);
                        }

                        var top = Layout.OffsetForIndex(index);
                        var offset = scrollRect.content.anchoredPosition.y;
                        var height = scrollRect.viewport.rect.height;
                        if (top < offset || ItemHeight(index) > height)
                        {
                            SetOffset(top);
                        }
                        else if (top + ItemHeight(index) > offset + height)
                        {
                            SetOffset(top + ItemHeight(index) - height);
                        }

                        RequestRefresh();
                        MeasureVisibleItems();
                        invalid = InvalidRevealStatus(request, revision, activation, token);
                        if (invalid.HasValue)
                        {
                            return RevealResult(invalid.Value);
                        }

                        if (Error != null)
                        {
                            return new VirtualListRevealOutcome(VirtualListRevealStatus.Failed, Error);
                        }

                        if (VisibleMeasurementsReady() && IsRevealAligned(index))
                        {
                            return CompleteReveal(key, focus, request, revision, activation);
                        }
                    }

                    return new VirtualListRevealOutcome(VirtualListRevealStatus.Failed,
                        new InvalidOperationException("Synchronous list geometry did not stabilize within its measurement budget."));
                });
            }
            catch (Exception error)
            {
                return new VirtualListRevealOutcome(VirtualListRevealStatus.Failed, error);
            }
            finally
            {
                synchronousRevealing = false;
            }
        }

        /// <summary>同步重新读取当前数据并重建视口；失败直接报告，未完成清理的单元不能复用。</summary>
        public void Retry()
        {
            RequireListAlive();
            if (lifetime == null || lifetime.Mode != LifetimeMode.Synchronous || scope == null || !scope.IsActive)
            {
                throw new InvalidOperationException("Retry requires an active synchronous virtual list.");
            }

            if (running)
            {
                throw new InvalidOperationException("Cannot retry during synchronous list refresh callbacks.");
            }

            ApplySnapshot(ReadSnapshot(items));
        }
    }
}
