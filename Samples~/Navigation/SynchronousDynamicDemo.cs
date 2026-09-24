using System;
using System.Collections.Generic;
using MUI.Resources;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    /// <summary>在场景父 View 下同步实例化、替换和移除动态道具，不使用异步调用。</summary>
    public sealed class SynchronousDynamicDemo : MonoBehaviour
    {
        [SerializeField] private View parentView = null;
        [SerializeField] private DynamicViewElement content = null;
        [SerializeField] private GameObject itemPrefab = null;
        private readonly Lifetime lifetime = new Lifetime(LifetimeMode.Synchronous);
        private int replacement;

        private void Start()
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
                content.ConfigureSynchronous(provider);
                parentView.BeginChildActivation(lifetime);
                lifetime.OnDispose(() =>
                {
                    if (parentView != null && parentView.IsAlive)
                    {
                        parentView.SetHostState(false, false);
                    }
                });

                content.SetContent("ThingItem", new ThingItemViewModel { Label = "动态同步道具 × 10" });
                parentView.CommitChildActivation();
                parentView.SetHostState(true, true);
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                Release();
            }
        }

        [ContextMenu("同步替换动态道具")]
        public void ReplaceItem()
        {
            if (Application.isPlaying && !lifetime.IsEnded && content != null)
            {
                var previousView = content.GetComponentInChildren<View>();
                content.SetContent("ThingItem", new ThingItemViewModel { Label = "第 " + (++replacement) + " 次动态替换" });
                var currentView = content.GetComponentInChildren<View>();
                Debug.Log($"同步动态更新：{content.SynchronousResult.Status}，复用实例={previousView != null && previousView == currentView}", this);
            }
        }

        [ContextMenu("同步清空动态道具")]
        public void ClearItem()
        {
            if (Application.isPlaying && !lifetime.IsEnded && content != null)
            {
                content.SetContent(null, null);
                Debug.Log("同步动态清空结果：" + content.SynchronousResult.Status, this);
            }
        }

        private void OnDestroy() => Release();

        private void Release()
        {
            try
            {
                lifetime.Dispose();
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }
        }
    }
}
