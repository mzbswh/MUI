using System;
using System.Threading;
using System.Threading.Tasks;
using MUI.Navigation;
using MUI.Resources;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class AsynchronousDependenciesDemo
    {
        private static Route<ThingItemViewModel, Unit, Unit> CreateDependency(string key, ViewResource resource,
                    bool fail, int prepareDelay, int cleanupDelay) =>
                    new Route<ThingItemViewModel, Unit, Unit>(key, resource, () => new ThingItemViewModel(),
                        presenterFactory: _ => new DependencyPresenter(fail, prepareDelay, cleanupDelay),
                        bindingFactory: ThingItemViewModelBindingFactory.Create,
                        preparation: PreparationMode.AsyncOnly,
                        policy: new RoutePolicy(enterHistory: false, takesFocus: false, backBehavior: BackBehavior.Ignore));

        /// <summary>仅用于演示延迟与取消；项目中应替换为实际资源后端。</summary>
        private sealed class DelayedProvider : IViewProvider, IDisposable
        {
            private readonly PrefabViewProvider inner;
            private readonly int delay;
            private readonly bool ignoreCancellation;

            internal DelayedProvider(PrefabViewProvider inner, int delay, bool ignoreCancellation)
            {
                this.inner = inner;
                this.delay = delay;
                this.ignoreCancellation = ignoreCancellation;
            }

            public SyncCreateAvailability GetSyncAvailability(ViewResource resource) => SyncCreateAvailability.Unsupported;

            public IViewLease Create(ViewResource resource) => throw new NotSupportedException("示例延迟提供方只支持异步加载。");

            public async ValueTask<IViewLease> CreateAsync(ViewResource resource, CancellationToken cancellationToken)
            {
                await Task.Delay(delay, ignoreCancellation ? CancellationToken.None : cancellationToken);
                // 故意忽略取消的模式用于观察迟到凭证仍被候选接管并回收，不用于生产后端。
                var acquired = inner.Create(resource);
                Debug.Log($"资源返回={resource}，调用令牌已取消={cancellationToken.IsCancellationRequested}");
                return acquired;
            }

            public void Dispose() => inner.Dispose();
        }

        private sealed class DependencyPresenter : Presenter<ThingItemViewModel, Unit, Unit>, IAsyncOpenPresenter<Unit>
        {
            private readonly bool fail;
            private readonly int prepareDelay;
            private readonly int cleanupDelay;

            internal DependencyPresenter(bool fail, int prepareDelay, int cleanupDelay)
            {
                this.fail = fail;
                this.prepareDelay = prepareDelay;
                this.cleanupDelay = cleanupDelay;
            }

            protected override void OnOpen(Unit args)
            {
                ViewModel.Label = fail ? "准备后故意失败" : "异步共享状态";
                Context.Lifetime.OnDisposeAsync(CleanupAsync);
            }

            public async ValueTask OnOpenAsync(Unit args, CancellationToken cancellationToken)
            {
                Debug.Log($"依赖开始异步准备，失败模式={fail}");
                try
                {
                    await Task.Delay(prepareDelay, cancellationToken);
                    if (fail)
                    {
                        throw new InvalidOperationException("示例可选依赖在异步准备后失败。");
                    }
                }
                finally
                {
                    Debug.Log($"依赖异步准备已退出，失败模式={fail}");
                }
            }

            private async ValueTask CleanupAsync()
            {
                Debug.Log($"依赖开始物理清理，失败模式={fail}");
                await Task.Delay(cleanupDelay);
                Debug.Log($"依赖物理清理完成，失败模式={fail}");
            }
        }
    }
}
