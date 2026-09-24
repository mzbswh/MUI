using System;
using MUI.UGUI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MUI.Samples.Navigation
{
    public sealed partial class ResourceImageDemo
    {
        private void CreateLayout()
        {
            canvasObject = new GameObject("MUI Resource Image Demo", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            if (EventSystem.current == null)
            {
                eventSystemObject = new GameObject("MUI Resource Image Input", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            var panel = CreateNode("ResourceImage", canvasObject.transform, new Vector2(620, 460), Vector2.zero);
            panel.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0.08f, 0.1f, 0.14f);
            var fontText = CreateText("FontPreview", panel, new Vector2(590, 36), new Vector2(0, 190), "Font resource preview");
            fontPreview = fontText.gameObject.AddComponent<TextElement>();
            var iconNode = CreateNode("Icon", panel, new Vector2(130, 130), new Vector2(-130, 90));
            iconNode.gameObject.AddComponent<UnityEngine.UI.Image>().raycastTarget = false;
            icon = iconNode.gameObject.AddComponent<ImageElement>();
            var previewNode = CreateNode("Preview", panel, new Vector2(180, 130), new Vector2(120, 90));
            previewNode.gameObject.AddComponent<UnityEngine.UI.RawImage>().raycastTarget = false;
            preview = previewNode.gameObject.AddComponent<RawImageElement>();
            status = CreateText("Status", panel, new Vector2(590, 130), new Vector2(0, -55), string.Empty);
            CreateButton("Next", panel, -240, NextImage);
            CreateButton("Fail", panel, -120, FailNextImages);
            CreateButton("Retry", panel, 0, RetryImages);
            CreateButton("Clear", panel, 120, ClearImages);
            CreateButton("Close", panel, 240, Close);
            // 最后挂 View，让 Awake 一次性收集已完成的控件层级。
            view = panel.gameObject.AddComponent<View>();
        }

        private static RectTransform CreateNode(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var node = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            node.SetParent(parent, false);
            node.anchorMin = node.anchorMax = new Vector2(0.5f, 0.5f);
            node.sizeDelta = size;
            node.anchoredPosition = position;
            return node;
        }

        private static UnityEngine.UI.Text CreateText(string name, Transform parent, Vector2 size, Vector2 position, string value)
        {
            var text = CreateNode(name, parent, size, position).gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private static void CreateButton(string caption, Transform parent, float x, Action action)
        {
            var node = CreateNode(caption, parent, new Vector2(100, 48), new Vector2(x, -170));
            var background = node.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color(0.2f, 0.3f, 0.42f);
            var button = node.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() =>
            {
                try
                {
                    action();
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                }
            });
            CreateText("Label", node, new Vector2(90, 42), Vector2.zero, caption);
        }
    }
}
