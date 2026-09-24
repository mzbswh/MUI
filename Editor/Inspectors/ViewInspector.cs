using System;
using System.Collections.Generic;
using System.Reflection;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;

namespace MUI.Editor
{
    [CustomEditor(typeof(View))]
    public sealed partial class ViewInspector : UnityEditor.Editor
    {
        private readonly List<BindingManifest> manifests = new List<BindingManifest>();
        private string[] labels = Array.Empty<string>();
        private int selected;
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
            labels[0] = "请选择 ViewModel 契约";
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
            DrawDefaultInspector();
            DrawInputDiagnostics();
            DrawResourcePreparationDiagnostics();
            DrawRaycastDiagnostics();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("MUI Binding Contract", EditorStyles.boldLabel);
            if (GUILayout.Button("Validate View Structure"))
            {
                validationKind = ValidationKind.Structure;
                results = ViewContractValidator.ValidateStructure((View)target);
            }

            if (manifests.Count == 0)
            {
                EditorGUILayout.HelpBox("No generated manifests found. Compile a partial ViewModel with ViewContract or Bind attributes.", MessageType.Info);
            }
            else
            {
                var next = EditorGUILayout.Popup("ViewModel", selected + 1, labels) - 1;
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
                    EditorGUILayout.HelpBox("选择此 View 使用的 ViewModel 契约后再校验绑定。结构校验不需要选择契约。", MessageType.Info);
                }

                using (new EditorGUI.DisabledScope(selected < 0))
                {
                    if (GUILayout.Button("Validate Binding Contract"))
                    {
                        validationKind = ValidationKind.Contract;
                        results = ViewContractValidator.Validate((View)target, manifests[selected]);
                    }
                }
            }

            if (GUILayout.Button("Validate Accessibility Semantics"))
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
                EditorGUILayout.HelpBox(validationKind == ValidationKind.Accessibility ? "No basic semantic issues found. Keyboard reachability and platform screen readers were not validated." : validationKind == ValidationKind.Structure ? "No structural issues found. Binding contract was not checked." : "The selected binding contract matches this View.", MessageType.Info);
            }

            foreach (var result in results)
            {
                EditorGUILayout.HelpBox(result, MessageType.Error);
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
