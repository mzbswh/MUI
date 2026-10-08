using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MUI.Resources;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    /// <summary>在父 View 下通过统一异步协议实例化、换绑和移除本地动态道具。</summary>
    public sealed class LocalDynamicDemo : MonoBehaviour
    {
        [SerializeField] private View parentView = null;
        [SerializeField] private DynamicViewElement content = null;
        [SerializeField] private GameObject itemPrefab = null;
        private readonly LifetimeScope lifetime = new LifetimeScope();
        private int replacement;

        private async void Start()
        {
            try
            {
                if (parentView == null || content == null || itemPrefab == null)
                {
                    throw new InvalidOperationException("请配置父 View、其下的动态内容节点和 ThingItem Prefab。");
                }

                parentView.Initialize();
                parentView.SetHostState(false, false);
                // 提供方先登记，确保父子实例全部释放后再销毁其暂存节点。
                var provider = lifetime.OwnDisposable(new PrefabViewProvider(parentView.transform,
                    new[] { new KeyValuePair<ViewResource, GameObject>(new ViewResource("ThingItem"), itemPrefab) }));
                content.Configure(provider);
                parentView.BeginChildActivation(lifetime);
                lifetime.OnDispose(() =>
                {
                    if (parentView != null && parentView.IsAlive)
                    {
                        parentView.SetHostState(false, false);
                    }
                });

                content.SetContent("ThingItem", new ThingItemViewModel { Label = "动态道具 × 10" });
                await content.PendingChange;
                lifetime.Token.ThrowIfCancellationRequested();
                parentView.CommitChildActivation();
                parentView.SetHostState(true, true);
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                await ReleaseAsync();
            }
        }

        [ContextMenu("替换动态道具")]
        public void ReplaceItem() => Run(ReplaceItemAsync);

        private async Task ReplaceItemAsync()
        {
            if (Application.isPlaying && !lifetime.IsEnded && content != null)
            {
                var previousView = content.GetComponentInChildren<View>();
                content.SetContent("ThingItem", new ThingItemViewModel { Label = "第 " + (++replacement) + " 次动态替换" });
                var result = await content.PendingChange;
                lifetime.Token.ThrowIfCancellationRequested();
                var currentView = content.GetComponentInChildren<View>();
                Debug.Log($"动态更新：{result.Status}，复用实例={previousView != null && previousView == currentView}", this);
            }
        }

        [ContextMenu("清空动态道具")]
        public void ClearItem() => Run(ClearItemAsync);

        private async Task ClearItemAsync()
        {
            if (Application.isPlaying && !lifetime.IsEnded && content != null)
            {
                content.SetContent(null, null);
                var result = await content.PendingChange;
                Debug.Log("动态清空结果：" + result.Status, this);
            }
        }

        private async void Run(Func<Task> operation)
        {
            try { await operation(); }
            catch (OperationCanceledException) when (lifetime.IsEnded) { }
            catch (Exception error) { Debug.LogException(error); }
        }

        private async void OnDestroy() => await ReleaseAsync();

        private async Task ReleaseAsync()
        {
            try
            {
                await lifetime.DisposeAsync();
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }
        }
    }
}
