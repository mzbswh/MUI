using System;
using System.IO;
using System.Text;
using MUI.UGUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MUI.Editor
{
    public sealed partial class PageWizard : EditorWindow
    {
        private string pageName = "Inventory";
        private string targetNamespace = "Game.UI";
        private string destination = "Assets";
        private PagePreset preset;
        private bool createPresenter;
        private bool typedContracts;
        [SerializeField] private string textBackendId = "ugui";
        private string message;
        private Vector2 scroll;

        [MenuItem("Tools/MUI/Page Wizard")]
        private static void Open()
        {
            var window = GetWindow<PageWizard>("MUI Page Wizard");
            window.minSize = new Vector2(600, 480);
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!AssetDatabase.IsValidFolder(path))
            {
                path = Path.GetDirectoryName(path)?.Replace('\\', '/');
            }

            if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path) && IsAssetsPath(path))
            {
                window.destination = path;
            }
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            pageName = EditorGUILayout.TextField("页面名称", pageName);
            targetNamespace = EditorGUILayout.TextField("命名空间", targetNamespace);
            destination = EditorGUILayout.TextField("已有父目录", destination);
            preset = (PagePreset)EditorGUILayout.EnumPopup("页面策略", preset);
            var backends = PageTextBackend.Capture();
            var selected = Array.FindIndex(backends, item => item.Id == textBackendId);
            var next = EditorGUILayout.Popup("文本组件", selected, Array.ConvertAll(backends, item => item.Label));
            if (next >= 0 && next < backends.Length)
            {
                textBackendId = backends[next].Id;
            }
            DrawExistingView();
            typedContracts = EditorGUILayout.Toggle("类型化参数与结果", typedContracts);
            if (typedContracts)
            {
                createPresenter = true;
            }

            using (new EditorGUI.DisabledScope(typedContracts))
            {
                createPresenter = EditorGUILayout.Toggle("创建 Presenter", createPresenter);
            }

            EditorGUILayout.HelpBox("创建新页面目录、Prefab 和可编辑源码。目标程序集需要 MUI.Core、MUI.Resources、MUI.Navigation、MUI.UGUI 和绑定生成器；选择 TMP 时还需引用 MUI.TMP。Prefab 应放在 UIHost Canvas 下，创建后在资源提供方或 UIHost 目录登记资源键。页面生命周期使用统一异步入口，本地准备可立即完成。", MessageType.Info);
            var error = ValidateDestination();
            if (error == null)
            {
                EditorGUILayout.LabelField("即将创建的文件", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(existingPrefab == null
                    ? destination + "/" + pageName + "/Prefabs/" + pageName + "View.prefab"
                    : "关联已有资产：" + AssetDatabase.GetAssetPath(existingPrefab));
                foreach (var file in PageSourceTemplate.Create(pageName, targetNamespace, preset, createPresenter, typedContracts, PageTextBackend.Find(textBackendId).ElementType,
                    existingPrefab == null ? "Title" : titleElementName,
                    existingPrefab == null ? "Close" : closeElementName, ExistingPrefabGuid))
                {
                    EditorGUILayout.LabelField(destination + "/" + pageName + "/Scripts/" + file.Key);
                }

                EditorGUILayout.LabelField("路由策略", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(PageSourceTemplate.Policy(preset), EditorStyles.textArea, GUILayout.Height(60));
                error = DrawAssemblyInspection();
            }
            else
            {
                EditorGUILayout.HelpBox(error, MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(error != null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
            {
                if (GUILayout.Button("创建页面"))
                {
                    Create();
                }
            }

            if (!string.IsNullOrEmpty(message))
            {
                EditorGUILayout.HelpBox(message, MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private static bool IsAssetsPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.IndexOf('\\') >= 0)
            {
                return false;
            }

            if (path != "Assets" && !path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return false;
            }

            foreach (var part in path.Split('/'))
            {
                if (part == ".." || part == "." || part.Length == 0)
                {
                    return false;
                }
            }

            return true;
        }

        private string ValidateDestination()
        {
            if (!PageSourceTemplate.IsIdentifier(pageName))
            {
                return "页面名只能包含英文字母、数字和下划线，且不能以数字开头。";
            }

            if (!PageSourceTemplate.IsNamespace(targetNamespace))
            {
                return "命名空间应由合法标识符组成，以点分隔。";
            }

            if (!IsAssetsPath(destination) || !AssetDatabase.IsValidFolder(destination))
            {
                return "请选择 Assets 下已存在的目录。";
            }

            var backend = PageTextBackend.Find(textBackendId);
            if (backend == null)
            {
                return "所选文本模板不可用，请启用对应模块或重新选择。";
            }
            var backendError = existingPrefab == null ? backend.Validate() : ValidateExistingView(backend);
            if (backendError != null)
            {
                return backendError;
            }
            var folder = destination + "/" + pageName;
            if (Directory.Exists(folder) || File.Exists(folder) || File.Exists(folder + ".meta") || AssetDatabase.LoadMainAssetAtPath(folder) != null)
            {
                return "页面目录已存在，请修改名称或父目录；不会覆盖已有内容。";
            }

            return null;
        }

        private void Create()
        {
            InvalidateExistingValidation();
            var error = ValidateDestination();
            if (error != null)
            {
                message = error;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return;
            }

            // 创建前重新读取编译配置，避免沿用窗口预览期间失效的结果。
            var inspection = InspectAssembly(refresh: true);
            if (inspection.Error != null)
            {
                message = inspection.Error;
                return;
            }

            var sources = PageSourceTemplate.Create(pageName, targetNamespace, preset, createPresenter, typedContracts, PageTextBackend.Find(textBackendId).ElementType,
                    existingPrefab == null ? "Title" : titleElementName,
                    existingPrefab == null ? "Close" : closeElementName, ExistingPrefabGuid);
            var folder = destination + "/" + pageName;
            var created = false;
            AssetDatabase.DisallowAutoRefresh();
            try
            {
                var guid = AssetDatabase.CreateFolder(destination, pageName);
                if (string.IsNullOrEmpty(guid))
                {
                    throw new IOException("无法创建页面目录。");
                }

                created = true;
                // 并发创建后 AssetDatabase 可能调整文件夹名，仅持有其实际返回路径。
                folder = AssetDatabase.GUIDToAssetPath(guid);
                if ((existingPrefab == null && string.IsNullOrEmpty(AssetDatabase.CreateFolder(folder, "Prefabs"))) ||
                    string.IsNullOrEmpty(AssetDatabase.CreateFolder(folder, "Scripts")))
                {
                    throw new IOException("无法创建页面子目录。");
                }

                var prefabPath = existingPrefab == null ? folder + "/Prefabs/" + pageName + "View.prefab"
                    : AssetDatabase.GetAssetPath(existingPrefab);
                if (existingPrefab == null)
                {
                    SavePrefab(prefabPath);
                }
                foreach (var file in sources)
                {
                    using (var stream = new FileStream(folder + "/Scripts/" + file.Key, FileMode.CreateNew, FileAccess.Write))
                    {
                        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                        {
                            writer.Write(file.Value);
                        }
                    }
                }

                message = "已创建 " + folder + "。请登记资源键 '" + pageName + "View'，并使用 " + pageName + "Page.CreateRoute()。类型化结果需由项目补充完成命令。";
                Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(prefabPath);
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
            catch (Exception failure)
            {
                Debug.LogException(failure);
                var removed = !created || AssetDatabase.DeleteAsset(folder);
                message = removed ? "创建失败，已移除本批新目录，请查看控制台。" : "创建和清理均失败，请检查新目录：" + folder;
            }
            finally
            {
                AssetDatabase.AllowAutoRefresh();
                AssetDatabase.Refresh();
            }
        }

        private void SavePrefab(string path)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject(pageName + "View", typeof(RectTransform), typeof(CanvasGroup), typeof(View));
                root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, scene);
                var rect = (RectTransform)root.transform;
                if (preset == PagePreset.Popup)
                {
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(600, 400);
                }
                else
                {
                    Stretch(rect);
                }

                var background = root.AddComponent<Image>();
                background.color = new Color(0.12f, 0.14f, 0.18f, 1);
                var backend = PageTextBackend.Find(textBackendId);
                var title = backend.CreateText("Title", rect, pageName);
                title.gameObject.AddComponent(backend.ElementType);
                title.rectTransform.anchorMin = new Vector2(0, 1);
                title.rectTransform.anchorMax = Vector2.one;
                title.rectTransform.pivot = new Vector2(0.5f, 1);
                title.rectTransform.anchoredPosition = new Vector2(-48, -16);
                title.rectTransform.sizeDelta = new Vector2(-128, 48);
                var close = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button), typeof(ButtonElement));
                close.transform.SetParent(rect, false);
                var closeRect = (RectTransform)close.transform;
                closeRect.anchorMin = closeRect.anchorMax = Vector2.one;
                closeRect.pivot = Vector2.one;
                closeRect.anchoredPosition = new Vector2(-16, -16);
                closeRect.sizeDelta = new Vector2(80, 48);
                close.GetComponent<Button>().targetGraphic = close.GetComponent<Image>();
                close.GetComponent<ButtonElement>().AccessibilityLabel = "关闭";
                // 默认模板使用基础字体包含的关闭符号；语义名称独立保留，字体和语言由项目配置。
                var label = backend.CreateText("Label", closeRect, "×");
                label.color = Color.black;
                Stretch(label.rectTransform);
                root.SetActive(true);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                {
                    throw new IOException("保存 View Prefab 失败。");
                }
            }
            finally
            {
                if (root != null)
                {
                    DestroyImmediate(root);
                }

                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
