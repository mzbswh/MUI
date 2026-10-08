using System;
using System.Threading.Tasks;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    /// <summary>借用场景中的父 View，演示本地的静态子 View 绑定、换模型和移除。</summary>
    public sealed partial class LocalNestedDemo : MonoBehaviour
    {
        [SerializeField] private View parentView = null;
        private readonly LifetimeScope lifetime = new LifetimeScope();
        private NestedPageViewModel model;
        private int replacement;

        private async void Start()
        {
            try
            {
                if (parentView == null)
                {
                    throw new InvalidOperationException("请配置带 NestedItem 包装节点的父 View。");
                }

                parentView.Initialize();
                parentView.SetHostState(false, false);
                parentView.BeginChildActivation(lifetime);
                model = new NestedPageViewModel();
                var binding = BindingRegistry.Create(parentView, model);
                lifetime.Own(binding);
                lifetime.OnDispose(() =>
                {
                    if (parentView != null && parentView.IsAlive)
                    {
                        parentView.SetHostState(false, false);
                    }
                });

                binding.Bind();
                binding.CommitSourceWrites();
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

        [ContextMenu("更换嵌套道具模型")]
        public void ReplaceItem()
        {
            if (!Application.isPlaying || lifetime.IsEnded || model == null)
            {
                return;
            }

            model.Item = new ThingItemViewModel { Label = "第 " + (++replacement) + " 次更换道具" };
        }

        [ContextMenu("移除嵌套道具")]
        public void ClearItem()
        {
            if (!Application.isPlaying || lifetime.IsEnded || model == null)
            {
                return;
            }

            model.Item = null;
        }

        private async void OnDestroy() => await ReleaseAsync();

        private async Task ReleaseAsync()
        {
            try
            {
                await lifetime.DisposeAsync();
                model = null;
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }
        }
    }
}
