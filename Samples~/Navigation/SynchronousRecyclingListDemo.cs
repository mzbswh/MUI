using System;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    /// <summary>少量奖励条目的纯同步回收示例；模型借用，条目绑定和资源由父激活统一管理。</summary>
    public sealed partial class SynchronousRecyclingListDemo : MonoBehaviour
    {
        [SerializeField] private View parentView = null;
        [SerializeField] private RecyclingListElement list = null;
        private readonly Lifetime lifetime = new Lifetime(LifetimeMode.Synchronous);
        private ObservableList<ViewModel> rewards;
        private int nextReward;

        private void Start()
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
                parentView.ConfigureSynchronousResources(resourceLoader);
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
                binding.SetLifetimeMode(LifetimeMode.Synchronous);
                lifetime.OnDispose(binding.Dispose);
                binding.Bind();
                AddReward();
                AddReward();
                AddReward();
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
                Release();
            }
        }

        [ContextMenu("同步添加奖励")]
        public void AddReward()
        {
            if (!lifetime.IsEnded && rewards != null)
            {
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

        [ContextMenu("同步移除首项并保留池节点")]
        public void RemoveFirst()
        {
            if (!lifetime.IsEnded && rewards != null && rewards.Count != 0)
            {
                rewards.RemoveAt(0);
            }
        }

        private void OnDestroy() => Release();

        [ContextMenu("同步关闭奖励页并输出资源计数")]
        public void CloseExample() => Release();

        private void Release()
        {
            try
            {
                lifetime.Dispose();
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
