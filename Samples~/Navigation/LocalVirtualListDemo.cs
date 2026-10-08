using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    /// <summary>本地的千项道具列表与多列网格，复用同一虚拟化控件。</summary>
    public sealed class LocalVirtualListDemo : MonoBehaviour
    {
        [SerializeField] private View parentView = null;
        [SerializeField] private VirtualListElement list = null;
        private readonly LifetimeScope lifetime = new LifetimeScope();
        private ObservableList<VirtualListItem> items;

        private async void Start()
        {
            try
            {
                if (parentView == null || list == null)
                {
                    throw new InvalidOperationException("请配置父 View 与其下已配置 ScrollRect 和 ThingItem 模板的虚拟列表。");
                }

                // 独立示例显式登记条目工厂，不依赖其他场景的启动代码或反射扫描。
                ThingItemViewModelBindingFactory.Register();
                parentView.Initialize();
                parentView.SetHostState(false, false);
                parentView.BeginChildActivation(lifetime);
                lifetime.OnDispose(() =>
                {
                    if (parentView != null && parentView.IsAlive)
                    {
                        parentView.SetHostState(false, false);
                    }
                });
                var values = new List<VirtualListItem>();
                for (var i = 0; i < 1000; ++i)
                {
                    values.Add(new VirtualListItem(i, new ThingItemViewModel { Label = "道具 " + i }));
                }

                items = new ObservableList<VirtualListItem>(values, itemKey: item => item.Key);
                list.Items = items;
                await parentView.CompleteChildPreparationAsync(lifetime.Token);
                parentView.CommitChildActivation();
                parentView.SetHostState(true, true);
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                await ReleaseAsync();
            }
        }

        [ContextMenu("切换列表与三列网格")]
        public void ToggleGrid()
        {
            if (!lifetime.IsEnded && list != null)
            {
                list.Columns = list.Columns == 1 ? 3 : 1;
            }
        }

        [ContextMenu("定位第 500 项")]
        public async void RevealItem()
        {
            try
            {
                if (lifetime.IsEnded || list == null)
                {
                    return;
                }

                var result = await list.ScrollToKeyAsync(500, cancellationToken: lifetime.Token);
                if (lifetime.IsEnded || list == null)
                {
                    return;
                }

                Debug.Log($"定位：{result.Status}，物化数量：{list.MaterializedCount}", this);
                if (result.Error != null)
                {
                    Debug.LogException(result.Error, this);
                }
            }
            catch (OperationCanceledException) when (lifetime.IsEnded) { }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        [ContextMenu("移除首项")]
        public void RemoveFirst()
        {
            if (!lifetime.IsEnded && items != null && items.Count != 0)
            {
                items.RemoveAt(0);
            }
        }

        private async void OnDestroy() => await ReleaseAsync();

        private async Task ReleaseAsync()
        {
            try
            {
                await lifetime.DisposeAsync();
                items = null;
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }
        }
    }
}
