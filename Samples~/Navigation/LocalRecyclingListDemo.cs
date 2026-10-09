using System;
using System.Threading.Tasks;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    /// <summary>少量奖励条目的本地回收示例；模型借用，条目绑定和资源由父激活统一管理。</summary>
    public sealed partial class LocalRecyclingListDemo : MonoBehaviour
    {
        [SerializeField] private View parentView = null;
        [SerializeField] private RecyclingListElement list = null;
        [SerializeField] private bool useFixedSlots;
        private readonly LifetimeScope lifetime = new LifetimeScope();
        private ObservableList<ViewModel> rewards;
        private int nextReward;
        private Task releaseTask;

        /// <summary>场景启动前选择五槽挂点演示；手工配置的场景应直接提供 SlotListElement 引用。</summary>
        public void ConfigureFixedSlots()
        {
            if (parentView != null || list != null || rewards != null || lifetime.IsEnded)
            {
                throw new InvalidOperationException("Choose the fixed-slot demo before its generated scene starts.");
            }

            useFixedSlots = true;
        }

        private async void Start()
        {
            if (lifetime.IsEnded)
            {
                return;
            }

            try
            {
                if (parentView == null && list == null)
                {
                    BuildExample();
                }

                if (parentView == null || list == null)
                {
                    throw new InvalidOperationException("请配置父 View、回收列表和 RewardItem 的嵌套视图模板。");
                }

                parentView.Initialize();
                // 只配置父 View；克隆的奖励子 View 默认继承加载器，并独立持有自己的图标和字体凭证。
                parentView.ConfigureResources(resourceLoader);
                parentView.SetHostState(false, false);
                parentView.BeginChildActivation(lifetime);
                lifetime.OnDispose(() =>
                {
                    if (parentView != null && parentView.IsAlive)
                    {
                        parentView.SetHostState(false, false);
                    }
                });
                rewards = new ObservableList<ViewModel>();
                RewardsViewModelBindingFactory.Register();
                RewardItemViewModelBindingFactory.Register();
                var model = new RewardsViewModel { Rewards = rewards };
                var binding = BindingRegistry.Create(parentView, model);
                lifetime.Own(binding);
                binding.Bind();
                AddReward();
                AddReward();
                AddReward();
                await parentView.CompleteChildPreparationAsync(lifetime.Token);
                parentView.CommitChildActivation();
                binding.CommitSourceWrites();
                parentView.SetHostState(true, true);
                if (generatedCanvas != null)
                {
                    generatedCanvas.SetActive(true);
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                await ReleaseAsync();
            }
        }

        [ContextMenu("添加奖励")]
        public void AddReward()
        {
            if (!lifetime.IsEnded && rewards != null)
            {
                if (list is SlotListElement slots && rewards.Count >= slots.Capacity)
                {
                    if (resourceStatus != null)
                    {
                        resourceStatus.text = "Fixed slot capacity reached";
                    }

                    return;
                }

                rewards.Add(new RewardItemViewModel
                {
                    Label = "Reward " + ++nextReward,
                    IconKey = nextReward % 2 == 0 ? "Cool" : "Warm",
                    RemoveRequested = RemoveReward
                });
            }
        }

        private void RemoveReward(RewardItemViewModel item)
        {
            if (lifetime.IsEnded || rewards == null)
            {
                return;
            }

            for (var index = 0; index < rewards.Count; ++index)
            {
                if (ReferenceEquals(rewards[index], item))
                {
                    rewards.RemoveAt(index);
                    return;
                }
            }
        }

        [ContextMenu("移除首项并保留池节点")]
        public void RemoveFirst()
        {
            if (!lifetime.IsEnded && rewards != null && rewards.Count != 0)
            {
                rewards.RemoveAt(0);
            }
        }

        private async void OnDestroy() => await ReleaseAsync();

        // 停止播放时原生对象的销毁顺序不确定；先让拥有者启动清理，View 兜底才能观察同一任务。
        private void OnApplicationQuit() => _ = ReleaseAsync();

        [ContextMenu("关闭奖励页并输出资源计数")]
        public async void CloseExample() => await ReleaseAsync();

        private Task ReleaseAsync() => releaseTask ?? (releaseTask = ReleaseCoreAsync());

        private async Task ReleaseCoreAsync()
        {
            try
            {
                await lifetime.DisposeAsync();
                LogResourceCounts();
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }
            finally
            {
                rewards = null;
            }
        }
    }
}
