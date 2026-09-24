using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewSlot
    {
        private readonly ChildViewPreparationOptions preparationOptions;
        private readonly HashSet<QuarantinedPreparation> quarantined = new HashSet<QuarantinedPreparation>();

        /// <summary>尚未完成准备或迟到清理的隔离操作数，计入准备容量。</summary>
        public int QuarantinedPreparationCount
        {
            get
            {
                scope.RequireThread();
                return quarantined.Count;
            }
        }

        private async ValueTask<ChildViewHandle> PrepareWithTimeoutAsync(Request request, CancellationToken token)
        {
            if (!preparationOptions.Timeout.HasValue)
            {
                return await InvokePreparationAsync(request, token);
            }

            if (quarantined.Count >= preparationOptions.MaxQuarantinedPreparations)
            {
                throw new InvalidOperationException("Child view preparation quarantine capacity is exhausted.");
            }

            var preparation = InvokePreparationAsync(request, token).AsTask();
            using (var timerCancellation = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                var deadline = Task.Delay(preparationOptions.Timeout.Value, timerCancellation.Token);
                await Task.WhenAny(preparation, deadline);
                // 同一轮完成时优先接管准备结果，避免把已经获得的资源遗失在超时分支。
                if (preparation.IsCompleted)
                {
                    timerCancellation.Cancel();
                    return await preparation;
                }

                var entry = new QuarantinedPreparation { Request = request, Preparation = preparation };
                request.Quarantined = true;
                quarantined.Add(entry);
                var cancelled = token.IsCancellationRequested;
                var timeout = cancelled ? null : new TimeoutException("Child view preparation exceeded its configured timeout.");
                if (timeout != null)
                {
                    // 先发布失败，再发出合作取消，取消回调不能把超时改写成普通取消。
                    request.Complete(ChildViewChangeStatus.Failed, timeout);
                }

                try
                {
                    request.Cancellation.Cancel();
                }
                catch (Exception error)
                {
                    cleanupErrors.Add(error);
                    UIErrors.Report(error);
                }

                _ = CleanupQuarantinedAsync(entry);
                if (cancelled)
                {
                    throw new OperationCanceledException(token);
                }

                throw timeout;
            }
        }

        private async Task CleanupQuarantinedAsync(QuarantinedPreparation entry)
        {
            var previous = preparationFrame.Value;
            var frame = new PreparationFrame();
            preparationFrame.Value = frame;
            try
            {
                var handle = await entry.Preparation;
                scope.RequireThread();
                if (handle == null || !handle.BelongsTo(scope))
                {
                    throw new InvalidOperationException("Late preparation must return a Prepared handle in its original scope.");
                }

                if (handle.State == ChildViewState.Prepared)
                {
                    // 迟到候选永不提交，完整关闭后才释放隔离名额。
                    await handle.BeginClose();
                }
                else if (handle.State == ChildViewState.Closing || handle.State == ChildViewState.Closed ||
                    handle.State == ChildViewState.Failed)
                {
                    // 父级或项目可能先发起关闭，仍须等待同一清理任务，不能提前释放隔离名额。
                    await handle.CleanupCompletion;
                }
                else
                {
                    throw new InvalidOperationException("Late preparation returned content outside its preparation or cleanup lifecycle.");
                }
            }
            catch (OperationCanceledException)
            {
                // 标准准备入口已经在取消路径中完成资源回滚。
            }
            catch (Exception error)
            {
                cleanupErrors.Add(error);
                UIErrors.Report(error);
                if (error is ChildViewPreparationException preparationFailure)
                {
                    try
                    {
                        await preparationFailure.CleanupCompletion;
                    }
                    catch (Exception cleanup)
                    {
                        cleanupErrors.Add(cleanup);
                        UIErrors.Report(cleanup);
                    }
                }
            }
            finally
            {
                frame.Active = false;
                preparationFrame.Value = previous;
                entry.Request.Dispose();
                quarantined.Remove(entry);
                entry.Cleanup.TrySetResult(true);
            }
        }

        private async Task DrainQuarantinedAsync()
        {
            // 正常准备泵已停止，不会新增隔离项；最终释放仍如实等待底层任务与资源清理。
            foreach (var entry in new List<QuarantinedPreparation>(quarantined))
            {
                await entry.Cleanup.Task;
            }
        }

        private sealed class QuarantinedPreparation
        {
            public Request Request;
            public Task<ChildViewHandle> Preparation;
            public readonly TaskCompletionSource<bool> Cleanup =
                            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
