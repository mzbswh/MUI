using MUI.Unity;
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using MUI.UGUI;
using MUI.Pages;
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
        [SerializeField] private string pageName = "Inventory";
        [SerializeField] private string targetNamespace = "Game.UI";
        [SerializeField] private string presetName;
        [SerializeField] private bool createPresenter;
        [SerializeField] private bool typedContracts;
        private MUISettings settings;
        private string settingsError;
        [SerializeField] private string textBackendId = "ugui";
        private string message;
        private string displayedLanguage;
        private Vector2 scroll;
        private bool creationQueued;
        [SerializeField] private int step;
        [SerializeField] private string createdScriptDirectory;
        [SerializeField] private string createdPrefabPath;
        private GUIStyle headingStyle;
        private GUIStyle cardStyle;
        private GUIStyle noteStyle;
        private GUIStyle policyValueStyle;
        private bool stylesUseDarkSkin;

        [MenuItem("Tools/MUI/页面向导", false, 1)]
        private static void Open()
        {
            var window = GetWindow<PageWizard>(L.Get("editor.PageWizard.5a814a8beb"));
            window.minSize = new Vector2(600, 480);
        }

        private static GUIContent Hint(string labelKey, string hintKey) =>
            new GUIContent(L.Get(labelKey), L.Get("wizard." + hintKey));

        private void EnsureStyles()
        {
            if (headingStyle != null && stylesUseDarkSkin == EditorGUIUtility.isProSkin)
                return;
            stylesUseDarkSkin = EditorGUIUtility.isProSkin;
            var textColor = stylesUseDarkSkin ? new Color(0.82f, 0.82f, 0.82f) : new Color(0.12f, 0.12f, 0.12f);
            headingStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, wordWrap = true };
            headingStyle.normal.textColor = textColor;
            cardStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(16, 16, 14, 14),
                margin = new RectOffset(0, 0, 8, 8)
            };
            noteStyle = new GUIStyle(EditorStyles.label) { fontSize = 12, wordWrap = true, stretchWidth = true };
            noteStyle.normal.textColor = textColor;
            policyValueStyle = new GUIStyle(noteStyle) { fontStyle = FontStyle.Bold };
        }

        private void OnGUI()
        {
            EnsureStyles();
            RuntimeSettingsAsset.TryGet(out settings, out settingsError);
            RefreshPolicy();
            if (displayedLanguage != L.LanguageId)
            {
                displayedLanguage = L.LanguageId;
                InvalidateAssemblyInspection();
            }
            titleContent.text = L.Get("editor.PageWizard.5a814a8beb");
            var oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * 0.35f, 100, 180);
            try
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(16);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(20);
                        using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Max(1, position.width - 40))))
                        {
                            GUILayout.Label(L.Get("wizard.title"), headingStyle);
                            GUILayout.Space(5);
                            GUILayout.Label(L.Get("wizard.subtitle"), noteStyle);
                            GUILayout.Space(16);
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                for (var i = 0; i < 3; i++)
                                {
                                    using (new EditorGUI.DisabledScope(step == 3 || i > step))
                                    {
                                        if (GUILayout.Toggle(step == i, new GUIContent((i + 1) + "  " + L.Get("wizard.step" + i), L.Get("wizard.step" + i + "Hint")), "Button", GUILayout.Height(32)) && i < step)
                                            ChangeStep(i);
                                    }
                                }
                            }
                        }
                        GUILayout.Space(20);
                    }
                    GUILayout.Space(10);
                    scroll.x = 0;
                    scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(20);
                        // 固定内容宽度，避免长路径和中文说明撑大滚动区域。
                        // 预留垂直滚动条空间，停靠到窄面板时也只需纵向滚动。
                        using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Max(1, position.width - 60))))
                        {
                            if (step == 0)
                                DrawIdentity();
                            else if (step == 1)
                                DrawBehavior();
                            else if (step == 2)
                                DrawReview();
                            else
                                DrawCompleted();
                        }
                        GUILayout.Space(20);
                    }
                    GUILayout.EndScrollView();
                    DrawFooter();
                }
            }
            finally { EditorGUIUtility.labelWidth = oldLabelWidth; }
        }

        private void ChangeStep(int next)
        {
            step = next;
            scroll = Vector2.zero;
            message = null;
            if (Event.current != null)
                GUI.FocusControl(null);
            Repaint();
        }

        private void DrawIdentity()
        {
            using (new EditorGUILayout.VerticalScope(cardStyle))
            {
                GUILayout.Label(L.Get("wizard.identity"), EditorStyles.boldLabel);
                GUILayout.Space(10);
                var widgetMode = EditorGUILayout.Popup("创建类型", createWidget ? 1 : 0, new[] { "Page 页面", "Widget 子视图" }) == 1;
                if (widgetMode != createWidget)
                {
                    createWidget = widgetMode;
                    InvalidateExistingValidation();
                }
                pageName = EditorGUILayout.TextField(Hint("editor.PageWizard.dd8fd8a18e", "nameHint"), pageName);
                targetNamespace = EditorGUILayout.TextField(Hint("editor.PageWizard.db772f3ff7", "namespaceHint"), targetNamespace);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField(new GUIContent(L.Get("property.pagePrefabDirectory"), L.Get("settings.pagePrefabDirectory.help")),
                        MUIEditorSettings.instance.PagePrefabDirectory);
                    EditorGUILayout.TextField(new GUIContent(L.Get("property.pageScriptDirectory"), L.Get("settings.pageScriptDirectory.help")),
                        MUIEditorSettings.instance.PageScriptDirectory);
                }
                if (GUILayout.Button(L.Get("wizard.editDirectories")))
                    RuntimeSettingsAsset.OpenOrCreate();
                GUILayout.Space(8);
                GUILayout.Label(L.Get("wizard.locationNote"), noteStyle);
            }
            using (new EditorGUILayout.VerticalScope(cardStyle))
            {
                GUILayout.Label(L.Get("wizard.view"), EditorStyles.boldLabel);
                GUILayout.Space(10);
                var backends = PageTextBackend.Capture();
                var selected = Array.FindIndex(backends, item => item.Id == textBackendId);
                var next = EditorGUILayout.Popup(Hint("editor.PageWizard.534ad4e758", "backendHint"), selected,
                    Array.ConvertAll(backends, item => new GUIContent(L.Diagnostic(item.Label))));
                if (next >= 0 && next < backends.Length)
                    textBackendId = backends[next].Id;
                DrawExistingView();
                if (existingPrefab == null)
                    GUILayout.Label(L.Get("wizard.newPrefabNote"), noteStyle);
            }
        }

        private void DrawBehavior()
        {
            if (createWidget)
            {
                DrawWidgetBehavior();
                return;
            }
            using (new EditorGUILayout.VerticalScope(cardStyle))
            {
                GUILayout.Label(L.Get("editor.PageWizard.0706f9cb0a"), EditorStyles.boldLabel);
                GUILayout.Space(8);
                DrawPolicyChoices();
                GUILayout.Space(8);
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField(Hint("globalSettings.label", "settingsHint"), settings, typeof(MUISettings), false);
                GUILayout.Label(L.Get("globalSettings.help"), noteStyle);
                if (settingsError == null && GUILayout.Button(L.Get(settings == null ? "globalSettings.create" : "globalSettings.locate")))
                {
                    RuntimeSettingsAsset.OpenOrCreate();
                    RuntimeSettingsAsset.TryGet(out settings, out settingsError);
                }
            }
            using (new EditorGUILayout.VerticalScope(cardStyle))
            {
                GUILayout.Label(L.Get("wizard.logic"), EditorStyles.boldLabel);
                GUILayout.Space(8);
                var mode = typedContracts ? 2 : createPresenter ? 1 : 0;
                var modes = new[]
                {
                    new GUIContent(L.Get("wizard.logicMode0"), L.Get("wizard.logicDescription0")),
                    new GUIContent(L.Get("wizard.logicMode1"), L.Get("wizard.logicDescription1")),
                    new GUIContent(L.Get("wizard.logicMode2"), L.Get("wizard.logicDescription2"))
                };
                mode = EditorGUILayout.Popup(new GUIContent(L.Get("wizard.logicQuestion"), L.Get("wizard.logicChoiceHint")), mode, modes);
                typedContracts = mode == 2;
                createPresenter = mode != 0;
                GUILayout.Space(10);
                GUILayout.Label(L.Get("wizard.logicDescription" + mode), noteStyle);
                GUILayout.Space(6);
                GUILayout.Label(L.Get("wizard.logicExample" + mode), noteStyle);
                GUILayout.Space(12);
                GUILayout.Label(L.Get("wizard.logicFiles"), EditorStyles.boldLabel);
                GUILayout.Label(pageName + "ViewModel.cs", noteStyle);
                if (createPresenter)
                    GUILayout.Label("+ " + pageName + "Presenter.cs", noteStyle);
                GUILayout.Space(8);
                GUILayout.Label(L.Get("wizard.logicScaffoldNote"), noteStyle);
            }
        }

        private void DrawReview()
        {
            var error = ValidateDestination();
            if (error != null)
                return;
            using (new EditorGUILayout.VerticalScope(cardStyle))
            {
                GUILayout.Label(L.Get("editor.PageWizard.8a83cce53b"), EditorStyles.boldLabel);
                GUILayout.Space(8);
                GUILayout.Label(existingPrefab == null
                    ? PrefabPath
                    : L.Get("editor.PageWizard.38e702fab3") + AssetDatabase.GetAssetPath(existingPrefab), noteStyle);
                foreach (var file in CreatePageSources())
                    GUILayout.Label(ScriptDirectory + "/" + file.Key, noteStyle);
                GUILayout.Space(8);
                GUILayout.Label(L.Format("wizard.resourceKey", PageResourceKey), noteStyle);
                if (!createWidget && !PrefabPath.Contains("/Resources/"))
                    EditorGUILayout.HelpBox(L.Get("wizard.customDirectoryLoader"), MessageType.Info);
            }
            if (!createWidget)
                using (new EditorGUILayout.VerticalScope(cardStyle))
                {
                    GUILayout.Label(L.Get("editor.PageWizard.3715f68b64"), EditorStyles.boldLabel);
                    var policy = selectedPolicy;
                    GUILayout.Label(L.Get("editor.PageWizard.1fa1a10b63") + ": " + (settings == null ? L.Get("editor.PageWizard.03c3f68152") : settings.name), noteStyle);
                    GUILayout.Label(L.Format("editor.PageWizard.89b4e4e0de", policy.LayerName, policy.Layer, policy.Coverage, policy.Modal), noteStyle);
                    GUILayout.Space(8);
                    GUILayout.Label(L.Get("editor.PageWizard.7fe8b9bce7"), noteStyle);
                }
            using (new EditorGUILayout.VerticalScope(cardStyle))
            {
                GUILayout.Label(L.Get("wizard.preflight"), EditorStyles.boldLabel);
                DrawAssemblyInspection();
            }
        }

        private void DrawCompleted()
        {
            using (new EditorGUILayout.VerticalScope(cardStyle))
            {
                GUILayout.Label(L.Get("wizard.success"), headingStyle);
                GUILayout.Space(10);
                GUILayout.Label(createdPrefabPath, noteStyle);
                GUILayout.Label(createdScriptDirectory, noteStyle);
                GUILayout.Space(12);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(Hint("wizard.openPrefab", "openPrefabHint"), GUILayout.Height(30)))
                        AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(createdPrefabPath));
                    if (GUILayout.Button(Hint("wizard.showScripts", "showScriptsHint"), GUILayout.Height(30)))
                    {
                        Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(createdScriptDirectory);
                        EditorGUIUtility.PingObject(Selection.activeObject);
                    }
                }
            }
            using (new EditorGUILayout.VerticalScope(cardStyle))
            {
                GUILayout.Label(L.Get("wizard.nextSteps"), EditorStyles.boldLabel);
                GUILayout.Space(8);
                GUILayout.Label(createWidget ? "将 Widget Prefab 拖入父 View 的层级，在父 View 点击生成绑定。通过 Presenter 的 VM 属性访问生成的子模型属性。" : L.Get("wizard.nextStepsBody"), noteStyle);
                if (!createWidget && !createdPrefabPath.Contains("/Resources/"))
                {
                    GUILayout.Space(8);
                    EditorGUILayout.HelpBox(L.Get("wizard.customDirectoryLoader"), MessageType.Info);
                }
            }
        }

        private void DrawFooter()
        {
            using (new EditorGUILayout.VerticalScope(cardStyle))
            {
                var busy = creationQueued || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling;
                var error = step == 3 ? null : ValidateDestination(includePolicy: step != 0);
                if (error == null && step == 2)
                    error = InspectAssembly().Error;
                if (!string.IsNullOrEmpty(message))
                    EditorGUILayout.HelpBox(L.Diagnostic(message), MessageType.Error);
                else if (error != null)
                    EditorGUILayout.HelpBox(L.Diagnostic(error), MessageType.Warning);
                else if (busy)
                    GUILayout.Label(L.Get("wizard.busy"), noteStyle);
                else
                    GUILayout.Label(L.Get(step == 3 ? "wizard.completedNote" : "wizard.footerNote"), EditorStyles.miniLabel);
                GUILayout.Space(8);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (step > 0 && step < 3 && GUILayout.Button(Hint("wizard.back", "backHint"), GUILayout.Width(100), GUILayout.Height(32)))
                        ChangeStep(step - 1);
                    GUILayout.FlexibleSpace();
                    if (step == 3)
                    {
                        if (GUILayout.Button(Hint("wizard.another", "anotherHint"), GUILayout.Width(160), GUILayout.Height(32)))
                            ChangeStep(0);
                    }
                    else
                    {
                        using (new EditorGUI.DisabledScope(busy || error != null))
                        {
                            if (GUILayout.Button(Hint(step == 2 ? "editor.PageWizard.f6f87e2c80" : "wizard.next", step == 2 ? "createHint" : "nextHint"), GUILayout.Width(160), GUILayout.Height(32)))
                            {
                                if (step == 2)
                                    QueueCreation();
                                else
                                    ChangeStep(step + 1);
                            }
                        }
                    }
                }
            }
        }

        private string ValidateDestination(bool includePolicy = true)
        {
            if (includePolicy && settingsError != null)
                return settingsError;
            if (!createWidget && includePolicy && policyError != null)
                return policyError;
            if (!PageSourceTemplate.IsIdentifier(pageName))
            {
                return L.Get("editor.PageWizard.245afa969c");
            }

            if (!PageSourceTemplate.IsNamespace(targetNamespace))
            {
                return L.Get("editor.PageWizard.c8e5200b74");
            }

            try
            {
                MUIEditorSettings.ResolvePageDirectory(MUIEditorSettings.instance.PageScriptDirectory, pageName);
                MUIEditorSettings.ResolvePageDirectory(MUIEditorSettings.instance.PagePrefabDirectory, pageName);
            }
            catch (ArgumentException error) { return error.Message; }

            var backend = PageTextBackend.Find(textBackendId);
            if (backend == null)
            {
                return L.Get("editor.PageWizard.e3acd6e42a");
            }
            var backendError = existingPrefab == null ? backend.Validate() : includePolicy ? ValidateExistingView(backend) : null;
            if (backendError != null)
            {
                return backendError;
            }
            var conflict = DirectoryConflict(ScriptDirectory) ?? (existingPrefab == null ? DirectoryConflict(PrefabDirectory) : null);
            if (conflict != null)
                return L.Format("wizard.pathOccupied", conflict);
            var names = new List<string> { pageName + "ViewModel.cs" };
            if (createPresenter)
                names.Add(pageName + "Presenter.cs");
            if (createWidget && createPresenter && typedContracts)
                names.Add(pageName + "Args.cs");
            foreach (var name in names)
                if (PathOccupied(ScriptDirectory + "/" + name))
                    return L.Format("wizard.pathOccupied", ScriptDirectory + "/" + name);
            if (existingPrefab == null && PathOccupied(PrefabPath))
                return L.Format("wizard.pathOccupied", PrefabPath);

            return null;
        }

        private void QueueCreation()
        {
            if (creationQueued)
                return;
            creationQueued = true;
            // 场景切换和资源刷新必须在当前 IMGUI 布局作用域全部结束后执行。
            EditorApplication.delayCall += CreateDeferred;
        }

        private void CreateDeferred()
        {
            creationQueued = false;
            if (this == null || step != 2)
                return;
            try
            {
                Create();
            }
            finally { if (this != null) Repaint(); }
        }

        private void Create()
        {
            InvalidateExistingValidation();
            RuntimeSettingsAsset.TryGet(out settings, out settingsError);
            RefreshPolicy();
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

            var sources = CreatePageSources();
            var createdFiles = new List<string>();
            var createdDirectories = new List<string>();
            AssetDatabase.DisallowAutoRefresh();
            try
            {
                EnsurePageDirectory(ScriptDirectory, createdDirectories);
                if (existingPrefab == null)
                    EnsurePageDirectory(PrefabDirectory, createdDirectories);
                var prefabPath = PrefabPath;
                if (existingPrefab == null)
                {
                    if (PathOccupied(prefabPath))
                        throw new IOException(L.Format("wizard.pathOccupied", prefabPath));
                    createdFiles.Add(prefabPath);
                    SavePrefab(prefabPath, inspection.AssemblyName);
                }
                sources = CreatePageSources();
                foreach (var file in sources)
                {
                    using (var stream = new FileStream(ScriptDirectory + "/" + file.Key, FileMode.CreateNew, FileAccess.Write))
                    {
                        createdFiles.Add(ScriptDirectory + "/" + file.Key);
                        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                        {
                            writer.Write(file.Value);
                        }
                    }
                }

                if (existingPrefab != null)
                    SaveExistingPageAssociation(prefabPath, inspection.AssemblyName);
                createdScriptDirectory = ScriptDirectory;
                createdPrefabPath = prefabPath;
                ChangeStep(3);
                Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(prefabPath);
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
            catch (Exception failure)
            {
                Debug.LogException(failure);
                var removed = RollbackPageFiles(createdFiles, createdDirectories);
                message = removed ? L.Get("editor.PageWizard.0156b77f5b") : L.Get("editor.PageWizard.4f3f610097") + ScriptDirectory;
            }
            finally
            {
                // 先保存同步请求，Refresh 触发编译/域重载后仍能恢复；失败回滚也同步删除结果。
                CompilerGeneratedSources.RequestProjectSync();
                AssetDatabase.AllowAutoRefresh();
                AssetDatabase.Refresh();
            }
        }

        private void SavePrefab(string path, string assemblyName)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject(pageName + "View", typeof(RectTransform), typeof(CanvasGroup), typeof(View));
                root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, scene);
                var rect = (RectTransform)root.transform;
                if (createWidget || selectedPolicy.PageRole != PageRole.Main)
                {
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = createWidget ? new Vector2(240, 120) : !ReceivesInput ? new Vector2(480, 80) : new Vector2(600, 400);
                }
                else
                {
                    Stretch(rect);
                }

                // 保留 View 根节点管理生命周期，将可停用的显示内容放到独立子树。
                var content = new GameObject("Content", typeof(RectTransform));
                content.transform.SetParent(rect, false);
                var contentRect = (RectTransform)content.transform;
                Stretch(contentRect);
                var view = root.GetComponent<View>();
                var serializedView = new SerializedObject(view);
                serializedView.FindProperty("hiddenMode").enumValueIndex = (int)ViewHiddenMode.DeactivateContent;
                serializedView.FindProperty("hiddenContentRoot").objectReferenceValue = content;
                serializedView.FindProperty("useGlobalModalBackdrop").boolValue = !createWidget;
                serializedView.ApplyModifiedPropertiesWithoutUndo();

                var background = content.AddComponent<Image>();
                background.color = new Color(0.12f, 0.14f, 0.18f, 1);
                var backend = PageTextBackend.Find(textBackendId);
                var title = backend.CreateText("Title", contentRect, pageName);
                AddNativeBinding(view, "Title", title, backend.ComponentType);
                title.rectTransform.anchorMin = new Vector2(0, 1);
                title.rectTransform.anchorMax = Vector2.one;
                title.rectTransform.pivot = new Vector2(0.5f, 1);
                title.rectTransform.anchoredPosition = new Vector2(-48, -16);
                title.rectTransform.sizeDelta = new Vector2(-128, 48);
                if (createWidget)
                {
                    title.rectTransform.anchoredPosition = new Vector2(0, -16);
                    title.rectTransform.sizeDelta = new Vector2(-32, 48);
                }
                if (!createWidget && !ReceivesInput)
                {
                    // 路由不抢焦点，原生图形也不拦截下层射线；提示由业务持有的句柄关闭。
                    var group = root.GetComponent<CanvasGroup>();
                    group.interactable = false;
                    group.blocksRaycasts = false;
                    background.raycastTarget = false;
                    title.raycastTarget = false;
                    Stretch(title.rectTransform);
                }
                else if (!createWidget)
                {
                    CreateCloseButton(view, contentRect, backend);
                }
                if (UsesAutomaticPageBindings)
                    SaveAutomaticPageBindings(view, assemblyName);
                else
                {
                    var association = new XElement("bindings", new XAttribute("assembly", assemblyName),
                        new XAttribute("model", targetNamespace + "." + pageName + "ViewModel"));
                    var associationView = new SerializedObject(view);
                    associationView.FindProperty("muiAutoBindings").stringValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(association.ToString(SaveOptions.DisableFormatting)));
                    associationView.ApplyModifiedPropertiesWithoutUndo();
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

        private static void CreateCloseButton(View view, RectTransform rect, PageTextBackend backend)
        {
            var close = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            close.transform.SetParent(rect, false);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = Vector2.one;
            closeRect.pivot = Vector2.one;
            closeRect.anchoredPosition = new Vector2(-16, -16);
            closeRect.sizeDelta = new Vector2(80, 48);
            close.GetComponent<Button>().targetGraphic = close.GetComponent<Image>();
            AddNativeBinding(view, "Close", close.GetComponent<Button>());
            // 默认模板使用基础字体包含的关闭符号；语义名称独立保留，字体和语言由项目配置。
            var label = backend.CreateText("Label", closeRect, "×");
            label.color = Color.black;
            Stretch(label.rectTransform);
        }

        private static void AddNativeBinding(View view, string name, Component target, Type componentType = null)
        {
            var serialized = new SerializedObject(view);
            var bindings = serialized.FindProperty("nativeBindings");
            var row = bindings.GetArrayElementAtIndex(bindings.arraySize++);
            row.FindPropertyRelative("Name").stringValue = name;
            row.FindPropertyRelative("Target").objectReferenceValue = target;
            row.FindPropertyRelative("ComponentType").stringValue = (componentType ?? target.GetType()).AssemblyQualifiedName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SaveAutomaticPageBindings(View view, string assemblyName)
        {
            var metadata = new XElement("bindings", new XAttribute("assembly", assemblyName),
                new XAttribute("model", targetNamespace + "." + pageName + "ViewModel"));
            var serialized = new SerializedObject(view);
            var selections = serialized.FindProperty("autoBindingSelections");
            selections.arraySize = ReceivesInput ? 2 : 1;
            AddSelection(0, "Title", "text", "Title", typeof(NativeControl), false);
            metadata.Element("binding").SetAttributeValue("initial", pageName);
            if (ReceivesInput)
                AddSelection(1, "Close", "onClick", "CloseCommand", typeof(NativeControl), true);
            serialized.FindProperty("muiAutoBindings").stringValue = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(metadata.ToString(SaveOptions.DisableFormatting)));
            serialized.ApplyModifiedPropertiesWithoutUndo();

            void AddSelection(int index, string control, string property, string source, Type contract, bool command)
            {
                metadata.Add(new XElement("binding", new XAttribute("name", control),
                    new XAttribute("contract", contract.FullName), new XAttribute("source", source),
                    new XAttribute("property", property), new XAttribute("mode", "OneWay"),
                    new XAttribute("command", command ? "true" : "false")));
                var component = command ? typeof(Button) : PageTextBackend.Find(textBackendId).ComponentType;
                var entry = metadata.Elements("binding").Last();
                entry.SetAttributeValue("component", component.FullName);
                entry.SetAttributeValue("targetComponent", component.FullName);
                var selection = selections.GetArrayElementAtIndex(index);
                selection.FindPropertyRelative("Control").stringValue = control;
                selection.FindPropertyRelative("ComponentType").stringValue = component.AssemblyQualifiedName;
                var properties = selection.FindPropertyRelative("Properties");
                properties.arraySize = 1;
                var choice = properties.GetArrayElementAtIndex(0);
                choice.FindPropertyRelative("Property").stringValue = property;
                choice.FindPropertyRelative("Source").stringValue = source;
                choice.FindPropertyRelative("Mode").intValue = (int)BindingMode.OneWay;
                choice.FindPropertyRelative("Native").boolValue = true;
                choice.FindPropertyRelative("Extension").stringValue = string.Empty;
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
