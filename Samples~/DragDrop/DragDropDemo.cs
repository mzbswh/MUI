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
        [SerializeField] private bool synchronous = true;
        private Lifetime lifetime;
        private GameObject canvasObject;
        private GameObject eventSystemObject;
        private View view;
        private Text status;
        private int committedCount;

        private void Start()
        {
            lifetime = new Lifetime(synchronous ? LifetimeMode.Synchronous : LifetimeMode.AsyncAllowed);
            try
            {
                Build();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                enabled = false;
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
            if (lifetime.Mode == LifetimeMode.Synchronous)
            {
                view.BeginChildActivation(lifetime);
                view.CommitChildActivation();
            }
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
            var destination = lifetime.Mode == LifetimeMode.Synchronous
                ? DropTarget<ItemPayload>.CreateSynchronous(lifetime, (payload, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    return Commit(payload, fail);
                }, payload => !reject && payload.Id == 7)
                : new DropTarget<ItemPayload>(lifetime, async (payload, token) =>
                {
                    SetStatus("Submitting item " + payload.Id + "...");
                    await Task.Delay(600, token);
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

        private void OnDestroy()
        {
            if (lifetime != null)
            {
                lifetime.Cancel();
            }
            try
            {
                if (view != null)
                {
                    view.Dispose();
                }
            }
            catch (Exception error)
            {
                // View 清理出错不能跳过会话、Lifetime 与示例创建的场景对象。
                Debug.LogException(error);
            }
            if (lifetime != null && lifetime.Mode == LifetimeMode.Synchronous)
            {
                try
                {
                    lifetime.Dispose();
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                }
            }
            else if (lifetime != null)
            {
                _ = CleanupAsync();
            }
            if (canvasObject != null)
            {
                Destroy(canvasObject);
            }

            if (eventSystemObject != null)
            {
                Destroy(eventSystemObject);
            }
        }

        private async Task CleanupAsync()
        {
            try
            {
                await lifetime.DisposeAsync();
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
