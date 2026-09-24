using System;
using System.Runtime.ExceptionServices;

namespace MUI
{
    internal sealed partial class ViewModelOwnership<TViewModel>
        where TViewModel : ViewModel
    {
        /// <summary>只检查本实例拥有的模型，借用模型不由框架释放。</summary>
        public bool CanDisposeSynchronously => disposalStarted
            ? disposalCompleted
            : !ownsModel || CanReleaseSynchronously(ownedModel);

        /// <summary>同步销毁不调用异步接口；能力不足时保留所有权和引用，供调用者处理。</summary>
        public void Dispose()
        {
            if (!CanDisposeSynchronously)
            {
                throw new InvalidOperationException("View model ownership cannot be released synchronously.");
            }

            if (disposalStarted)
            {
                ThrowDisposalFailure();
                return;
            }

            disposalStarted = true;
            var previous = ownsModel ? ownedModel : null;
            ownedModel = null;
            Model = null;
            ownsModel = false;
            try
            {
                ReleaseModel(previous);
            }
            catch (Exception error)
            {
                disposalFailure = error;
                throw;
            }
            finally
            {
                disposalCompleted = true;
                // 仅通知显式异步观察者；纯同步调用不会创建完成任务。
                if (disposal != null)
                {
                    if (disposalFailure == null)
                    {
                        disposal.TrySetResult(true);
                    }
                    else
                    {
                        disposal.TrySetException(disposalFailure);
                        _ = disposal.Task.Exception;
                    }
                }
            }
        }

        /// <summary>成功换绑后同步释放原工厂模型；只支持异步释放时先拒绝，不丢弃其所有权。</summary>
        internal void ReleaseReplacedModel()
        {
            if (ReferenceEquals(ownedModel, Model) || ownedModel == null)
            {
                return;
            }

            if (!CanReleaseSynchronously(ownedModel))
            {
                throw new InvalidOperationException("The replaced view model requires asynchronous disposal.");
            }

            var previous = ownedModel;
            ownedModel = null;
            ownsModel = false;
            ReleaseModel(previous);
        }

        private static bool CanReleaseSynchronously(TViewModel model)
        {
            return (!(model is IAsyncDisposable) || model is IDisposable)
                && (!(model is ISynchronousDisposable guarded) || guarded.CanDisposeSynchronously);
        }

        private static void ReleaseModel(TViewModel model)
        {
            if (model is IDisposable synchronous)
            {
                synchronous.Dispose();
            }
        }

        private void ThrowDisposalFailure()
        {
            if (disposalFailure != null)
            {
                ExceptionDispatchInfo.Capture(disposalFailure).Throw();
            }
        }
    }
}
