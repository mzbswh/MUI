using System;
using System.Collections.Generic;
using MUI.ChildViews;
using MUI.Resources;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    /// <summary>
    /// 完全同步的独立道具视图示例。配置 ThingItem Prefab 和挂载节点后运行，
    /// 通过组件菜单打开/关闭；不使用导航、异步方法或异步资源加载器。
    /// </summary>
    public sealed class SynchronousChildDemo : MonoBehaviour
    {
        [SerializeField] private View itemPrefab = null;
        [SerializeField] private Transform contentHost = null;
        private readonly Lifetime lifetime = new Lifetime(LifetimeMode.Synchronous);
        private readonly ViewResource resource = new ViewResource("SynchronousThingItem");
        private PrefabViewProvider provider;
        private ChildViewScope scope;
        private ChildViewTemplate<ThingItemViewModel, string> template;
        private ChildViewHandle<ThingItemViewModel, string> item;

        private void Start()
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
                    () => new ThingItemViewModel { Label = "同步道具 × 10" },
                    (view, model) => BindingRegistry.Create(view, model),
                    _ => new SynchronousThingPresenter(),
                    supportsSynchronousLifecycle: true);
                scope.CommitActivation();
                scope.SetHostState(true, true);
                OpenItem();
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                Release();
            }
        }

        [ContextMenu("同步打开道具")]
        public void OpenItem()
        {
            if (!Application.isPlaying || scope == null || !scope.IsActive || item != null)
            {
                return;
            }

            var candidate = scope.PrepareSynchronous(template, provider, "同步道具 × 10");
            try
            {
                candidate.Commit();
                item = candidate;
            }
            catch (Exception failure)
            {
                try
                {
                    candidate.Dispose();
                }
                catch (Exception cleanup)
                {
                    throw new AggregateException(failure, cleanup);
                }

                throw;
            }
        }

        [ContextMenu("同步更新道具参数")]
        public void UpdateItemArgs() => ApplyItemArgs("参数更新后的同步道具 × 40");

        [ContextMenu("演示同步参数提交失败并回滚")]
        public void FailItemArgs() => ApplyItemArgs(SynchronousThingPresenter.FailingArgs);

        private void ApplyItemArgs(string next)
        {
            if (item == null || item.State != ChildViewState.Active)
            {
                return;
            }

            try
            {
                var outcome = item.UpdateArgs(next);
                Debug.Log($"同步参数更新：{outcome.Status}/{outcome.Cleanup}，当前参数={item.Args}", this);
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

        [ContextMenu("同步换绑原道具视图")]
        public void RebindItem()
        {
            if (item == null || item.State != ChildViewState.Active)
            {
                return;
            }

            try
            {
                var outcome = item.Rebind(new ThingItemViewModel { Label = "原视图同步换绑的新道具 × 30" });
                Debug.Log($"同步换绑：{outcome.Status}/{outcome.Cleanup}，恢复失败={outcome.RecoveryFailed}", this);
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

        [ContextMenu("同步关闭道具")]
        public void CloseItem()
        {
            if (item == null)
            {
                return;
            }

            item.Dispose();
            item = null;
        }

        [ContextMenu("同步停用道具并保留实例")]
        public void DeactivateItem()
        {
            if (item == null || item.State != ChildViewState.Active)
            {
                return;
            }

            try
            {
                scope.Deactivate(item);
                item.ViewModel.Label = "同步恢复后显示的新数量 × 20";
            }
            finally
            {
                ForgetClosedItem();
            }
        }

        [ContextMenu("同步恢复原道具实例")]
        public void ReactivateItem()
        {
            if (item == null || item.State != ChildViewState.Inactive)
            {
                return;
            }

            try
            {
                scope.PrepareReactivation(item);
                item.Commit();
            }
            finally
            {
                ForgetClosedItem();
            }
        }

        private void ForgetClosedItem()
        {
            if (item != null && (item.State == ChildViewState.Closed || item.State == ChildViewState.Failed))
            {
                item = null;
            }
        }

        private void OnDestroy() => Release();

        private void Release()
        {
            try
            {
                // 先创建的提供方最后释放，Scope 与所有子项在此前同步完成清理。
                lifetime.Dispose();
                item = null;
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }
        }
    }
}
