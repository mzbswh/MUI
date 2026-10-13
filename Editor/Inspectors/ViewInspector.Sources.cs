using System;
using System.Collections.Generic;
using System.IO;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public sealed partial class ViewInspector
    {
        private bool showBindingSources;
        private string sourceOpenError;
        private BindingEntry locatedBinding;
        private IReadOnlyList<Component> locatedElements;
        private string targetLookupError;

        private void ClearBindingTargets()
        {
            locatedBinding = null;
            locatedElements = null;
            targetLookupError = null;
        }

        private void OnBindingHierarchyChanged()
        {
            QueueBindingValidation();
            ClearBindingTargets();
            Repaint();
        }

        private void OnBindingUndoRedo()
        {
            if (this == null || target == null)
                return;
            serializedObject.Update();
            InitializeInspectorState();
            ClearBindingTargets();
            results = null;
            nativeBindingMessage = "已撤销 / 重做绑定配置；将自动同步并编译。";
            QueueBindingValidation();
            Repaint();
        }

        private void OnDisable()
        {
            // A selection change must not discard edits waiting for the debounce timer.
            if (validationPending && !EditorApplication.isCompiling)
            {
                validationDue = 0;
                RunPendingBindingValidation();
            }
            inspectorReady = false;
            EditorApplication.update -= RunPendingBindingValidation;
            Undo.postprocessModifications -= OnBindingModifications;
            Undo.undoRedoPerformed -= OnBindingUndoRedo;
            EditorApplication.delayCall -= SyncCodeBindingReferences;
            EditorApplication.hierarchyChanged -= OnBindingHierarchyChanged;
        }

        /// <summary>沿用选中的契约展示原始绑定声明，不实例化模型或执行绑定。</summary>
        private void DrawBindingSources(BindingManifest manifest)
        {
            showBindingSources = EditorGUILayout.Foldout(showBindingSources, L.Get("editor.ViewInspector.Sources.fa24d326cc"), true);
            if (!showBindingSources)
            {
                return;
            }

            foreach (var entry in manifest.Entries)
            {
                var hasLocation = !string.IsNullOrEmpty(entry.SourcePath) && entry.SourceLine > 0;
                var relation = entry.Source + " → " + entry.ElementName + "." + entry.TargetProperty;
                var location = hasLocation ? Path.GetFileName(entry.SourcePath) + ":" + entry.SourceLine
                    : L.Get("editor.ViewInspector.Sources.ba7542980d");
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent(relation, relation + "\n" + location), GUILayout.MinWidth(40));
                    if (GUILayout.Button(new GUIContent(L.Get("bindingRow.locate"), L.Get("bindingRow.locate.help")),
                        EditorStyles.miniButton, GUILayout.Width(48)))
                        LocateBindingTargets(entry);
                    using (new EditorGUI.DisabledScope(!hasLocation))
                    {
                        if (GUILayout.Button(new GUIContent(L.Get("bindingRow.source"),
                            hasLocation ? entry.SourcePath + ":" + entry.SourceLine : location),
                            EditorStyles.miniButton, GUILayout.Width(48)))
                            OpenBindingSource(entry);
                    }
                }
                DrawBindingTargets(entry);
                var divider = EditorGUILayout.GetControlRect(false, 1);
                EditorGUI.DrawRect(divider, new Color(0.5f, 0.5f, 0.5f, 0.18f));
            }

            if (manifest.Entries.Count == 0)
            {
                EditorGUILayout.HelpBox(L.Get("editor.ViewInspector.Sources.7ea0036985"), MessageType.Info);
            }

            if (!string.IsNullOrEmpty(sourceOpenError))
            {
                EditorGUILayout.HelpBox(L.Diagnostic(sourceOpenError), MessageType.Warning);
            }
        }

        private void LocateBindingTargets(BindingEntry entry)
        {
            ClearBindingTargets();
            locatedBinding = entry;
            try
            {
                locatedElements = ViewContractValidator.FindBindingTargets((View)target, entry);
                if (locatedElements.Count == 1 && locatedElements[0] != null)
                {
                    // 唯一匹配直接高亮，不展开重复的对象行，也不切走当前 Inspector。
                    EditorGUIUtility.PingObject(locatedElements[0].gameObject);
                }
            }
            catch (Exception error)
            {
                targetLookupError = L.Get("editor.ViewInspector.Sources.67bb7dc1f7") + error.Message;
            }
        }

        private void DrawBindingTargets(BindingEntry entry)
        {
            if (!ReferenceEquals(locatedBinding, entry))
            {
                return;
            }

            if (targetLookupError != null)
            {
                EditorGUILayout.HelpBox(L.Diagnostic(targetLookupError), MessageType.Error);
                return;
            }

            if (locatedElements == null || locatedElements.Count == 0)
            {
                EditorGUILayout.HelpBox(L.Get("editor.ViewInspector.Sources.2a6d9ccfea"), MessageType.Error);
                return;
            }

            if (locatedElements.Count > 1)
            {
                EditorGUILayout.HelpBox(L.Get("editor.ViewInspector.Sources.8efab5e68f"), MessageType.Error);
            }

            // 多个匹配时才展示候选，供用户逐一定位；正常匹配保持单行。
            if (locatedElements.Count == 1)
                return;
            foreach (var element in locatedElements)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.ObjectField(element, typeof(Component), true);
                    }

                    using (new EditorGUI.DisabledScope(element == null))
                    {
                        if (GUILayout.Button(L.Get("editor.ViewInspector.Sources.05f954565f"), GUILayout.Width(48)))
                        {
                            EditorGUIUtility.PingObject(element.gameObject);
                        }
                    }
                }
            }
        }

        private void OpenBindingSource(BindingEntry entry)
        {
            sourceOpenError = null;
            try
            {
                // AssetDatabase 能解析虚拟 Packages 路径；本地绝对路径则交给外部代码编辑器。
                var path = entry.SourcePath.Replace('\\', '/');
                if (IsAutoBinding(entry))
                {
                    if (EditorApplication.isCompiling)
                        throw new InvalidOperationException("Unity 正在编译，请等待编译完成后打开生成源码。");
                    path = CompilerGeneratedSources.Find(path);
                    if (path == null)
                    {
                        if (!CompilerGeneratedSources.OutputEnabled)
                        {
                            RuntimeSettingsAsset.OpenOrCreate();
                            sourceOpenError = "请在 MUI 全局设置的开发工具中开启“输出生成源码供 IDE 使用”。";
                            return;
                        }
                        CompilerGeneratedSources.Regenerate();
                        sourceOpenError = "已请求重新生成源码。请等待编译完成后再次点击源码；若仍未生成，请检查 Console 编译错误。";
                        return;
                    }
                    UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(path, Math.Max(1, entry.SourceLine));
                    return;
                }
                var script = path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal)
                    ? AssetDatabase.LoadAssetAtPath<MonoScript>(path)
                    : null;
                if (script != null)
                {
                    if (!AssetDatabase.OpenAsset(script, entry.SourceLine))
                    {
                        sourceOpenError = L.Get("editor.ViewInspector.Sources.679ca80186");
                    }

                    return;
                }

                if (!File.Exists(path))
                {
                    sourceOpenError = L.Get("editor.ViewInspector.Sources.7ae12e0993") + path;
                    return;
                }

                UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(Path.GetFullPath(path), entry.SourceLine);
            }
            catch (Exception error)
            {
                sourceOpenError = L.Get("editor.ViewInspector.Sources.1b2ff64e53") + error.Message;
            }
        }
    }
}
