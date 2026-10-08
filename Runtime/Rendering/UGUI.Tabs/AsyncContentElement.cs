using System;
using System.ComponentModel;
using System.Threading.Tasks;
using MUI.Resources;
using MUI.Tabs;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>只负责内容区加载和错误表现；TabBar 必须位于独立的兄弟区域。</summary>
    [DisallowMultipleComponent]
    public sealed class AsyncContentElement : Element, IElementBoundary
    {
        [SerializeField]
        private Transform contentRoot;
        [SerializeField]
        private GameObject loadingOverlay;
        [SerializeField]
        private Text errorLabel;
        [SerializeField]
        private Button retryButton;
        [SerializeField]
        private string failureMessage = "Could not load this tab. Please retry.";
        private TabViewModel model;
        private TabContentController controller;
        private int renderVersion;

        public event Action RetryRequested;

        public TabViewModel ViewModel
        {
            get => model;
            set
            {
                RequireAlive();
                // 直接换模型不能保留旧控制器，否则显示状态与点击请求会指向不同页面。
                if (controller != null && !ReferenceEquals(controller.ViewModel, value))
                {
                    controller = null;
                }

                if (ReferenceEquals(model, value))
                {
                    return;
                }

                if (model != null)
                {
                    model.PropertyChanged -= OnStateChanged;
                }

                model = value;
                if (model != null)
                {
                    model.PropertyChanged += OnStateChanged;
                }

                Render();
            }
        }

        public void Bind(TabContentController value)
        {
            RequireAlive();
            controller = value;
            ViewModel = value == null ? null : value.ViewModel;
        }

        public IViewProvider CreateProvider(IViewProvider provider)
        {
            Initialize();
            return new ContentViewProvider(provider, contentRoot, constrainSorting: true);
        }

        protected override void OnInitialize()
        {
            if (contentRoot == null)
            {
                contentRoot = transform.Find("ContentHost");
            }

            if (loadingOverlay == null)
            {
                var child = transform.Find("LoadingOverlay");
                if (child != null)
                {
                    loadingOverlay = child.gameObject;
                }
            }

            if (errorLabel == null)
            {
                var child = transform.Find("ErrorOverlay");
                if (child != null)
                {
                    errorLabel = child.GetComponent<Text>();
                }
            }

            if (retryButton == null)
            {
                var child = transform.Find("Retry");
                if (child != null)
                {
                    retryButton = child.GetComponent<Button>();
                }
            }

            if (contentRoot == null || contentRoot == transform || !contentRoot.IsChildOf(transform))
            {
                throw new InvalidOperationException("AsyncContentElement requires a descendant ContentHost.");
            }

            ValidateTabBarOrder();
            foreach (var canvas in GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.overrideSorting)
                {
                    throw new InvalidOperationException("Content region Canvas cannot override TabBar sorting.");
                }
            }

            ValidateOverlay(loadingOverlay);
            ValidateOverlay(errorLabel == null ? null : errorLabel.gameObject);
            ValidateOverlay(retryButton == null ? null : retryButton.gameObject);
            UnityAction retry = () =>
            {
                if (!CanReceiveInput(retryButton))
                {
                    return;
                }

                if (controller != null)
                {
                    _ = ObserveAsync(controller.RetryAsync());
                }
                else
                {
                    RetryRequested?.Invoke();
                }
            };
            if (retryButton != null)
            {
                retryButton.onClick.AddListener(retry);
            }

            OnDispose(() =>
            {
                if (model != null)
                {
                    model.PropertyChanged -= OnStateChanged;
                }

                if (retryButton != null)
                {
                    retryButton.onClick.RemoveListener(retry);
                }

                model = null;
                controller = null;
                RetryRequested = null;
            });
            Render();
        }

        private void ValidateOverlay(GameObject overlay)
        {
            if (overlay != null && (!overlay.transform.IsChildOf(transform) || overlay.transform == transform || overlay.transform.IsChildOf(contentRoot) || contentRoot.IsChildOf(overlay.transform)))
            {
                throw new InvalidOperationException("Loading/error controls must be siblings of content inside the content region.");
            }
        }

        private void ValidateTabBarOrder()
        {
            var parent = transform.parent;
            if (parent == null)
            {
                return;
            }

            foreach (Transform sibling in parent)
            {
                if (sibling.GetComponent<TabBarElement>() != null && sibling.GetSiblingIndex() <= transform.GetSiblingIndex())
                {
                    throw new InvalidOperationException("TabBar must follow its sibling content region in Canvas order.");
                }
            }
        }

        private void OnStateChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(TabViewModel.Snapshot) || string.IsNullOrEmpty(args.PropertyName))
            {
                Render();
            }
        }

        protected override void OnVisualRetentionEnded()
        {
            Render();
        }

        private void Render()
        {
            var version = unchecked(++renderVersion);
            if (!IsAlive || IsVisualRetentionActive)
            {
                return;
            }

            var snapshot = model == null ? null : model.Snapshot;
            var failed = snapshot != null && snapshot.Phase == TabPhase.Error;
            if (loadingOverlay != null)
            {
                loadingOverlay.SetActive(snapshot != null && snapshot.LoadingIndicatorVisible);
                if (!IsCurrentRender(version))
                {
                    return;
                }
            }

            if (errorLabel != null)
            {
                errorLabel.gameObject.SetActive(failed);
                if (!IsCurrentRender(version))
                {
                    return;
                }

                // 启停回调可能销毁文字组件，即使内容区本身仍然存活。
                if (failed && errorLabel != null)
                {
                    errorLabel.text = failureMessage;
                    if (!IsCurrentRender(version))
                    {
                        return;
                    }
                }
            }

            if (retryButton != null)
            {
                retryButton.gameObject.SetActive(failed);
            }
        }

        // 原生启停和文字更新会触发用户回调；新渲染开始后，旧渲染必须停止写入。
        private bool IsCurrentRender(int version)
        {
            return this != null && IsAlive && !IsVisualRetentionActive && version == renderVersion;
        }

        private static async Task ObserveAsync(ValueTask<TabSelectionResult> selection)
        {
            try
            {
                await selection;
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }
    }
}
