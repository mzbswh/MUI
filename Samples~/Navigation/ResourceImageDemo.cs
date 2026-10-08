using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    /// <summary>独立场景中的资源键绑定示例，自动创建展示节点，不依赖外部图片或 Prefab。</summary>
    public sealed partial class ResourceImageDemo : MonoBehaviour
    {
        [SerializeField, Min(0)] private int delayMilliseconds = 500;
        [SerializeField] private bool ignoreCancellation = true;
        [SerializeField] private bool waitForInitialResources = false;
        private LifetimeScope resources;
        private BindingContext binding;
        private ResourceImageViewModel model;
        private DemoLoader loader;
        private View view;
        private ImageElement icon;
        private RawImageElement preview;
        private TextElement fontPreview;
        private GameObject canvasObject;
        private GameObject eventSystemObject;
        private UnityEngine.UI.Text status;
        private bool closing;
        private bool alternate;
        private Task closeTask;
        private int failures;
        private string failedKey = "none";
        private bool observingErrors;

        private void Start()
        {
            if (closing)
            {
                return;
            }

            try
            {
                UIErrors.Reported += OnResourceError;
                observingErrors = true;
                CreateLayout();
                resources = new LifetimeScope();
                loader = new DemoLoader(Math.Max(0, delayMilliseconds), ignoreCancellation);
                view.ConfigureResources(loader);

                view.WaitForResourceSources = waitForInitialResources;

                // 正式导航会驱动这两个激活阶段；独立示例显式调用，复用相同控件接入路径。
                view.BeginChildActivation(resources);

                alternate = true;
                model = new ResourceImageViewModel
                {
                    IconKey = "Warm",
                    PreviewKey = "Warm",
                    FontKey = "Warm"
                };
                binding = ResourceImageViewModelBindingFactory.Create(view, model);
                binding.Bind();
                _ = CompleteOpeningAsync(resources);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Close();
            }
        }

        /// <summary>先完成隐藏准备；关闭后的旧等待不能再次显示界面。</summary>
        private async Task CompleteOpeningAsync(LifetimeScope activation)
        {
            try
            {
                await view.CompleteChildPreparationAsync(activation.Token);
                if (closing || activation.IsEnded || !ReferenceEquals(resources, activation) || view == null)
                {
                    return;
                }

                view.CommitChildActivation();
                view.SetHostState(true, true);
            }
            catch (OperationCanceledException) when (activation.IsEnded)
            {
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Close();
            }
        }

        private void Update()
        {
            if (status == null || loader == null || closing)
            {
                return;
            }

            var iconSource = icon == null ? "destroyed" : icon.SpriteSource ?? "empty";
            var previewSource = preview == null ? "destroyed" : preview.TextureSource ?? "empty";
            var fontSource = fontPreview == null ? "destroyed" : fontPreview.FontSource ?? "empty";
            status.text = "Resource loading\n" +
                $"Icon: {iconSource}   Preview: {previewSource}   Font: {fontSource}\n" +
                $"Loading: {loader.Pending}   Held resources: {loader.HeldResources}\n" +
                $"Created: {loader.Created}   Released: {loader.Released}\n" +
                $"Failures: {failures}   Last failed key: {failedKey}";
        }

        [ContextMenu("切换资源键")]
        public void NextImage()
        {
            if (closing || model == null)
            {
                return;
            }

            alternate = !alternate;
            var key = alternate ? "Warm" : "Cool";
            TryRequest(() => model.IconKey = key);
            TryRequest(() => model.PreviewKey = key);
            TryRequest(() => model.FontKey = key);
        }

        [ContextMenu("下一组加载失败")]
        public void FailNextImages()
        {
            if (closing || model == null || loader == null)
            {
                return;
            }

            loader.FailuresRemaining = 3;
            NextImage();
        }

        [ContextMenu("重试当前资源键")]
        public void RetryImages()
        {
            if (closing || model == null)
            {
                return;
            }

            TryRequest(model.RefreshIcon);
            TryRequest(model.RefreshPreview);
            TryRequest(model.RefreshFont);
        }

        [ContextMenu("清空显示")]
        public void ClearImages()
        {
            if (closing || model == null)
            {
                return;
            }

            TryRequest(() => model.IconKey = null);
            TryRequest(() => model.PreviewKey = null);
            TryRequest(() => model.FontKey = null);
        }

        private void TryRequest(Action request)
        {
            try
            {
                request();
            }
            catch (DemoLoadException error)
            {
                OnResourceError(error);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        private void OnResourceError(Exception error)
        {
            if (!(error is DemoLoadException failure) || !ReferenceEquals(failure.Owner, loader))
            {
                return;
            }

            ++failures;
            failedKey = failure.Key;
            Debug.LogWarning(failure.Message);
        }

        [ContextMenu("关闭并释放")]
        public void Close()
        {
            if (closing)
            {
                return;
            }

            closing = true;
            var errors = new List<Exception>();
            TryCleanup(() =>
            {
                if (view != null && view.IsAlive)
                {
                    view.SetHostState(false, false);
                }
            }, errors);

            closeTask = CloseAsync(errors);
        }

        private async Task CloseAsync(List<Exception> errors)
        {
            if (binding != null)
            {
                try
                {
                    await binding.UnbindAsync();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            if (resources != null)
            {
                try
                {
                    await resources.DisposeAsync();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            FinishClose(errors);
        }

        private void FinishClose(List<Exception> errors)
        {
            if (observingErrors)
            {
                UIErrors.Reported -= OnResourceError;
                observingErrors = false;
            }

            TryCleanup(() =>
            {
                if (view != null)
                {
                    view.Dispose();
                }
            }, errors);
            TryCleanup(() =>
            {
                if (canvasObject != null)
                {
                    Destroy(canvasObject);
                }
            }, errors);
            TryCleanup(() =>
            {
                if (eventSystemObject != null)
                {
                    Destroy(eventSystemObject);
                }
            }, errors);

            if (loader != null)
            {
                Debug.Log($"资源示例清理结束：在途 {loader.Pending}，持有 {loader.HeldResources}，创建 {loader.Created}，归还 {loader.Released}。");
            }

            foreach (var error in errors)
            {
                Debug.LogException(error);
            }
            binding = null;
            resources = null;
            model = null;
        }

        private static void TryCleanup(Action action, List<Exception> errors)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
        }

        private void OnDestroy()
        {
            // 应先调用 Close 等待清理；原生销毁回调不能阻塞 Unity 主线程。
            if (!closing)
            {
                Close();
            }
            if (closeTask != null && !closeTask.IsCompleted)
            {
                Debug.Log("资源示例仍在等待异步加载归还，请勿据此认为退出清理已经完成。");
            }
        }
    }
}
