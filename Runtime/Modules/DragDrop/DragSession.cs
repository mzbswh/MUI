using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.DragDrop
{
    /// <summary>
    /// 源界面激活周期拥有的一次拖拽，至多提交一次业务操作。
    /// 创建和公开操作在所属 UI 线程执行，异步结束通过该线程的 SynchronizationContext 收敛。
    /// </summary>
    public sealed partial class DragSession<TPayload> : IAsyncDisposable
    {
        /// <summary>每个来源 LifetimeScope、每种载荷类型最多同时保留的未结束会话数，不计历史完成会话。</summary>
        public const int MaxConcurrentSessionsPerSource = 256;
        private readonly LifetimeScope sourceLifetime;
        private readonly AsyncLocal<CommitFrame> executing;
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly SynchronizationContext context;
        private readonly CancellationTokenSource cancellation;
        private readonly CancellationToken token;
        private readonly object pendingResultGate = new object();
        private CancellationTokenRegistration invalidation;
        private TaskCompletionSource<DropResult> completion;
        private DropResult finalResult;
        private bool finished;
        private Action<DropResult> settleVisual;
        private IDisposable capture;
        private bool releasingCapture;
        private bool notifying;
        private bool evaluating;
        private Task commitTask;
        private Task disposal;
        private readonly DragSessionOwner<TPayload> owner;
        private DropResult pendingResult;
        private bool resultPending;
        private int cancellationPending;

        /// <summary>创建拖拽会话并向源生命周期登记清理；构造不创建完成任务。</summary>
        /// <param name="payload">本次拖拽的数据，不在会话内复制或管理业务资源所有权。</param>
        /// <param name="source">尚未结束的源激活周期。</param>
        /// <param name="pointerCapture">创建成功后交给会话释放的指针关联凭证。</param>
        /// <param name="settleVisual">最终结果确定后的视觉收尾回调，不得等待本会话销毁。</param>
        /// <param name="uiContext">所属 UI 调度上下文；省略时使用当前上下文。</param>
        public DragSession(TPayload payload, LifetimeScope source, IDisposable pointerCapture,
            Action<DropResult> settleVisual, SynchronizationContext uiContext = null)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (source.IsEnded)
            {
                throw new ObjectDisposedException(nameof(source));
            }

            if (pointerCapture == null)
            {
                throw new ArgumentNullException(nameof(pointerCapture));
            }

            if (settleVisual == null)
            {
                throw new ArgumentNullException(nameof(settleVisual));
            }

            sourceLifetime = source;
            context = uiContext ?? SynchronizationContext.Current;
            if (context == null)
            {
                throw new InvalidOperationException("Drag sessions require an owning UI SynchronizationContext.");
            }
            executing = new AsyncLocal<CommitFrame>();
            Payload = payload;
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(source.Token);
            token = cancellation.Token;
            try
            {
                owner = DragSessionOwner<TPayload>.Attach(source, this);
            }
            catch
            {
                cancellation.Dispose();
                throw;
            }

            capture = pointerCapture;
            this.settleVisual = settleVisual;
            invalidation = token.Register(OnCancelled);
            // 注册回调可能同步结束已被取消的会话，需回收刚得到的注册凭证。
            if (finished)
            {
                invalidation.Dispose();
            }
        }

        /// <summary>业务结果及视觉收尾尝试是否都已完成，不创建或探测任务。</summary>
        public bool IsCompleted
        {
            get
            {
                RequireThread();
                return finished;
            }
        }

        /// <summary>本次拖拽的数据载荷。</summary>
        public TPayload Payload
        {
            get;
        }

        /// <summary>当前交互阶段；Dropping 表示指针已释放，业务提交尚未收尾。</summary>
        public DragPhase Phase
        {
            get; private set;
        }

        /// <summary>源生命周期或主动 Cancel 发出的合作式取消信号。</summary>
        public CancellationToken Token => token;

        /// <summary>显式观察完成结果，按需创建任务；即时状态可用 TryGetResult 查询。</summary>
        public Task<DropResult> Completion
        {
            get
            {
                RequireThread();
                if (completion == null)
                {
                    completion = new TaskCompletionSource<DropResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                    if (finished)
                    {
                        completion.TrySetResult(finalResult);
                    }
                }
                return completion.Task;
            }
        }

        /// <summary>读取已发布结果，未完成时返回 false；不创建完成任务。</summary>
        public bool TryGetResult(out DropResult result)
        {
            RequireThread();
            result = finalResult;
            return finished;
        }

        /// <summary>同步查询当前目标是否可接受载荷；业务谓词应无副作用，提交前仍会重新检查。</summary>
        public bool CanDrop(DropTarget<TPayload> target)
        {
            RequireThread();
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (evaluating || notifying || Phase != DragPhase.Dragging ||
                token.IsCancellationRequested || target.Scope.IsEnded)
            {
                return false;
            }

            evaluating = true;
            try
            {
                var accepted = target.CanAccept(Payload);
                return accepted && Phase == DragPhase.Dragging && !token.IsCancellationRequested && !target.Scope.IsEnded;
            }
            finally
            {
                evaluating = false;
            }
        }

        /// <summary>所属 UI 线程收敛后台取消和提交结果。</summary>
        public void Pump()
        {
            RequireThread();
            DropResult result;
            bool hasResult;
            lock (pendingResultGate)
            {
                hasResult = resultPending;
                result = pendingResult;
                resultPending = false;
            }

            if (hasResult)
            {
                Finish(result);
            }

            if (Interlocked.Exchange(ref cancellationPending, 0) != 0 || token.IsCancellationRequested)
            {
                CancelOnUIThread();
            }
        }

        /// <summary>
        /// 结束拖拽并尝试提交到目标。进入提交阶段后释放指针，等待业务回调确定最终结果。
        /// 重复调用共享第一次操作的结果，不向另一个目标重复提交。
        /// </summary>
        public ValueTask<DropResult> DropAsync(DropTarget<TPayload> target)
        {
            RequireThread();

            RequireDropAllowed(target);
            if (BeginDrop(target))
            {
                commitTask = CommitAsync(target);
            }
            return new ValueTask<DropResult>(Completion);
        }

        private void RequireDropAllowed(DropTarget<TPayload> target)
        {
            if (evaluating || releasingCapture || notifying ||
                (Phase == DragPhase.Completed && !finished) ||
                (executing != null && executing.Value != null && executing.Value.Active))
            {
                throw new InvalidOperationException("不能从拖放回调重入本会话的提交。");
            }
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
        }

        private bool BeginDrop(DropTarget<TPayload> target)
        {
            // 重复松开只等待原操作，不能再向第二个目标提交。
            if (Phase != DragPhase.Dragging)
            {
                return false;
            }

            if (token.IsCancellationRequested || target.Scope.IsEnded)
            {
                Finish(new DropResult(DropStatus.Cancelled));
                return false;
            }

            try
            {
                if (!CanDrop(target))
                {
                    Finish(new DropResult(token.IsCancellationRequested || target.Scope.IsEnded
                        ? DropStatus.Cancelled
                        : DropStatus.Rejected));
                    return false;
                }
            }
            catch (Exception error)
            {
                Finish(new DropResult(DropStatus.Failed, error));
                return false;
            }

            Phase = DragPhase.Dropping;
            try
            {
                ReleaseCapture();
            }
            catch (Exception error)
            {
                Finish(new DropResult(DropStatus.Failed, error));
                return false;
            }

            return true;
        }

        /// <summary>
        /// 发出取消信号。拖拽阶段立即结束；提交阶段等待业务返回，不把已确认成功改成取消。
        /// 取消不负责撤销已提交的业务修改。
        /// </summary>
        public void Cancel()
        {
            RequireThread();
            if (Phase == DragPhase.Completed)
            {
                return;
            }

            try
            {
                cancellation.Cancel();
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
            finally
            {
                // 令牌可能先在后台取消；再次 Cancel 不会重跑登记回调，仍需在所属线程收尾。
                CancelOnUIThread();
            }
        }

        private async Task CommitAsync(DropTarget<TPayload> target)
        {
            DropResult result;
            var frame = new CommitFrame();
            executing.Value = frame;
            try
            {
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, target.Scope.Token))
                {
                    linked.Token.ThrowIfCancellationRequested();
                    // 两端在清理资源前均等待提交退出；会话管理器的登记次序不能决定业务资源安全。
                    var accepted = await sourceLifetime.RunAsync(_ =>
                        target.Scope.RunAsync(__ => target.Commit(Payload, linked.Token)));
                    // 业务已确认成功时，竞态到达的取消信号不能将结果改为取消。
                    result = new DropResult(accepted
                        ? DropStatus.Committed
                        : linked.IsCancellationRequested ? DropStatus.Cancelled : DropStatus.Rejected);
                }
            }
            catch (OperationCanceledException)
            {
                result = new DropResult(DropStatus.Cancelled);
            }
            catch (Exception error)
            {
                result = new DropResult(DropStatus.Failed, error);
            }
            finally
            {
                frame.Active = false;
                executing.Value = null;
            }

            if (Thread.CurrentThread.ManagedThreadId == thread)
            {
                Finish(result);
            }
            else
            {
                lock (pendingResultGate)
                {
                    pendingResult = result;
                    resultPending = true;
                }

                try
                {
                    context.Post(_ =>
                    {
                        if (Thread.CurrentThread.ManagedThreadId != thread)
                        {
                            UIErrors.Report(new InvalidOperationException("拖放结果未派发到所属 UI 线程。"));
                            return;
                        }

                        Pump();
                    }, null);
                }
                catch (Exception error)
                {
                    UIErrors.Report(error);
                }
            }
        }

        private void OnCancelled()
        {
            if (Thread.CurrentThread.ManagedThreadId == thread)
            {
                CancelOnUIThread();
            }
            else
            {
                try
                {
                    context.Post(_ =>
                    {
                        if (Thread.CurrentThread.ManagedThreadId != thread)
                        {
                            Interlocked.Exchange(ref cancellationPending, 1);
                            UIErrors.Report(new InvalidOperationException("拖放取消未派发到所属 UI 线程。"));
                            return;
                        }

                        CancelOnUIThread();
                    }, null);
                }
                catch (Exception error)
                {
                    Interlocked.Exchange(ref cancellationPending, 1);
                    UIErrors.Report(error);
                }
            }
        }

        private void CancelOnUIThread()
        {
            RequireThread();
            if (Phase == DragPhase.Dragging)
            {
                Finish(new DropResult(DropStatus.Cancelled));
            }
            // 提交阶段已释放指针，必须等业务结果确定后再进行视觉收尾。
        }

        private void Finish(DropResult result)
        {
            RequireThread();
            if (Phase == DragPhase.Completed)
            {
                return;
            }

            Phase = DragPhase.Completed;
            try
            {
                ReleaseCapture();
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }

            var callback = settleVisual;
            settleVisual = null;
            notifying = true;
            try
            {
                if (callback != null)
                {
                    callback(result);
                }
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
            finally
            {
                notifying = false;
                try
                {
                    invalidation.Dispose();
                    cancellation.Dispose();
                }
                finally
                {
                    finalResult = result;
                    finished = true;
                    owner.Remove(this);
                    if (completion != null)
                    {
                        completion.TrySetResult(result);
                    }
                }
            }
        }

        private void ReleaseCapture()
        {
            var previous = capture;
            capture = null;
            if (previous != null)
            {
                // 外部凭证释放可回调本会话；此时提交或最终收尾尚未完成。
                releasingCapture = true;
                try
                {
                    previous.Dispose();
                }
                finally
                {
                    releasingCapture = false;
                }
            }
        }

        /// <summary>
        /// 请求取消并等待最终收尾，重复调用共享清理任务。
        /// 谓词、提交和视觉回调不能等待自身会话销毁，避免形成自等待。
        /// </summary>
        public ValueTask DisposeAsync()
        {
            RequireThread();

            if (evaluating || releasingCapture || notifying ||
                (Phase == DragPhase.Completed && !finished) ||
                (executing != null && executing.Value != null && executing.Value.Active))
            {
                throw new InvalidOperationException("Drag callbacks cannot await disposal of their own session.");
            }

            if (disposal == null)
            {
                disposal = DisposeCoreAsync();
            }

            return new ValueTask(disposal);
        }

        private async Task DisposeCoreAsync()
        {
            Cancel();
            if (commitTask != null)
            {
                try
                {
                    await commitTask;
                }
                catch (Exception error)
                {
                    UIErrors.Report(error);
                }
            }

            Pump();
            await Completion;
        }

        private void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("Drag session requires its owning UI thread.");
            }
        }

        private sealed class CommitFrame
        {
            internal bool Active = true;
        }
    }
}
