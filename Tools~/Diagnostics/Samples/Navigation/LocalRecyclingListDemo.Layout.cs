using MUI.UGUI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MUI.Samples.Navigation
{
    public sealed partial class LocalRecyclingListDemo
    {
        private GameObject generatedCanvas;

        /// <summary>仅在未提供场景引用时生成演示层级；全部节点在绑定完成后统一显示。</summary>
        private void BuildExample()
        {
            generatedCanvas = new GameObject("MUI Rewards Canvas", typeof(RectTransform));
            generatedCanvas.SetActive(false);
            var ownedCanvas = generatedCanvas;
            lifetime.OnDispose(() =>
            {
                if (ownedCanvas != null)
                {
                    Destroy(ownedCanvas);
                }
            });
            generatedCanvas.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = generatedCanvas.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            // 固定尺寸的演示面板需完整进入视口，兼容窄窗口和超宽窗口。
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            generatedCanvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            if (EventSystem.current == null)
            {
                var input = new GameObject("Rewards EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                lifetime.OnDispose(() =>
                {
                    if (input != null)
                    {
                        Destroy(input);
                    }
                });
            }

            var page = CreateNode("RewardsView", generatedCanvas.transform, new Vector2(820, 490));
            page.gameObject.AddComponent<CanvasGroup>();
            var background = page.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color(0.09f, 0.12f, 0.18f);
            background.raycastTarget = false;
            var title = CreateText("Title", page, useFixedSlots ? "Rewards - five fixed slots" : "Rewards - recycling", new Vector2(730, 44));
            title.rectTransform.anchoredPosition = new Vector2(0, 208);
            title.fontSize = 26;

            resourceStatus = CreateText("ResourceStatus", page, "", new Vector2(730, 24));
            resourceStatus.fontSize = 16;
            resourceStatus.rectTransform.anchoredPosition = new Vector2(0, 174);

            var listRoot = CreateNode("Lst_Rewards", page, new Vector2(740, 310));
            list = useFixedSlots ? listRoot.gameObject.AddComponent<SlotListElement>() :
                listRoot.gameObject.AddComponent<RecyclingListElement>();
            var viewport = CreateNode("Viewport", listRoot, new Vector2(740, 310));
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var viewportGraphic = viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
            viewportGraphic.color = new Color(0.12f, 0.16f, 0.23f);
            var content = CreateNode("Content", viewport, Vector2.zero);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 10;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit =
                UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var scroll = listRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 24;

            // 包装节点参与父级布局；内部 View 拉伸填满，仍使用普通子视图绑定。
            var template = CreateNode("ItemTemplate", listRoot, new Vector2(700, 60));
            template.gameObject.SetActive(false);
            var itemSize = template.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            itemSize.minHeight = 60;
            itemSize.preferredHeight = 60;
            var item = CreateNode("RewardItem", template, Vector2.zero);
            item.anchorMin = Vector2.zero;
            item.anchorMax = Vector2.one;
            item.offsetMin = item.offsetMax = Vector2.zero;
            item.gameObject.AddComponent<CanvasGroup>();
            var itemGraphic = item.gameObject.AddComponent<UnityEngine.UI.Image>();
            itemGraphic.color = new Color(0.19f, 0.26f, 0.36f);
            itemGraphic.raycastTarget = false;
            var icon = CreateNode("Icon", item, new Vector2(40, 40));
            icon.anchorMin = icon.anchorMax = new Vector2(0, 0.5f);
            icon.pivot = new Vector2(0, 0.5f);
            icon.anchoredPosition = new Vector2(16, 0);
            var image = icon.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            icon.gameObject.AddComponent<ImageElement>();
            var label = CreateText("ItemLabel", item, "Reward", new Vector2(430, 48));
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0, 0.5f);
            label.rectTransform.pivot = new Vector2(0, 0.5f);
            label.rectTransform.anchoredPosition = new Vector2(72, 0);
            label.gameObject.AddComponent<TextElement>();
            var remove = CreateButton("Remove", item, "Remove", new Vector2(130, 42));
            var removeRect = (RectTransform)remove.transform;
            removeRect.anchorMin = removeRect.anchorMax = new Vector2(1, 0.5f);
            removeRect.pivot = new Vector2(1, 0.5f);
            removeRect.anchoredPosition = new Vector2(-12, 0);
            remove.gameObject.AddComponent<ButtonElement>();
            item.gameObject.AddComponent<View>();
            var nested = template.gameObject.AddComponent<NestedViewElement>();
            if (list is SlotListElement fixedList)
            {
                var mounts = new Transform[5];
                for (var index = 0; index < mounts.Length; ++index)
                {
                    var mount = CreateNode("Slot " + (index + 1), content, new Vector2(700, 60));
                    var size = mount.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                    size.minHeight = size.preferredHeight = 60;
                    mounts[index] = mount;
                }

                fixedList.ConfigureMounts(mounts, nested);
            }
            else
            {
                list.Configure(content, nested);
            }
            parentView = page.gameObject.AddComponent<View>();

            CreateActionButton("AddReward", "Add reward", -276, AddReward);
            CreateActionButton("ClearFont", "Clear font", -92, ClearFirstFont);
            CreateActionButton("RetryFont", "Retry font", 92, RetryFirstFont);
            CreateActionButton("FailFont", "Fail font", 276, FailFirstFont);
        }

        /// <summary>演示工具按钮不属于页面绑定；监听由同一激活作用域明确解除。</summary>
        private void CreateActionButton(string name, string title, float x, UnityEngine.Events.UnityAction action)
        {
            var button = CreateButton(name, generatedCanvas.transform, title, new Vector2(170, 42));
            ((RectTransform)button.transform).anchoredPosition = new Vector2(x, -204);
            button.onClick.AddListener(action);
            lifetime.OnDispose(() =>
            {
                if (button != null)
                {
                    button.onClick.RemoveListener(action);
                }
            });
        }

        private static RectTransform CreateNode(string name, Transform parent, Vector2 size)
        {
            var node = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            node.SetParent(parent, false);
            node.anchorMin = node.anchorMax = node.pivot = new Vector2(0.5f, 0.5f);
            node.sizeDelta = size;
            return node;
        }

        private static UnityEngine.UI.Text CreateText(string name, Transform parent, string value, Vector2 size)
        {
            var label = CreateNode(name, parent, size).gameObject.AddComponent<UnityEngine.UI.Text>();
            // 复用现有示例的内置字体，不引入 TMP 包或外部字体资源；演示文字使用其支持的拉丁字符。
            label.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 22;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = Color.white;
            label.text = value;
            label.raycastTarget = false;
            return label;
        }

        private static UnityEngine.UI.Button CreateButton(string name, Transform parent, string title, Vector2 size)
        {
            var node = CreateNode(name, parent, size);
            var graphic = node.gameObject.AddComponent<UnityEngine.UI.Image>();
            graphic.color = new Color(0.18f, 0.42f, 0.66f);
            var button = node.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = graphic;
            var text = CreateText("Caption", node, title, size);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }
    }
}
