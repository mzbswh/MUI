using System;
using System.Collections.Generic;
using System.Reflection;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    [CustomEditor(typeof(View))]
    public sealed partial class ViewInspector : UnityEditor.Editor
    {
        private List<BindingManifest> manifests = new List<BindingManifest>();
        private bool inspectorReady;
        private string[] labels = Array.Empty<string>();
        private int selected;
        private string displayedLanguage;
        private IReadOnlyList<string> results;
        private ValidationKind validationKind;

        private void OnEnable()
        {
            RestoreBindingColumnWidths();
            InitializeInspectorState();
        }

        private void InitializeInspectorState()
        {
            inspectorReady = false;
            if (manifests == null) manifests = new List<BindingManifest>();
            ClearBindingTargets();
            Undo.undoRedoPerformed -= OnBindingUndoRedo;
            Undo.undoRedoPerformed += OnBindingUndoRedo;
            EditorApplication.hierarchyChanged -= OnBindingHierarchyChanged;
            EditorApplication.hierarchyChanged += OnBindingHierarchyChanged;
            resourcePreparationSnapshot = null;
            resourcePreparationError = null;
            inputSnapshot = null;
            inputSnapshotError = null;
            raycastSnapshot = null;
            raycastError = null;
            sourceOpenError = null;
            manifests.Clear();
            resourceManifest = null;
            resourceBindingRows.Clear();
            results = null;
            selected = -1;
            // 仅发现编辑器元数据，不创建业务 ViewModel 或 Presenter。
            foreach (var type in TypeCache.GetTypesWithAttribute<BindingFactoryAttribute>())
            {
                // 开放泛型没有可读取的静态清单；项目可显式提供闭合类型的清单。
                if (type.ContainsGenericParameters)
                {
                    continue;
                }

                var property = type.GetProperty("Manifest", BindingFlags.Public | BindingFlags.Static);
                if (property == null || property.PropertyType != typeof(BindingManifest))
                {
                    continue;
                }

                try
                {
                    if (property.GetValue(null) is BindingManifest manifest && manifest.ViewModelType != null)
                    {
                        manifests.Add(manifest);
                    }
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                }
            }

            manifests.RemoveAll(manifest => !MatchesCurrentPrefab(manifest));
            manifests.Sort((left, right) => string.Compare(left.ViewModelType.FullName, right.ViewModelType.FullName, StringComparison.Ordinal));
            labels = new string[manifests.Count + 1];
            labels[0] = L.Get("editor.ViewInspector.6541599e58");
            for (var i = 0; i < manifests.Count; ++i)
            {
                labels[i + 1] = manifests[i].ViewModelType.FullName;
            }

            // 只有唯一匹配当前 Prefab 的契约才自动选中。
            if (manifests.Count == 1)
            {
                selected = 0;
            }
            inspectorReady = true;
            Undo.postprocessModifications -= OnBindingModifications;
            Undo.postprocessModifications += OnBindingModifications;
            QueueCodeBindingSync();
            QueueBindingValidation();
        }

        private bool MatchesCurrentPrefab(BindingManifest manifest)
        {
            var view = (View)target;
            // 只关联 Prefab 根 View，不能把页面契约误认给内部嵌套 View。
            var source = EditorUtility.IsPersistent(view) ? view : PrefabUtility.GetCorrespondingObjectFromSource(view);
            string path;
            if (source != null)
            {
                if (source.transform.parent != null) return false;
                path = AssetDatabase.GetAssetPath(source);
            }
            else
            {
                var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(view.gameObject);
                if (stage == null || stage.prefabContentsRoot != view.gameObject) return false;
                path = stage.assetPath;
            }
            if (string.IsNullOrEmpty(path)) return false;
            var model = manifest.ViewModelType;
            var contract = model.GetCustomAttribute<ViewContractAttribute>();
            var guid = contract == null ? null : contract.PrefabAssetGuid;
            if (!string.IsNullOrEmpty(guid))
                return string.Equals(guid, AssetDatabase.AssetPathToGUID(path), StringComparison.OrdinalIgnoreCase);
            const string marker = "/Resources/";
            var start = path.LastIndexOf(marker, StringComparison.Ordinal);
            if (start < 0 || contract == null) return false;
            var key = path.Substring(start + marker.Length);
            key = key.Substring(0, key.Length - System.IO.Path.GetExtension(key).Length);
            return string.Equals(contract.ResourceKey, key, StringComparison.Ordinal);
        }

        private void DrawBindings()
        {
            if (this == null || !inspectorReady || target == null || manifests == null) return;
            if (selected >= manifests.Count) selected = -1;
            if (displayedLanguage != L.LanguageId)
            {
                displayedLanguage = L.LanguageId;
                if (labels != null && labels.Length > 0) labels[0] = L.Get("editor.ViewInspector.6541599e58");
                resourceManifest = null;
                results = null;
            }
            if (manifests.Count == 0)
            {
                EditorGUILayout.HelpBox(L.Get("viewPanel.binding.unmatched"), MessageType.Info);
                DrawNativeBindings();
            }
            else
            {
                var next = selected;
                if (manifests.Count == 1)
                    EditorGUILayout.LabelField(new GUIContent(L.Get("editor.ViewInspector.b741b9457b")),
                        new GUIContent(manifests[0].ViewModelType.FullName, manifests[0].ViewModelType.FullName));
                else
                {
                    EditorGUILayout.HelpBox(L.Get("viewPanel.binding.multiple"), MessageType.Info);
                    next = EditorGUILayout.Popup(L.Get("editor.ViewInspector.b741b9457b"), selected + 1, labels) - 1;
                }
                if (next != selected)
                {
                    selected = next;
                    QueueCodeBindingSync();
                    QueueBindingValidation();
                    ClearBindingTargets();
                    sourceOpenError = null;
                    if (validationKind != ValidationKind.Structure)
                    {
                        results = null;
                    }
                }

                DrawNativeBindings();
                if (selected >= 0)
                {
                    DrawResourceBindings(manifests[selected]);
                }
                else
                {
                    EditorGUILayout.HelpBox(L.Get("editor.ViewInspector.ad9d661681"), MessageType.Info);
                }

            }

            if (results == null)
            {
                return;
            }

            foreach (var result in results)
            {
                EditorGUILayout.HelpBox(L.Diagnostic(result), MessageType.Error);
            }
        }

        private enum ValidationKind
        {
            Structure,
            Contract
        }
    }
}
