using System;
using System.Threading;

namespace MUI.DragDrop
{
    public sealed partial class DragSession<TPayload>
    {
        private readonly Lifetime sourceLifetime;
        private bool synchronousCommitting;

        /// <summary>会话沿用源生命周期的执行模式，创建后不再改变。</summary>
        public LifetimeMode Mode
        {
            get;
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

        /// <summary>仅同步模式且没有执行中的谓词、提交或视觉收尾时可同步释放。</summary>
        public bool CanDisposeSynchronously
        {
            get
            {
                RequireThread();
                return Mode == LifetimeMode.Synchronous && !evaluating && !notifying && !synchronousCommitting &&
                    Phase != DragPhase.Dropping && (Phase != DragPhase.Completed || finished);
            }
        }

        /// <summary>读取已发布结果，未完成时返回 false；不创建完成任务。</summary>
        public bool TryGetResult(out DropResult result)
        {
            RequireThread();
            result = finalResult;
            return finished;
        }

        /// <summary>直接提交并返回最终结果；重复提交读取第一次结果，不再执行业务。</summary>
        public DropResult Drop(DropTarget<TPayload> target)
        {
            RequireThread();
            if (Mode != LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("异步拖放会话请使用 DropAsync。");
            }
            RequireDropAllowed(target);
            Pump();
            if (!BeginDrop(target))
            {
                return finalResult;
            }

            DropResult result;
            synchronousCommitting = true;
            try
            {
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, target.Lifetime.Token))
                {
                    linked.Token.ThrowIfCancellationRequested();
                    // 两端都登记同步操作，业务回调退出前不能释放源或目标所持资源。
                    var accepted = sourceLifetime.Run(_ => target.Lifetime.Run(__ =>
                        target.SynchronousCommit(Payload, linked.Token)));
                    result = new DropResult(accepted ? DropStatus.Committed :
                        linked.IsCancellationRequested ? DropStatus.Cancelled : DropStatus.Rejected);
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
                synchronousCommitting = false;
            }

            Finish(result);
            return finalResult;
        }

        /// <summary>同步取消并收尾，不能从自身业务回调重入；重复释放不创建任务。</summary>
        public void Dispose()
        {
            RequireThread();
            if (!CanDisposeSynchronously)
            {
                throw new InvalidOperationException("拖放会话当前不可同步释放。");
            }
            Cancel();
        }
    }
}
