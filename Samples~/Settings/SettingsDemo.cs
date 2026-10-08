using System;
using System.Threading;
using System.Threading.Tasks;
using MUI.UGUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MUI.Samples.Settings
{
    /// <summary>添加到空场景中的空 GameObject，示例会自行构建 UI。</summary>
    public sealed partial class SettingsDemo : MonoBehaviour
    {
        [SerializeField] private bool automaticWalkthrough;
        private readonly CancellationTokenSource walkthroughCancellation = new CancellationTokenSource();
        private SettingsViewModel model;
        private GameObject canvasObject;
        private GameObject eventSystemObject;
        private View view;
        private BindingContext binding;

        public bool AutomaticWalkthrough
        {
            get => automaticWalkthrough; set => automaticWalkthrough = value;
        }

        private void Start()
        {
            UIErrors.Reported += Debug.LogException;
            canvasObject = new GameObject("MUI Settings", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(960, 540);
            if (EventSystem.current == null)
            {
                eventSystemObject = new GameObject("MUI EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            var root = Node("SettingsView", canvasObject.transform, new Vector2(560, 380), Vector2.zero);
            root.gameObject.SetActive(false);
            root.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.15f, 0.2f);
            var label = Node("Status", root, new Vector2(520, 50), new Vector2(0, 130));
            var text = label.gameObject.AddComponent<Text>();
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 22;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            label.gameObject.AddComponent<TextElement>();

            var sliderRoot = Node("Volume", root, new Vector2(400, 36), new Vector2(0, 55));
            sliderRoot.gameObject.AddComponent<Image>().color = new Color(0.24f, 0.28f, 0.34f);
            var slider = sliderRoot.gameObject.AddComponent<Slider>();
            slider.minValue = 0;
            slider.maxValue = 1;
            var handle = Node("Handle", sliderRoot, new Vector2(28, 44), Vector2.zero);
            var graphic = handle.gameObject.AddComponent<Image>();
            graphic.color = new Color(0.3f, 0.75f, 1f);
            slider.handleRect = handle;
            slider.targetGraphic = graphic;
            sliderRoot.gameObject.AddComponent<SliderElement>();
            AddNameInput(root);
            AddButton("ToggleNameLock", "Lock / Unlock", root, new Vector2(0, -85));
            AddButton("Save", "Save", root, new Vector2(-110, -145));
            AddButton("Reset", "Reset", root, new Vector2(110, -145));
            view = root.gameObject.AddComponent<View>();
            view.Initialize();
            Generated.SettingsBindings.Initialize();
            model = new SettingsViewModel();
            binding = BindingRegistry.Create(view, model);
            binding.Bind();
            binding.CommitSourceWrites();
            view.SetHostState(true, true);
            root.gameObject.SetActive(true);
            Debug.Log($"MUI Settings ready: {model.Status}");
            if (automaticWalkthrough)
            {
                _ = WalkthroughAsync();
            }
        }

        private async Task WalkthroughAsync()
        {
            var token = walkthroughCancellation.Token;
            try
            {
                await Task.Delay(150, token);
                if (view == null)
                {
                    return;
                }

                view.GetElement<SliderElement>("Volume").GetComponent<Slider>().value = 0.8f;
                Debug.Log($"MUI Settings slider: {model.Status}");
                view.GetElement<ButtonElement>("Save").GetComponent<Button>().onClick.Invoke();
                await Task.Delay(500, token);
                if (view == null)
                {
                    return;
                }

                Debug.Log($"MUI Settings save: {model.Status}; executing={model.SaveCommand.IsExecuting}");
                view.GetElement<ButtonElement>("Reset").GetComponent<Button>().onClick.Invoke();
                Debug.Log($"MUI Settings reset: {model.Status}");
                if (Application.isBatchMode)
                {
                    view.GetElement<ButtonElement>("Save").GetComponent<Button>().onClick.Invoke();
                    Debug.Log($"MUI Settings closing while saving: executing={model.SaveCommand.IsExecuting}");
                    Destroy(gameObject);
                }
            }
            catch (OperationCanceledException)
            {
                // 演示被关闭时取消在途流程，由退出逻辑继续清理。
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        private static void AddButton(string name, string caption, Transform parent, Vector2 position)
        {
            var node = Node(name, parent, new Vector2(180, 45), position);
            var image = node.gameObject.AddComponent<Image>();
            image.color = new Color(0.2f, 0.4f, 0.6f);
            var button = node.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            node.gameObject.AddComponent<ButtonElement>();
            var label = Node("Label", node, new Vector2(170, 40), Vector2.zero).gameObject.AddComponent<Text>();
            label.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 22;
            label.text = caption;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
        }

        private static RectTransform Node(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var node = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            node.SetParent(parent, false);
            node.anchorMin = node.anchorMax = new Vector2(0.5f, 0.5f);
            node.sizeDelta = size;
            node.anchoredPosition = position;
            return node;
        }

        private void OnDestroy()
        {
            walkthroughCancellation.Cancel();
            walkthroughCancellation.Dispose();
            if (binding != null)
            {
                try
                {
                    var completion = binding.UnbindAsync();
                    _ = ObserveCleanupAsync(completion);
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                    UIErrors.Reported -= Debug.LogException;
                }
            }
            else
            {
                UIErrors.Reported -= Debug.LogException;
            }

            if (view != null)
            {
                view.Dispose();
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

        private async Task ObserveCleanupAsync(ValueTask completion)
        {
            try
            {
                await completion;
                Debug.Log($"MUI Settings binding cleanup: state={binding.State}; saveExecuting={model.SaveCommand.IsExecuting}");
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
            finally { UIErrors.Reported -= Debug.LogException; }
        }
    }
}
