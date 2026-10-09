using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MUI.ChildViews;
using MUI.Resources;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    /// <summary>
    /// 本地 Prefab 独立道具视图示例。配置 ThingItem Prefab 和挂载节点后运行，
    /// 通过组件菜单打开/关闭；通过统一子视图协议管理准备、换绑和释放。
    /// </summary>
    public sealed class LocalChildDemo : MonoBehaviour
    {
        [SerializeField] private View itemPrefab = null;
        [SerializeField] private Transform contentHost = null;
        private readonly LifetimeScope lifetime = new LifetimeScope();
        private readonly ViewResource resource = new ViewResource("LocalThingItem");
        private PrefabViewProvider provider;
        private ChildViewScope scope;
        private ChildViewTemplate<ThingItemViewModel, string> template;
        private ChildViewHandle<ThingItemViewModel, string> item;
        private bool running;

        private async void Start()
        {
            try
            {
                if (itemPrefab == null || contentHost == null)
                {
                    throw new InvalidOperationException("请配置 ThingItem Prefab 和挂载节点。");
                }

                provider = lifetime.OwnDisposable(new PrefabViewProvider(contentHost,
                    new[] { new KeyValuePair<ViewResource, GameObject>(resource, itemPrefab.gameObject) }));
                scope = new ChildViewScope(lifetime);
                template = new ChildViewTemplate<ThingItemViewModel, string>(resource,
                    () => new ThingItemViewModel { Label = "本地道具 × 10" },
                    (view, model) => BindingRegistry.Create(view, model),
                    _ => new LocalThingPresenter());
                scope.CommitActivation();
                scope.SetHostState(true, true);
                OpenItem();
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                await ReleaseAsync();
            }
        }

        [ContextMenu("本地打开道具")]
        public void OpenItem() => Run(OpenItemAsync);

        private async Task OpenItemAsync()
        {
            if (!Application.isPlaying || scope == null || !scope.IsActive || item != null)
            {
                return;
            }

            var candidate = await scope.PrepareAsync(template, provider, "本地道具 × 10", cancellationToken: lifetime.Token);
            try
            {
                candidate.Commit();
                item = candidate;
            }
            catch (Exception failure)
            {
                try
                {
                    await candidate.DisposeAsync();
                }
                catch (Exception cleanup)
                {
                    throw new AggregateException(failure, cleanup);
                }

                throw;
            }
        }

        [ContextMenu("本地更新道具参数")]
        public void UpdateItemArgs() => Run(() => ApplyItemArgsAsync("参数更新后的本地道具 × 40"));

        [ContextMenu("演示本地参数提交失败并故障关闭")]
        public void FailItemArgs() => Run(() => ApplyItemArgsAsync(LocalThingPresenter.FailingArgs));

        private async Task ApplyItemArgsAsync(string next)
        {
            if (item == null || item.State != ChildViewState.Active)
            {
                return;
            }

            try
            {
                var outcome = await item.UpdateArgsAsync(next, lifetime.Token);
                Debug.Log($"本地参数更新：{outcome.Status}/{outcome.Cleanup}，当前参数={item.Args}", this);
                if (outcome.Error != null)
                {
                    Debug.LogException(outcome.Error, this);
                }
            }
            finally
            {
                ForgetClosedItem();
            }
        }

        [ContextMenu("本地换绑原道具视图")]
        public void RebindItem() => Run(RebindItemAsync);

        private async Task RebindItemAsync()
        {
            if (item == null || item.State != ChildViewState.Active)
            {
                return;
            }

            try
            {
                var outcome = await item.RebindAsync(new ThingItemViewModel { Label = "原视图换绑的新道具 × 30" }, lifetime.Token);
                Debug.Log($"本地换绑：{outcome.Status}/{outcome.Cleanup}，视图故障={outcome.ViewFaulted}", this);
                if (outcome.Error != null)
                {
                    Debug.LogException(outcome.Error, this);
                }
            }
            finally
            {
                ForgetClosedItem();
            }
        }

        [ContextMenu("本地关闭道具")]
        public void CloseItem() => Run(CloseItemAsync);

        private async Task CloseItemAsync()
        {
            if (item == null)
            {
                return;
            }

            await item.DisposeAsync();
            item = null;
        }

        [ContextMenu("本地停用道具并保留实例")]
        public void DeactivateItem() => Run(DeactivateItemAsync);

        private async Task DeactivateItemAsync()
        {
            if (item == null || item.State != ChildViewState.Active)
            {
                return;
            }

            try
            {
                await scope.DeactivateAsync(item, lifetime.Token);
                item.ViewModel.Label = "本地恢复后显示的新数量 × 20";
            }
            finally
            {
                ForgetClosedItem();
            }
        }

        [ContextMenu("本地恢复原道具实例")]
        public void ReactivateItem() => Run(ReactivateItemAsync);

        private async Task ReactivateItemAsync()
        {
            if (item == null || item.State != ChildViewState.Inactive)
            {
                return;
            }

            try
            {
                await scope.PrepareReactivationAsync(item, lifetime.Token);
                item.Commit();
            }
            finally
            {
                ForgetClosedItem();
            }
        }

        private async void Run(Func<Task> operation)
        {
            if (running || lifetime.IsEnded)
            {
                return;
            }
            running = true;
            try
            {
                await operation();
            }
            catch (OperationCanceledException) when (lifetime.IsEnded)
            {
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }
            finally
            {
                running = false;
            }
        }

        private void ForgetClosedItem()
        {
            if (item != null && (item.State == ChildViewState.Closed || item.State == ChildViewState.Failed))
            {
                item = null;
            }
        }

        private async void OnDestroy() => await ReleaseAsync();

        private async Task ReleaseAsync()
        {
            try
            {
                // 先创建的提供方最后释放，Scope 与所有子项在此前完成清理。
                await lifetime.DisposeAsync();
                item = null;
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }
        }
    }
}
