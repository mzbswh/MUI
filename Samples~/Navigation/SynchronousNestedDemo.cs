using System;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    /// <summary>借用场景中的父 View，演示完全同步的静态子 View 绑定、换模型和移除。</summary>
    public sealed partial class SynchronousNestedDemo : MonoBehaviour
    {
        [SerializeField] private View parentView = null;
        private readonly Lifetime lifetime = new Lifetime(LifetimeMode.Synchronous);
        private SynchronousNestedViewModel model;
        private int replacement;

        private void Start()
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
                model = new SynchronousNestedViewModel();
                var binding = BindingRegistry.Create(parentView, model);
                binding.SetLifetimeMode(LifetimeMode.Synchronous);
                lifetime.OwnDisposable(binding);
                lifetime.OnDispose(() =>
                {
                    if (parentView != null && parentView.IsAlive)
                    {
                        parentView.SetHostState(false, false);
                    }
                });

                binding.Bind();
                binding.CommitSourceWrites();
                parentView.CommitChildActivation();
                parentView.SetHostState(true, true);
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                Release();
            }
        }

        [ContextMenu("同步更换嵌套道具模型")]
        public void ReplaceItem()
        {
            if (!Application.isPlaying || lifetime.IsEnded || model == null)
            {
                return;
            }

            model.Item = new ThingItemViewModel { Label = "第 " + (++replacement) + " 次同步更换道具" };
        }

        [ContextMenu("同步移除嵌套道具")]
        public void ClearItem()
        {
            if (!Application.isPlaying || lifetime.IsEnded || model == null)
            {
                return;
            }

            model.Item = null;
        }

        private void OnDestroy() => Release();

        private void Release()
        {
            try
            {
                lifetime.Dispose();
                model = null;
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }
        }
    }
}
