using System;
using System.IO;
using System.Text;
using MUI.UGUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using L = MUI.Editor.Localization.MUIEditorLocalization;

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
        [SerializeField] private MUISettings settings = null;
        [SerializeField] private string textBackendId = "ugui";
        private string message;
        private string displayedLanguage;
        private Vector2 scroll;

        [MenuItem("Tools/MUI/页面向导 (Page Wizard)")]
        private static void Open()
        {
            var window = GetWindow<PageWizard>(L.Get("editor.PageWizard.5a814a8beb"));
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
            if (displayedLanguage != L.LanguageId)
            {
                displayedLanguage = L.LanguageId;
                InvalidateAssemblyInspection();
            }
            titleContent.text = L.Get("editor.PageWizard.5a814a8beb");
            scroll = EditorGUILayout.BeginScrollView(scroll);
            pageName = EditorGUILayout.TextField(L.Get("editor.PageWizard.dd8fd8a18e"), pageName);
            targetNamespace = EditorGUILayout.TextField(L.Get("editor.PageWizard.db772f3ff7"), targetNamespace);
            destination = EditorGUILayout.TextField(L.Get("editor.PageWizard.4d698da8c0"), destination);
            preset = L.EnumPopup(L.Get("editor.PageWizard.0706f9cb0a"), preset);
            settings = (MUISettings)EditorGUILayout.ObjectField(L.Get("editor.PageWizard.c37f7fe785"), settings, typeof(MUISettings), false);
            var backends = PageTextBackend.Capture();
            var selected = Array.FindIndex(backends, item => item.Id == textBackendId);
            var next = EditorGUILayout.Popup(L.Get("editor.PageWizard.534ad4e758"), selected, Array.ConvertAll(backends, item => L.Diagnostic(item.Label)));
            if (next >= 0 && next < backends.Length)
            {
                textBackendId = backends[next].Id;
            }
            DrawExistingView();
            typedContracts = EditorGUILayout.Toggle(L.Get("editor.PageWizard.19288123a4"), typedContracts);
            if (typedContracts)
            {
                createPresenter = true;
            }

            using (new EditorGUI.DisabledScope(typedContracts))
            {
                createPresenter = EditorGUILayout.Toggle(L.Get("editor.PageWizard.cf209c44b1"), createPresenter);
            }

            EditorGUILayout.HelpBox(L.Get("editor.PageWizard.593bb3ad77"), MessageType.Info);
            var error = ValidateDestination();
            if (error == null)
            {
                EditorGUILayout.LabelField(L.Get("editor.PageWizard.8a83cce53b"), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(existingPrefab == null
                    ? destination + "/" + pageName + "/Prefabs/" + pageName + "View.prefab"
                    : L.Get("editor.PageWizard.38e702fab3") + AssetDatabase.GetAssetPath(existingPrefab));
                foreach (var file in PageSourceTemplate.Create(pageName, targetNamespace, preset, createPresenter, typedContracts, PageTextBackend.Find(textBackendId).ElementType,
                    existingPrefab == null ? "Title" : titleElementName,
                    existingPrefab == null ? "Close" : closeElementName, ExistingPrefabGuid))
                {
                    EditorGUILayout.LabelField(destination + "/" + pageName + "/Scripts/" + file.Key);
                }

                EditorGUILayout.LabelField(L.Get("editor.PageWizard.3715f68b64"), EditorStyles.boldLabel);
                var policy = settings == null ? MUISettings.ResolveBuiltInPolicy(preset.ToString()) : settings.ResolvePolicy(preset.ToString());
                EditorGUILayout.LabelField(L.Get("editor.PageWizard.1fa1a10b63"), settings == null ? L.Get("editor.PageWizard.03c3f68152") : settings.name);
                EditorGUILayout.LabelField(L.Get("editor.PageWizard.4d0f13b4e9"), L.Format("editor.PageWizard.89b4e4e0de", policy.LayerName, policy.Layer, policy.Coverage, policy.Modal));
                EditorGUILayout.HelpBox(L.Get("editor.PageWizard.7fe8b9bce7"), MessageType.Info);
                error = DrawAssemblyInspection();
            }
            else
            {
                EditorGUILayout.HelpBox(L.Diagnostic(error), MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(error != null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
            {
                if (GUILayout.Button(L.Get("editor.PageWizard.f6f87e2c80")))
                {
                    Create();
                }
            }

            if (!string.IsNullOrEmpty(message))
            {
                EditorGUILayout.HelpBox(L.Diagnostic(message), MessageType.Info);
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
            if (settings != null)
            {
                try
                {
                    settings.ResolvePolicy(preset.ToString());
                }
                catch (Exception error)
                {
                    return error.Message;
                }
            }
            if (!PageSourceTemplate.IsIdentifier(pageName))
            {
                return L.Get("editor.PageWizard.245afa969c");
            }

            if (!PageSourceTemplate.IsNamespace(targetNamespace))
            {
                return L.Get("editor.PageWizard.c8e5200b74");
            }

            if (!IsAssetsPath(destination) || !AssetDatabase.IsValidFolder(destination))
            {
                return L.Get("editor.PageWizard.2ed479b29e");
            }

            var backend = PageTextBackend.Find(textBackendId);
            if (backend == null)
            {
                return L.Get("editor.PageWizard.e3acd6e42a");
            }
            var backendError = existingPrefab == null ? backend.Validate() : ValidateExistingView(backend);
            if (backendError != null)
            {
                return backendError;
            }
            var folder = destination + "/" + pageName;
            if (Directory.Exists(folder) || File.Exists(folder) || File.Exists(folder + ".meta") || AssetDatabase.LoadMainAssetAtPath(folder) != null)
            {
                return L.Get("editor.PageWizard.b93a5d933f");
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
                    throw new IOException(L.Get("editor.PageWizard.b96a4455b0"));
                }

                created = true;
                // 并发创建后 AssetDatabase 可能调整文件夹名，仅持有其实际返回路径。
                folder = AssetDatabase.GUIDToAssetPath(guid);
                if ((existingPrefab == null && string.IsNullOrEmpty(AssetDatabase.CreateFolder(folder, "Prefabs"))) ||
                    string.IsNullOrEmpty(AssetDatabase.CreateFolder(folder, "Scripts")))
                {
                    throw new IOException(L.Get("editor.PageWizard.ca7dc34857"));
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

                message = L.Get("editor.PageWizard.576a4a3ca2") + folder + L.Get("editor.PageWizard.cb599b951d") + pageName + L.Get("editor.PageWizard.02149f5808") + pageName + L.Get("editor.PageWizard.275da80635");
                Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(prefabPath);
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
            catch (Exception failure)
            {
                Debug.LogException(failure);
                var removed = !created || AssetDatabase.DeleteAsset(folder);
                message = removed ? L.Get("editor.PageWizard.0156b77f5b") : L.Get("editor.PageWizard.4f3f610097") + folder;
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
                if (preset == PagePreset.Popup || preset == PagePreset.Notice)
                {
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = preset == PagePreset.Notice ? new Vector2(480, 80) : new Vector2(600, 400);
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
                if (preset == PagePreset.Notice)
                {
                    // 路由不抢焦点，原生图形也不拦截下层射线；提示由业务持有的句柄关闭。
                    var group = root.GetComponent<CanvasGroup>();
                    group.interactable = false;
                    group.blocksRaycasts = false;
                    background.raycastTarget = false;
                    title.raycastTarget = false;
                    Stretch(title.rectTransform);
                }
                else
                {
                    CreateCloseButton(rect, backend);
                }
                root.SetActive(true);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                {
                    throw new IOException(L.Get("editor.PageWizard.0680c075b1"));
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

        private static void CreateCloseButton(RectTransform rect, PageTextBackend backend)
        {
            var close = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button), typeof(ButtonElement));
            close.transform.SetParent(rect, false);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = Vector2.one;
            closeRect.pivot = Vector2.one;
            closeRect.anchoredPosition = new Vector2(-16, -16);
            closeRect.sizeDelta = new Vector2(80, 48);
            close.GetComponent<Button>().targetGraphic = close.GetComponent<Image>();
            close.GetComponent<ButtonElement>().AccessibilityLabel = L.Get("editor.PageWizard.3fd47edce4");
            // 默认模板使用基础字体包含的关闭符号；语义名称独立保留，字体和语言由项目配置。
            var label = backend.CreateText("Label", closeRect, "×");
            label.color = Color.black;
            Stretch(label.rectTransform);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
