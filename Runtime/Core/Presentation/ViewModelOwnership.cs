using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>
    /// 单个视图内容的模型所有权。外部模型只借用，工厂模型由实例负责释放。
    /// 宿主必须串行执行创建与销毁，不能在模型工厂返回前释放此对象。
    /// </summary>
    internal sealed class ViewModelOwnership<TViewModel> : IAsyncDisposable, ICleanupResponsibilitySource
        where TViewModel : ViewModel
    {
        private bool ownsModel;
        private bool initialized;
        private TViewModel ownedModel;
        private bool disposalStarted;
        private CleanupResponsibility modelRelease;
        private readonly List<CleanupResponsibility> retiredModels = new List<CleanupResponsibility>();

        public ViewModelOwnership()
        {
            CleanupResponsibility = new CleanupResponsibility(ReleaseOwnedModelAsync,
                "ViewModelOwnership<" + typeof(TViewModel).Name + ">", true, Thread.CurrentThread.ManagedThreadId);
        }

        public CleanupResponsibility CleanupResponsibility
        {
            get;
        }

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

        /// <summary>切换借用引用，原工厂模型保留到换绑提交清理或故障关闭结束。</summary>
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
            var responsibility = CreateModelRelease(previous);
            ownedModel = null;
            ownsModel = false;
            if (responsibility == null)
            {
                return default;
            }
            retiredModels.Add(responsibility);
            return responsibility.DisposeAsync();
        }

        /// <summary>
        /// 释放入口共享完成结果，即使释放失败也不重复调用模型。
        /// 清除业务借用引用，但失败仍由责任持有模型；同时支持两种接口时优先异步释放。
        /// </summary>
        public ValueTask DisposeAsync()
        {
            if (!disposalStarted)
            {
                disposalStarted = true;
                Model = null;
                ownsModel = false;
            }
            return CleanupResponsibility.DisposeAsync();
        }

        private async ValueTask ReleaseOwnedModelAsync()
        {
            foreach (var retired in retiredModels)
            {
                var snapshot = retired.CaptureSnapshot();
                if (snapshot.State != CleanupResponsibilityState.Completed)
                {
                    throw snapshot.Failure ?? new InvalidOperationException("Retired ViewModel cleanup is not confirmed.");
                }
            }
            if (modelRelease == null)
            {
                modelRelease = CreateModelRelease(ownedModel);
                if (modelRelease != null)
                {
                    await modelRelease.DisposeAsync();
                }
            }
            else if (modelRelease.CaptureSnapshot().State != CleanupResponsibilityState.Completed)
            {
                // 只核对已经显式处理的子责任，不重复调用未知模型的释放回调。
                throw modelRelease.CaptureSnapshot().Failure ?? new InvalidOperationException("ViewModel cleanup is not confirmed.");
            }
            ownedModel = null;
            modelRelease = null;
            retiredModels.Clear();
        }

        private static CleanupResponsibility CreateModelRelease(TViewModel model)
        {
            if (model is IAsyncDisposable asynchronous)
            {
                return CleanupRegistry.GetResponsibility(asynchronous, "ViewModel<" + typeof(TViewModel).Name + ">");
            }
            else if (model is IDisposable synchronous)
            {
                return new CleanupResponsibility(() =>
                {
                    synchronous.Dispose();
                    return default;
                }, "ViewModel<" + typeof(TViewModel).Name + ">");
            }
            return null;
        }
    }
}
