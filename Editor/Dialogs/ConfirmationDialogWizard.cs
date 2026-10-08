using System;
using System.IO;
using MUI.UGUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MUI.Editor
{
    internal static class ConfirmationDialogWizard
    {
        [MenuItem("Tools/MUI/Create Confirmation Dialog Prefab")]
        private static void Create() => Create(false);

        [MenuItem("Tools/MUI/Create Alert Dialog Prefab")]
        private static void CreateAlert() => Create(true);

        // 两种标准对话框共用正文滚动区与保存流程，按钮契约分别校验。
        private static void Create(bool alert)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return;
            }

            var viewName = alert ? "AlertDialogView" : "ConfirmationDialogView";
            var path = EditorUtility.SaveFilePanelInProject("创建 MUI 对话框", viewName, "prefab", "请选择新的 Prefab 路径。");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (File.Exists(path) || File.Exists(path + ".meta"))
            {
                Debug.LogError("MUI will not overwrite an existing Prefab. Choose a new path.");
                return;
            }

            var scene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject(viewName, typeof(RectTransform), typeof(CanvasGroup), typeof(View), typeof(Image));
                root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, scene);
                var rect = (RectTransform)root.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(640, 420);
                root.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 1);
                var title = PrefabAuthoring.CreateText("Title", rect, alert ? "提示" : "Confirmation");
                title.gameObject.AddComponent<TextElement>();
                title.rectTransform.anchorMin = new Vector2(0, 1);
                title.rectTransform.anchorMax = Vector2.one;
                title.rectTransform.pivot = new Vector2(0.5f, 1);
                title.rectTransform.sizeDelta = new Vector2(-48, 48);
                title.rectTransform.anchoredPosition = new Vector2(0, -16);
                var viewport = new GameObject("MessageViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
                viewport.transform.SetParent(rect, false);
                var viewportRect = (RectTransform)viewport.transform;
                viewportRect.anchorMin = Vector2.zero;
                viewportRect.anchorMax = Vector2.one;
                viewportRect.offsetMin = new Vector2(24, 96);
                viewportRect.offsetMax = new Vector2(-24, -80);
                viewport.GetComponent<Image>().color = Color.clear;
                var message = PrefabAuthoring.CreateText("Message", viewportRect, alert ? "提示正文" : "Confirmation message");
                message.gameObject.AddComponent<TextElement>();
                message.alignment = TextAnchor.UpperLeft;
                message.horizontalOverflow = HorizontalWrapMode.Wrap;
                message.verticalOverflow = VerticalWrapMode.Overflow;
                message.rectTransform.anchorMin = new Vector2(0, 1);
                message.rectTransform.anchorMax = Vector2.one;
                message.rectTransform.pivot = new Vector2(0.5f, 1);
                message.rectTransform.sizeDelta = Vector2.zero;
                var fitter = message.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var scroll = viewport.GetComponent<ScrollRect>();
                scroll.viewport = viewportRect;
                scroll.content = message.rectTransform;
                scroll.horizontal = false;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                Selectable defaultSelection;
                if (alert)
                {
                    var acknowledge = AddButton(rect, "Acknowledge", "AcknowledgeLabel", 0, "确定");
                    var navigation = acknowledge.navigation;
                    navigation.mode = UnityEngine.UI.Navigation.Mode.None;
                    acknowledge.navigation = navigation;
                    defaultSelection = acknowledge;
                    // 返回表示关闭，不应转成已阅读；项目返回输入仍接到 UIHost.RequestBack。
                }
                else
                {
                    var cancel = AddButton(rect, "Cancel", "CancelLabel", -112, "Cancel");
                    var confirm = AddButton(rect, "Confirm", "ConfirmLabel", 112, "Confirm");
                    cancel.gameObject.AddComponent<DialogCancelInput>().CancelButton = cancel;
                    confirm.gameObject.AddComponent<DialogCancelInput>().CancelButton = cancel;
                    var navigation = cancel.navigation;
                    navigation.mode = UnityEngine.UI.Navigation.Mode.Explicit;
                    navigation.selectOnRight = confirm;
                    cancel.navigation = navigation;
                    navigation = confirm.navigation;
                    navigation.mode = UnityEngine.UI.Navigation.Mode.Explicit;
                    navigation.selectOnLeft = cancel;
                    confirm.navigation = navigation;
                    // 默认选中取消，避免破坏性操作被一次提交键意外接受。
                    defaultSelection = cancel;
                }
                var serializedView = new SerializedObject(root.GetComponent<View>());
                serializedView.FindProperty("defaultSelection").objectReferenceValue = defaultSelection;
                serializedView.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(true);
                var issues = ViewContractValidator.Validate(root.GetComponent<View>(),
                    alert ? AlertDialog.Manifest : ConfirmationDialog.Manifest);
                if (issues.Count > 0)
                {
                    throw new InvalidOperationException(string.Join("\n", issues));
                }

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (prefab == null)
                {
                    throw new IOException("无法保存对话框 Prefab。");
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

        private static Button AddButton(RectTransform parent, string name, string labelName, float x, string content)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(ButtonElement));
            node.transform.SetParent(parent, false);
            var rect = (RectTransform)node.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0);
            rect.anchoredPosition = new Vector2(x, 48);
            rect.sizeDelta = new Vector2(192, 48);
            var button = node.GetComponent<Button>();
            button.targetGraphic = node.GetComponent<Image>();
            node.GetComponent<ButtonElement>().AccessibilityLabel = content;
            var label = PrefabAuthoring.CreateText(labelName, rect, content);
            label.gameObject.AddComponent<TextElement>();
            label.color = Color.black;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            return button;
        }
    }
}
