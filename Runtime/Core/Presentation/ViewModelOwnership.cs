using System;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>
    /// 单个视图内容的模型所有权。外部模型只借用，工厂模型由实例负责释放。
    /// 宿主必须串行执行创建与销毁，不能在模型工厂返回前释放此对象。
    /// </summary>
    internal sealed partial class ViewModelOwnership<TViewModel> : ISynchronousDisposable, IAsyncDisposable
        where TViewModel : ViewModel
    {
        private bool ownsModel;
        private bool initialized;
        private TViewModel ownedModel;
        private TaskCompletionSource<bool> disposal;
        private Exception disposalFailure;
        private bool disposalStarted;
        private bool disposalCompleted;

        internal TViewModel Model
        {
            get; private set;
        }

        internal bool OwnsCurrentModel => ownsModel && ReferenceEquals(ownedModel, Model);

        /// <summary>在调用工厂前登记本所有权对象；工厂返回后先接管模型，再由宿主检查取消。</summary>
        internal void Initialize(TViewModel assignedModel, Func<TViewModel> factory)
        {
            if (initialized || disposalStarted)
            {
                throw new InvalidOperationException("View model ownership has already been initialized or released.");
            }

            initialized = true;
            ownsModel = assignedModel == null;
            Model = assignedModel ?? factory();
            if (Model == null)
            {
                throw new InvalidOperationException("ViewModel factory returned null.");
            }

            ownedModel = ownsModel ? Model : null;
        }

        /// <summary>切换借用引用，原工厂模型保留到换绑提交或失败恢复结束。</summary>
        internal void SetModel(TViewModel model)
        {
            if (!initialized || disposalStarted)
            {
                throw new InvalidOperationException("View model ownership is not active.");
            }

            Model = model ?? throw new ArgumentNullException(nameof(model));
        }

        /// <summary>成功换绑后释放退役工厂模型；宿主必须等待释放后才能销毁实例。</summary>
        internal ValueTask ReleaseReplacedModelAsync()
        {
            if (ReferenceEquals(ownedModel, Model) || ownedModel == null)
            {
                return default;
            }

            var previous = ownedModel;
            ownedModel = null;
            ownsModel = false;
            return ReleaseModelAsync(previous);
        }

        /// <summary>
        /// 释放入口共享完成结果，即使释放失败也不重复调用模型。
        /// 本异步入口清除持有引用，同时支持两种接口时优先调用异步释放。
        /// </summary>
        public ValueTask DisposeAsync()
        {
            if (disposal != null)
            {
                return new ValueTask(disposal.Task);
            }

            if (disposalStarted && disposalCompleted)
            {
                return disposalFailure == null ? default : new ValueTask(Task.FromException(disposalFailure));
            }

            disposal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            // 同步释放回调显式请求兼容信号时，只观察已有释放，不重复调用模型。
            if (disposalStarted)
            {
                return new ValueTask(disposal.Task);
            }
            disposalStarted = true;
            var previous = ownedModel;
            ownedModel = null;
            Model = null;
            var release = ownsModel;
            ownsModel = false;
            _ = ReleaseAsync(previous, release, disposal);
            return new ValueTask(disposal.Task);
        }

        private async Task ReleaseAsync(TViewModel model, bool owned, TaskCompletionSource<bool> completion)
        {
            try
            {
                if (owned)
                {
                    await ReleaseModelAsync(model);
                }

                disposalCompleted = true;
                completion.TrySetResult(true);
            }
            catch (Exception error)
            {
                disposalFailure = error;
                disposalCompleted = true;
                completion.TrySetException(error);
            }
        }

        private static async ValueTask ReleaseModelAsync(TViewModel model)
        {
            if (model is IAsyncDisposable asynchronous)
            {
                await asynchronous.DisposeAsync();
            }
            else if (model is IDisposable synchronous)
            {
                synchronous.Dispose();
            }
        }
    }
}
