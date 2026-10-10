using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MUI.Navigation;
using MUI.UGUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public sealed partial class MUIDashboard : EditorWindow
    {
        [SerializeField] private UIHost selectedHost = null;
        [SerializeField] private string activeTab = "pages";
        [SerializeField] private MUISettings inspectedSettings = null;
        [SerializeField] private bool refreshAutomatically;
        private readonly List<UIHost> hosts = new List<UIHost>();
        private readonly List<ViewInstanceSnapshot> pages = new List<ViewInstanceSnapshot>();
        private NavigationSnapshot snapshot;
        private NavigationTraceSnapshot trace;
        private ViewHandle selectedHandle;
        private string capturedAt;
        private string viewDiagnostics;
        private DropdownField hostChoices;
        private Label summary;
        private HelpBox message;
        private Label statusNote;
        private ListView pageList;
        private ToolbarSearchField search;
        private ScrollView details;
        private Toggle autoRefresh;
        private ObjectField settingsField;
        private VisualElement settingsInspector;
        private Label validationReport;
        private Label traceSummary;
        private Label traceReport;
        private ToolbarSearchField traceSearch;
        private bool updating;
        private double nextRefresh;
        private bool runtimeAvailable;

        [MenuItem("Tools/MUI/控制台 (Dashboard)")]
        public static void Open() => GetWindow<MUIDashboard>(L.Get("editor.MUIDashboard.788f44b794"));

        public static void Show(UIHost host)
        {
            var window = GetWindow<MUIDashboard>(L.Get("editor.MUIDashboard.788f44b794"));
            window.selectedHost = host;
            window.ResetSnapshots();
            window.RefreshHosts();
            window.ShowHostSettings();
            window.Capture();
            window.Focus();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.hierarchyChanged += RefreshHosts;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.hierarchyChanged -= RefreshHosts;
            snapshot = null;
            trace = null;
            hosts.Clear();
            pages.Clear();
        }

        public void CreateGUI()
        {
            minSize = new Vector2(500, 420);
            rootVisualElement.Clear();
            hostChoices = null;
            summary = null;
            autoRefresh = null;
            languageChoices = null;
            displayedSettings = null;
            settingsDisplayed = false;
            var script = MonoScript.FromScriptableObject(this);
            var directory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(script));
            var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(directory + "/MUIDashboard.uxml");
            if (template == null)
            {
                rootVisualElement.Add(new HelpBox(L.Get("editor.MUIDashboard.13d2975890"), HelpBoxMessageType.Error));
                return;
            }
            template.CloneTree(rootVisualElement);
            MUIEditorControls.Style(rootVisualElement, this);
            var surface = rootVisualElement.Q<VisualElement>(className: "mui-dashboard");
            L.BindTree(surface);
            hostChoices = rootVisualElement.Q<DropdownField>("hosts");
            summary = rootVisualElement.Q<Label>("summary");
            message = rootVisualElement.Q<HelpBox>("message");
            statusNote = rootVisualElement.Q<Label>("statusNote");
            pageList = rootVisualElement.Q<ListView>("pages");
            search = rootVisualElement.Q<ToolbarSearchField>("search");
            details = rootVisualElement.Q<ScrollView>("details");
            autoRefresh = rootVisualElement.Q<Toggle>("autoRefresh");
            settingsField = rootVisualElement.Q<ObjectField>("settings");
            settingsInspector = rootVisualElement.Q<VisualElement>("settingsInspector");
            validationReport = rootVisualElement.Q<Label>("validationReport");
            traceSummary = rootVisualElement.Q<Label>("traceSummary");
            traceReport = rootVisualElement.Q<Label>("traceReport");
            traceSearch = rootVisualElement.Q<ToolbarSearchField>("traceSearch");
            autoRefresh.SetValueWithoutNotify(refreshAutomatically);
            autoRefresh.RegisterValueChangedCallback(evt => refreshAutomatically = evt.newValue);
            settingsField.SetValueWithoutNotify(inspectedSettings);
            hostChoices.RegisterValueChangedCallback(_ =>
            {
                if (!updating && hostChoices.index >= 0 && hostChoices.index < hosts.Count)
                {
                    selectedHost = hosts[hostChoices.index];
                    ResetSnapshots();
                    ShowHostSettings();
                    Capture();
                }
            });
            search.RegisterValueChangedCallback(_ => RebuildPages());
            traceSearch.RegisterValueChangedCallback(_ => ShowTrace());
            pageList.itemsSource = pages;
            pageList.makeItem = () =>
            {
                var row = new VisualElement();
                row.AddToClassList("mui-dashboard-row");
                var title = new Label { name = "route" };
                title.AddToClassList("mui-dashboard-page-title");
                row.Add(title);
                var metadata = new Label { name = "metadata" };
                metadata.AddToClassList("mui-dashboard-page-meta");
                row.Add(metadata);
                return row;
            };
            pageList.bindItem = (element, index) =>
            {
                var item = pages[index];
                var title = element.Q<Label>("route");
                title.text = "#" + item.Handle.Id + "  " + item.RouteKey;
                title.tooltip = item.RouteKey;
                element.Q<Label>("metadata").text = L.Format("dashboard.page.meta", item.Layer, item.State, item.Focused);
            };
            pageList.selectionChanged += values =>
            {
                if (updating)
                {
                    return;
                }
                foreach (ViewInstanceSnapshot item in values)
                {
                    if (selectedHandle != item.Handle)
                    {
                        viewDiagnostics = null;
                    }
                    selectedHandle = item.Handle;
                    ShowPage(item);
                    return;
                }
            };
            Bind("refreshHosts", RefreshHosts);
            Bind("locateHost", () =>
            {
                if (selectedHost != null)
                {
                    Selection.activeObject = selectedHost;
                    EditorGUIUtility.PingObject(selectedHost.gameObject);
                }
            });
            Bind("capture", Capture);
            Bind("overview", () =>
            {
                selectedHandle = default;
                viewDiagnostics = null;
                RebuildPages();
            });
            Bind("pagesTab", () => SelectTab("pages"));
            Bind("traceTab", () => SelectTab("trace"));
            Bind("settingsTab", () => SelectTab("settings"));
            BindConfigurationAndTrace();
            ShowSettings();
            RefreshHosts();
            SelectTab(activeTab);
            Capture();
            BindLanguage();
            surface.RegisterCallback<GeometryChangedEvent>(evt => UpdateResponsiveLayout(evt.newRect.width));
            surface.schedule.Execute(() => UpdateResponsiveLayout(surface.resolvedStyle.width));
        }

        private void Bind(string name, Action action) => rootVisualElement.Q<Button>(name).clicked += action;

        private void SelectTab(string selected)
        {
            activeTab = selected;
            foreach (var tab in new[] { "pages", "trace", "settings" })
            {
                rootVisualElement.Q<VisualElement>(tab + "Panel").EnableInClassList("mui-dashboard-hidden", tab != selected);
                rootVisualElement.Q<Button>(tab + "Tab").EnableInClassList("mui-dashboard-active-tab", tab == selected);
            }
        }

        private void RefreshHosts()
        {
            if (hostChoices == null)
            {
                return;
            }
            hosts.Clear();
            hosts.AddRange(UnityEngine.Resources.FindObjectsOfTypeAll<UIHost>().Where(host => host != null &&
                !EditorUtility.IsPersistent(host) && host.gameObject.scene.IsValid() && host.gameObject.scene.isLoaded &&
                !EditorSceneManager.IsPreviewScene(host.gameObject.scene)).OrderBy(host => host.gameObject.scene.name).ThenBy(host => host.name));
            var previous = selectedHost;
            if (selectedHost == null || !hosts.Contains(selectedHost))
            {
                selectedHost = hosts.Count == 0 ? null : hosts[0];
            }
            updating = true;
            try
            {
                hostChoices.choices = hosts.Select(host => $"{host.gameObject.scene.name} / {host.name} ({host.GetInstanceID()})").ToList();
                hostChoices.index = selectedHost == null ? -1 : hosts.IndexOf(selectedHost);
            }
            finally
            {
                updating = false;
            }
            if (previous != selectedHost || (selectedHost == null && snapshot != null))
            {
                ResetSnapshots();
                ShowHostSettings();
                Capture();
            }
            else if (settingsField != null && settingsField.value == null)
            {
                ShowHostSettings();
            }
            UpdateActions();
        }

        private void Capture()
        {
            if (summary == null)
            {
                return;
            }
            if (!Application.isPlaying || selectedHost == null || !selectedHost.IsInitialized)
            {
                ResetSnapshots();
                summary.text = selectedHost == null ? L.Get("editor.MUIDashboard.329dce8f48") :
                    L.Format("editor.MUIDashboard.1797c05538", selectedHost.name, (selectedHost.IsInitialized ? L.Get("editor.MUIDashboard.c6d125d5b7") : L.Get("editor.MUIDashboard.1e5827d7eb")), L.Get(selectedHost.AutomaticFramePump ? "settings.update.automatic" : "settings.update.manual"));
                SetMessage(L.Get("editor.MUIDashboard.7d7704c517"), HelpBoxMessageType.Info);
                ShowOverview();
                RefreshEmptyPages();
                return;
            }
            Run(() =>
            {
                var limit = rootVisualElement.Q<IntegerField>("snapshotLimit").value;
                snapshot = selectedHost.Navigator.CaptureSnapshot(limit, limit);
                trace = selectedHost.Navigator.CaptureLifecycleTrace();
                capturedAt = DateTime.Now.ToString("HH:mm:ss");
                summary.text = L.Format("dashboard.summary", capturedAt, snapshot.TotalInstances, snapshot.Focused.Id,
                    snapshot.PendingRequestCount + snapshot.PostedRequestCount, snapshot.CachedViewCount);
                var truncated = snapshot.InstancesTruncated || snapshot.HistoryTruncated || snapshot.CleanupResponsibilitiesTruncated;
                SetMessage((snapshot.IsPresentationSettled ? L.Get("editor.MUIDashboard.edf200a690") : L.Get("editor.MUIDashboard.bd66520cbe")) +
                    (truncated ? L.Get("editor.MUIDashboard.8c72555563") : string.Empty) + L.Get("editor.MUIDashboard.1cf88f53d4"),
                    snapshot.IsPresentationSettled && !truncated ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning);
                RebuildPages();
                ShowTrace();
                UpdateActions();
            }, clearSnapshotOnFailure: true);
        }

        private void ResetSnapshots()
        {
            snapshot = null;
            trace = null;
            selectedHandle = default;
            viewDiagnostics = null;
            pages.Clear();
            if (pageList != null)
            {
                pageList.Rebuild();
                details.Clear();
                summary.text = string.Empty;
                RefreshEmptyPages();
                ShowOverview();
                ShowTrace();
                UpdateActions();
            }
        }

        private void UpdateActions()
        {
            if (hostChoices == null)
            {
                return;
            }
            var canCapture = Application.isPlaying && selectedHost != null && selectedHost.IsInitialized;
            var settings = settingsField.value as MUISettings;
            rootVisualElement.Q<Button>("capture").SetEnabled(canCapture);
            rootVisualElement.Q<Button>("locateHost").SetEnabled(selectedHost != null);
            rootVisualElement.Q<Button>("copySnapshot").SetEnabled(snapshot != null);
            rootVisualElement.Q<Button>("overview").SetEnabled(snapshot != null);
            rootVisualElement.Q<Button>("hostSettings").SetEnabled(selectedHost != null);
            rootVisualElement.Q<Button>("locateSettings").SetEnabled(settings != null);
            rootVisualElement.Q<Button>("validateSettings").SetEnabled(settings != null);
            rootVisualElement.Q<Button>("assignSettings").SetEnabled(settings != null && selectedHost != null &&
                !Application.isPlaying && !selectedHost.IsInitialized && selectedHost.Settings != settings);
            rootVisualElement.Q<Button>("startTrace").SetEnabled(canCapture && (trace == null || !trace.IsRecording));
            rootVisualElement.Q<Button>("stopTrace").SetEnabled(canCapture && trace != null && trace.IsRecording);
            rootVisualElement.Q<Button>("clearTrace").SetEnabled(canCapture && trace != null && (trace.IsRecording || trace.Entries.Count != 0));
            rootVisualElement.Q<Button>("captureTrace").SetEnabled(canCapture);
            rootVisualElement.Q<Button>("copyTrace").SetEnabled(trace != null && trace.Entries.Count != 0);
            if (runtimeAvailable != canCapture && snapshot == null)
            {
                ShowOverview();
                RefreshEmptyPages();
                SetMessage(L.Get(canCapture ? "dashboard.capture.ready" : "editor.MUIDashboard.7d7704c517"), HelpBoxMessageType.Info);
            }
            runtimeAvailable = canCapture;
        }

        private void OnEditorUpdate()
        {
            if (autoRefresh == null || rootVisualElement.panel == null ||
                EditorApplication.timeSinceStartup < nextRefresh)
            {
                return;
            }
            nextRefresh = EditorApplication.timeSinceStartup + 0.5;
            UpdateActions();
            if (autoRefresh.value)
            {
                Capture();
            }
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            ResetSnapshots();
            RefreshHosts();
            Capture();
        }

        private void Run(Action action, bool clearSnapshotOnFailure = false)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                if (clearSnapshotOnFailure)
                {
                    ResetSnapshots();
                }
                SetMessage(L.Diagnostic(error.Message), HelpBoxMessageType.Error);
            }
        }

        private void SetMessage(string text, HelpBoxMessageType type)
        {
            message.text = L.Diagnostic(text);
            message.messageType = type;
            message.EnableInClassList("mui-dashboard-hidden", type == HelpBoxMessageType.Info);
            statusNote.text = type == HelpBoxMessageType.Info ? message.text : string.Empty;
            statusNote.style.display = type == HelpBoxMessageType.Info ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
