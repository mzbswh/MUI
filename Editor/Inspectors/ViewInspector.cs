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
        private readonly List<BindingManifest> manifests = new List<BindingManifest>();
        private string[] labels = Array.Empty<string>();
        private int selected;
        private string displayedLanguage;
        private IReadOnlyList<string> results;
        private ValidationKind validationKind;

        private void OnEnable()
        {
            ClearBindingTargets();
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
                    if (property.GetValue(null) is BindingManifest manifest)
                    {
                        manifests.Add(manifest);
                    }
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                }
            }

            manifests.Sort((left, right) => string.Compare(left.ViewModelType.FullName, right.ViewModelType.FullName, StringComparison.Ordinal));
            labels = new string[manifests.Count + 1];
            labels[0] = L.Get("editor.ViewInspector.6541599e58");
            for (var i = 0; i < manifests.Count; ++i)
            {
                labels[i + 1] = manifests[i].ViewModelType.FullName;
            }

            // 资源键不等于节点名；多个契约时不猜测当前 View 属于哪个模型。
            if (manifests.Count == 1)
            {
                selected = 0;
            }
        }

        public override void OnInspectorGUI()
        {
            if (displayedLanguage != L.LanguageId)
            {
                displayedLanguage = L.LanguageId;
                labels[0] = L.Get("editor.ViewInspector.6541599e58");
                resourceManifest = null;
                results = null;
            }
            Localization.MUIEditorInspector.Draw(serializedObject);
            var inspectedView = (View)target;
            EditorGUILayout.HelpBox(L.Get("sorting.targets.help"), MessageType.Info);
            if (inspectedView.HiddenMode == ViewHiddenMode.DeactivateContent)
            {
                var root = inspectedView.HiddenContentRoot;
                var valid = root != null && root != inspectedView.gameObject && root.transform.IsChildOf(inspectedView.transform);
                EditorGUILayout.HelpBox(L.Get(valid ? "policy.hidden.help" : "policy.hidden.invalid"),
                    valid ? MessageType.Info : MessageType.Error);
            }
            DrawInputDiagnostics();
            DrawResourcePreparationDiagnostics();
            DrawRaycastDiagnostics();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(L.Get("editor.ViewInspector.dceaeea259"), EditorStyles.boldLabel);
            if (GUILayout.Button(L.Get("editor.ViewInspector.52e088acdc")))
            {
                validationKind = ValidationKind.Structure;
                results = ViewContractValidator.ValidateStructure((View)target);
            }

            if (manifests.Count == 0)
            {
                EditorGUILayout.HelpBox(L.Get("editor.ViewInspector.e32a3bfa20"), MessageType.Info);
            }
            else
            {
                var next = EditorGUILayout.Popup(L.Get("editor.ViewInspector.b741b9457b"), selected + 1, labels) - 1;
                if (next != selected)
                {
                    selected = next;
                    ClearBindingTargets();
                    sourceOpenError = null;
                    if (validationKind != ValidationKind.Structure)
                    {
                        results = null;
                    }
                }

                if (selected >= 0)
                {
                    DrawBindingSources(manifests[selected]);
                    DrawResourceBindings(manifests[selected]);
                }
                else
                {
                    EditorGUILayout.HelpBox(L.Get("editor.ViewInspector.ad9d661681"), MessageType.Info);
                }

                using (new EditorGUI.DisabledScope(selected < 0))
                {
                    if (GUILayout.Button(L.Get("editor.ViewInspector.5f472e2647")))
                    {
                        validationKind = ValidationKind.Contract;
                        results = ViewContractValidator.Validate((View)target, manifests[selected]);
                    }
                }
            }

            if (GUILayout.Button(L.Get("editor.ViewInspector.b104369e08")))
            {
                validationKind = ValidationKind.Accessibility;
                results = ViewContractValidator.ValidateAccessibility((View)target, selected < 0 ? null : manifests[selected]);
            }

            if (results == null)
            {
                return;
            }

            if (results.Count == 0)
            {
                EditorGUILayout.HelpBox(validationKind == ValidationKind.Accessibility ? L.Get("editor.ViewInspector.f9d933d15c") : validationKind == ValidationKind.Structure ? L.Get("editor.ViewInspector.48bcffa0d4") : L.Get("editor.ViewInspector.3f95319fe9"), MessageType.Info);
            }

            foreach (var result in results)
            {
                EditorGUILayout.HelpBox(L.Diagnostic(result), MessageType.Error);
            }
        }

        private enum ValidationKind
        {
            Structure,
            Contract,
            Accessibility
        }
    }
}
