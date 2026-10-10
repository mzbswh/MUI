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
        private IReadOnlyList<Element> locatedElements;
        private string targetLookupError;

        private void ClearBindingTargets()
        {
            locatedBinding = null;
            locatedElements = null;
            targetLookupError = null;
        }

        private void OnBindingHierarchyChanged()
        {
            ClearBindingTargets();
            Repaint();
        }

        private void OnDisable()
        {
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
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(entry.Source + " → " + entry.ElementName + "." + entry.TargetProperty);
                    DrawBindingTargets(entry);
                    var hasLocation = !string.IsNullOrEmpty(entry.SourcePath) && entry.SourceLine > 0;
                    if (!hasLocation)
                    {
                        EditorGUILayout.LabelField(L.Get("editor.ViewInspector.Sources.ba7542980d"));
                        continue;
                    }

                    EditorGUILayout.LabelField(new GUIContent(Path.GetFileName(entry.SourcePath) + ":" + entry.SourceLine, entry.SourcePath));
                    if (GUILayout.Button(L.Get("editor.ViewInspector.Sources.a7f7f7a203")))
                    {
                        OpenBindingSource(entry);
                    }
                }
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

        private void DrawBindingTargets(BindingEntry entry)
        {
            if (GUILayout.Button(L.Get("editor.ViewInspector.Sources.d90e1d2f55")))
            {
                ClearBindingTargets();
                locatedBinding = entry;
                try
                {
                    locatedElements = ViewContractValidator.FindBindingTargets((View)target, entry);
                    if (locatedElements.Count == 1 && locatedElements[0] != null)
                    {
                        // 只高亮目标，不切走当前 Inspector，便于继续对照声明。
                        EditorGUIUtility.PingObject(locatedElements[0].gameObject);
                    }
                }
                catch (Exception error)
                {
                    targetLookupError = L.Get("editor.ViewInspector.Sources.67bb7dc1f7") + error.Message;
                }
            }

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

            foreach (var element in locatedElements)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.ObjectField(element, typeof(Element), true);
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
