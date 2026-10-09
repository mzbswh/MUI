using System;
using System.Threading.Tasks;
using MUI.DragDrop;
using MUI.UGUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MUI.Samples.DragDrop
{
    /// <summary>交互示例，添加到启用旧版输入模块的空场景中。</summary>
    public sealed class DragDropDemo : MonoBehaviour
    {
        [SerializeField] private bool delayCommit = false;
        private LifetimeScope lifetime;
        private GameObject canvasObject;
        private GameObject eventSystemObject;
        private View view;
        private Text status;
        private int committedCount;
        private Task shutdown;
        private bool shutdownObserved;

        private void Start()
        {
            if (shutdown != null)
            {
                return;
            }

            lifetime = new LifetimeScope();
            try
            {
                Build();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                enabled = false;
                _ = ObserveShutdownAsync();
            }
        }

        private void Build()
        {
            canvasObject = new GameObject("MUI DragDrop", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            if (EventSystem.current == null)
            {
                eventSystemObject = new GameObject("MUI EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            var page = Node("DragDropView", canvasObject.transform, new Vector2(900, 480), Vector2.zero);
            page.gameObject.SetActive(false);
            page.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.12f, 0.16f);
            view = page.gameObject.AddComponent<View>();
            Label("Instructions", page, "Drag the gold item onto a target. Drop outside to cancel.", new Vector2(820, 50), new Vector2(0, 190));
            status = Label("Status", page, "Ready", new Vector2(820, 60), new Vector2(0, -190));
            var item = Node("Item", page, new Vector2(88, 88), new Vector2(0, 85));
            var icon = item.gameObject.AddComponent<Image>();
            icon.color = new Color(1, 0.75f, 0.2f);
            var source = item.gameObject.AddComponent<DragSourceElement>();
            source.AccessibilityLabel = "Gold item";
            source.Finished += result => SetStatus(result.Status + "; committed moves: " + committedCount);

            AddTarget(page, "Accept", new Vector2(-280, -60), false, false);
            AddTarget(page, "Reject", new Vector2(0, -60), true, false);
            AddTarget(page, "Fail", new Vector2(280, -60), false, true);
            var ghostRoot = Node("GhostRoot", page, new Vector2(900, 480), Vector2.zero);
            source.ConfigureGhost(icon, ghostRoot);
            source.Source = new DragBinding<ItemPayload>(lifetime, () => new ItemPayload(7));
            view.Initialize();
            view.BeginChildActivation(lifetime);
            view.CommitChildActivation();
            page.gameObject.SetActive(true);
            view.SetHostState(true, true);
        }

        private void AddTarget(RectTransform parent, string name, Vector2 position, bool reject, bool fail)
        {
            var rect = Node(name, parent, new Vector2(240, 110), position);
            var image = rect.gameObject.AddComponent<Image>();
            var idle = new Color(0.22f, 0.26f, 0.32f);
            image.color = idle;
            var target = rect.gameObject.AddComponent<DropTargetElement>();
            target.AccessibilityLabel = name + " target";
            Label(name + "Label", rect, name, new Vector2(220, 50), Vector2.zero);
            target.PropertyChanged += (_, args) =>
            {
                if (image != null && target.IsAlive && args.PropertyName == nameof(DropTargetElement.IsDropAllowed))
                {
                    image.color = target.IsDropAllowed ? new Color(0.2f, 0.65f, 0.4f) : idle;
                }
            };
            var destination = new DropTarget<ItemPayload>(lifetime, async (payload, token) =>
                {
                    SetStatus("Submitting item " + payload.Id + "...");
                    if (delayCommit)
                    {
                        await Task.Delay(600, token);
                    }
                    token.ThrowIfCancellationRequested();
                    return Commit(payload, fail);
                }, payload => !reject && payload.Id == 7);
            target.Target = new DropBinding<ItemPayload>(destination);
        }

        private bool Commit(ItemPayload payload, bool fail)
        {
            if (fail)
            {
                throw new InvalidOperationException("示例提交失败，没有移动道具。");
            }
            ++committedCount;
            SetStatus("Moved item " + payload.Id);
            return true;
        }

        private void SetStatus(string value)
        {
            if (this != null && lifetime != null && !lifetime.IsEnded && status != null)
            {
                status.text = value;
            }
        }

        private static RectTransform Node(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static Text Label(string name, Transform parent, string content, Vector2 size, Vector2 position)
        {
            var label = Node(name, parent, size, position).gameObject.AddComponent<Text>();
            label.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 22;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = content;
            label.raycastTarget = false;
            return label;
        }

        private void OnDisable()
        {
            if (lifetime != null)
            {
                lifetime.Cancel();
            }
            if (view != null && view.IsAlive)
            {
                view.SetHostState(false, false);
            }
        }

        // 停止播放时原生对象销毁顺序不确定，先由激活拥有者启动同一次清理。
        private void OnApplicationQuit() => _ = ObserveShutdownAsync();

        private void OnDestroy() => _ = ObserveShutdownAsync();

        /// <summary>场景切换前等待会话和激活收尾，再归还 View 及示例创建的对象。</summary>
        public Task ShutdownAsync()
        {
            if (shutdown != null)
            {
                return shutdown;
            }

            // 在取消和原生销毁回调之前发布任务，重入及重复退出共享同一结果。
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            shutdown = completion.Task;
            _ = ShutdownCoreAsync(completion);
            return shutdown;
        }

        private async Task ShutdownCoreAsync(TaskCompletionSource<bool> completion)
        {
            try
            {
                if (view != null && view.IsAlive)
                {
                    view.SetHostState(false, false);
                }

                if (lifetime != null)
                {
                    await lifetime.DisposeAsync();
                }

                if (view != null)
                {
                    await view.DisposeAsync();
                }

                if (canvasObject != null)
                {
                    Destroy(canvasObject);
                }

                if (eventSystemObject != null)
                {
                    Destroy(eventSystemObject);
                }

                completion.TrySetResult(true);
            }
            catch (Exception error)
            {
                // 未确认的激活或 View 继续持有原生对象，不用销毁掩盖释放失败。
                completion.TrySetException(error);
            }
        }

        private async Task ObserveShutdownAsync()
        {
            if (shutdownObserved)
            {
                return;
            }

            shutdownObserved = true;
            try
            {
                await ShutdownAsync();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        private readonly struct ItemPayload
        {
            internal ItemPayload(int id)
            {
                Id = id;
            }

            internal int Id
            {
                get;
            }
        }
    }
}
