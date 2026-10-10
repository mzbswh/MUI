using System;
using System.IO;
using MUI.UGUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    internal static class OverlayPrefabWizard
    {
        [MenuItem("Tools/MUI/创建上下文菜单 (Create Context Menu Prefab)")]
        private static void CreateContextMenu() => Create(true);

        [MenuItem("Tools/MUI/创建悬浮提示 (Create Tooltip Prefab)")]
        private static void CreateTooltip() => Create(false);

        private static void Create(bool menu)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return;
            }

            var name = menu ? "ContextMenuView" : "TooltipView";
            var path = EditorUtility.SaveFilePanelInProject(L.Get("editor.OverlayPrefabWizard.3d439e23e6") + name, name, "prefab", L.Get("editor.OverlayPrefabWizard.a587f03b9b"));
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (File.Exists(path) || File.Exists(path + ".meta"))
            {
                Debug.LogError(L.Get("editor.OverlayPrefabWizard.43e83309d7"));
                return;
            }

            var scene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(View));
                root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, scene);
                Stretch((RectTransform)root.transform);
                var bounds = Node(menu ? "Menu" : "Tooltip", root.transform);
                Stretch(bounds);
                var overlay = bounds.gameObject.AddComponent<AnchoredOverlayElement>();
                var content = Node("Content", bounds);
                content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
                content.sizeDelta = menu ? new Vector2(280, 120) : new Vector2(320, 96);
                var background = content.gameObject.AddComponent<Image>();
                background.color = new Color(0.12f, 0.14f, 0.18f, 1);
                var data = new SerializedObject(overlay);
                data.FindProperty("content").objectReferenceValue = content;
                data.FindProperty("dismissOutside").boolValue = menu;
                data.ApplyModifiedPropertiesWithoutUndo();
                if (menu)
                {
                    BuildMenu(bounds, content);
                }
                else
                {
                    BuildTooltip(content);
                }

                root.SetActive(true);
                var view = root.GetComponent<View>();
                var issues = ViewContractValidator.ValidateStructure(view);
                if (issues.Count > 0)
                {
                    throw new InvalidOperationException(string.Join("\n", issues));
                }

                var semantics = ViewContractValidator.ValidateAccessibility(view);
                if (semantics.Count > 0)
                {
                    throw new InvalidOperationException(string.Join("\n", semantics));
                }

                // 保存前再次检查，向导不会更新已有资源。
                if (File.Exists(path) || File.Exists(path + ".meta"))
                {
                    throw new IOException(L.Get("editor.OverlayPrefabWizard.28ea9043a7"));
                }

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (prefab == null)
                {
                    throw new IOException(L.Get("editor.OverlayPrefabWizard.28d14ed8ee"));
                }

                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
            finally
            {
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }

                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void BuildMenu(RectTransform bounds, RectTransform content)
        {
            var surface = bounds.gameObject.AddComponent<Image>();
            surface.color = Color.clear;
            surface.alphaHitTestMinimumThreshold = 0;
            // 资源中保持禁用，仅在菜单打开且可交互时由 OverlayDismissArea 启用。
            surface.enabled = false;
            surface.raycastTarget = false;
            bounds.gameObject.AddComponent<OverlayDismissArea>();
            bounds.gameObject.AddComponent<ContextMenuController>();
            AddButton(content, "Action1", L.Get("editor.OverlayPrefabWizard.da2eb576b5"), 26);
            AddButton(content, "Action2", L.Get("editor.OverlayPrefabWizard.dbdebc5676"), -26);
        }

        private static void AddButton(RectTransform parent, string name, string title, float y)
        {
            var rect = Node(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(256, 44);
            rect.anchoredPosition = new Vector2(0, y);
            var graphic = rect.gameObject.AddComponent<Image>();
            graphic.color = new Color(0.25f, 0.28f, 0.34f, 1);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = graphic;
            rect.gameObject.AddComponent<ButtonElement>().AccessibilityLabel = title;
            rect.gameObject.AddComponent<ContextMenuItemInput>();
            var label = PrefabAuthoring.CreateText(name + "Label", rect, title);
            label.gameObject.AddComponent<TextElement>();
            Stretch(label.rectTransform);
        }

        private static void BuildTooltip(RectTransform content)
        {
            var group = content.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            content.GetComponent<Image>().raycastTarget = false;
            var label = PrefabAuthoring.CreateText("Message", content, L.Get("editor.OverlayPrefabWizard.613a9a6df1"));
            label.gameObject.AddComponent<TextElement>();
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(16, 12);
            label.rectTransform.offsetMax = new Vector2(-16, -12);
        }

        private static RectTransform Node(string name, Transform parent)
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.transform.SetParent(parent, false);
            return (RectTransform)node.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
