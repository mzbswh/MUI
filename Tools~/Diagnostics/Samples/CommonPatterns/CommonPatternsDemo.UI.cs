using UnityEngine;
using UnityEngine.EventSystems;

namespace MUI.Samples.CommonPatterns
{
    public sealed partial class CommonPatternsDemo
    {
        private void BuildUI()
        {
            canvasObject = new GameObject("Common Patterns Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 600);
            scaler.matchWidthOrHeight = 0.5f;

            if (EventSystem.current == null)
            {
                eventSystemObject = new GameObject("Common Patterns EventSystem", typeof(EventSystem),
                    typeof(StandaloneInputModule));
            }

            var background = CreateNode("Background", canvasObject.transform);
            Stretch(background);
            backgroundGraphic = background.gameObject.AddComponent<UnityEngine.UI.Image>();
            backgroundGraphic.raycastTarget = false;

            panel = CreateNode("Panel", background);
            panel.anchorMin = new Vector2(0.08f, 0.04f);
            panel.anchorMax = new Vector2(0.92f, 0.96f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            panelGraphic = panel.gameObject.AddComponent<UnityEngine.UI.Image>();
            panelGraphic.raycastTarget = false;
            var panelLayout = panel.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(24, 24, 24, 24);
            panelLayout.spacing = 12;
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            var header = CreateRow("Header", panel, 58);
            iconGraphic = CreateNode("ThemeIcon", header).gameObject.AddComponent<UnityEngine.UI.Image>();
            iconGraphic.raycastTarget = false;
            var iconLayout = iconGraphic.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            iconLayout.preferredWidth = 52;
            iconLayout.preferredHeight = 52;
            iconLayout.flexibleWidth = 0;
            titleText = CreateText("Title", header, 28, TextAnchor.MiddleLeft);
            titleText.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1;

            detailText = CreateText("Detail", panel, 20, TextAnchor.MiddleLeft);
            var detailLayout = detailText.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            detailLayout.preferredHeight = 82;
            detailLayout.flexibleWidth = 1;

            var languageRow = CreateRow("Preferences", panel, 58);
            localeButton = CreateButton("LocaleButton", languageRow, out localeButtonText);
            themeButton = CreateButton("ThemeButton", languageRow, out themeButtonText);

            var actionsRow = CreateRow("Actions", panel, 58);
            toastButton = CreateButton("ToastButton", actionsRow, out toastButtonText);
            workButton = CreateButton("WorkButton", actionsRow, out workButtonText);

            inputText = CreateText("InputState", panel, 18, TextAnchor.MiddleLeft);
            inputText.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 46;

            banners = CreateNode("Feedback", panel);
            var bannerLayout = banners.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            bannerLayout.spacing = 10;
            bannerLayout.childControlWidth = true;
            bannerLayout.childControlHeight = true;
            bannerLayout.childForceExpandWidth = true;
            bannerLayout.childForceExpandHeight = false;
            banners.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 0;

            toastPanel = CreateNode("Toast", banners);
            toastGraphic = toastPanel.gameObject.AddComponent<UnityEngine.UI.Image>();
            toastGraphic.raycastTarget = false;
            toastPanel.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 54;
            toastText = CreateText("Message", toastPanel, 20, TextAnchor.MiddleLeft);
            Stretch(toastText.rectTransform, 14, 12);

            loadingPanel = CreateNode("Loading", banners);
            loadingGraphic = loadingPanel.gameObject.AddComponent<UnityEngine.UI.Image>();
            loadingGraphic.raycastTarget = false;
            loadingPanel.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 58;
            var loadingRow = loadingPanel.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            loadingRow.padding = new RectOffset(14, 14, 8, 8);
            loadingRow.spacing = 10;
            loadingRow.childControlWidth = true;
            loadingRow.childControlHeight = true;
            loadingRow.childForceExpandWidth = false;
            loadingRow.childForceExpandHeight = true;
            loadingText = CreateText("Message", loadingPanel, 20, TextAnchor.MiddleLeft);
            loadingText.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1;
            progressText = CreateText("Progress", loadingPanel, 20, TextAnchor.MiddleRight);
            var progressLayout = progressText.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            progressLayout.preferredWidth = 60;
            progressLayout.flexibleWidth = 0;

            toastPanel.gameObject.SetActive(false);
            loadingPanel.gameObject.SetActive(false);
            lightIcon = CreateIcon(false, out lightIconTexture);
            darkIcon = CreateIcon(true, out darkIconTexture);
        }

        private void UpdateBannerLayout()
        {
            if (banners == null || panel == null)
            {
                return;
            }

            var toastHeight = toastPanel != null && toastPanel.gameObject.activeSelf
                ? toastPanel.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight : 0;
            var loadingHeight = loadingPanel != null && loadingPanel.gameObject.activeSelf ? 58f : 0;
            banners.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight =
                toastHeight + loadingHeight + (toastHeight > 0 && loadingHeight > 0 ? 10 : 0);
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        }

        private UnityEngine.UI.Button CreateButton(string name, Transform parent, out UnityEngine.UI.Text label)
        {
            var node = CreateNode(name, parent);
            var image = node.gameObject.AddComponent<UnityEngine.UI.Image>();
            var button = node.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var layout = node.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.preferredHeight = 54;
            layout.flexibleWidth = 1;
            label = CreateText("Label", node, 20, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, 8, 2);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = 20;
            buttonImages.Add(image);
            buttonTexts.Add(label);
            return button;
        }

        private static RectTransform CreateRow(string name, Transform parent, float height)
        {
            var row = CreateNode(name, parent);
            row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
            var layout = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return row;
        }

        private static RectTransform CreateNode(string name, Transform parent)
        {
            var node = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            node.SetParent(parent, false);
            return node;
        }

        private static UnityEngine.UI.Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment)
        {
            var text = CreateNode(name, parent).gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform node, float horizontalInset = 0, float verticalInset = 0)
        {
            node.anchorMin = Vector2.zero;
            node.anchorMax = Vector2.one;
            node.offsetMin = new Vector2(horizontalInset, verticalInset);
            node.offsetMax = new Vector2(-horizontalInset, -verticalInset);
        }

        private static Sprite CreateIcon(bool dark, out Texture2D texture)
        {
            const int size = 32;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            var pixels = new Color[size * size];
            for (var y = 0; y < size; ++y)
            {
                for (var x = 0; x < size; ++x)
                {
                    var dx = x - 15.5f;
                    var dy = y - 15.5f;
                    var filled = dark
                        ? dx * dx + dy * dy < 132 && (dx - 5) * (dx - 5) + (dy + 5) * (dy + 5) > 120
                        : dx * dx + dy * dy < 72;
                    pixels[y * size + x] = filled ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
