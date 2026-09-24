using System;
using System.Collections.Generic;
using System.IO;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;

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
            showBindingSources = EditorGUILayout.Foldout(showBindingSources, "绑定声明与源码", true);
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
                        EditorGUILayout.LabelField("此清单未提供源码位置，请使用新版生成器重新编译。");
                        continue;
                    }

                    EditorGUILayout.LabelField(new GUIContent(Path.GetFileName(entry.SourcePath) + ":" + entry.SourceLine, entry.SourcePath));
                    if (GUILayout.Button("打开声明"))
                    {
                        OpenBindingSource(entry);
                    }
                }
            }

            if (manifest.Entries.Count == 0)
            {
                EditorGUILayout.HelpBox("当前契约没有绑定声明。", MessageType.Info);
            }

            if (!string.IsNullOrEmpty(sourceOpenError))
            {
                EditorGUILayout.HelpBox(sourceOpenError, MessageType.Warning);
            }
        }

        private void DrawBindingTargets(BindingEntry entry)
        {
            if (GUILayout.Button("定位控件"))
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
                    targetLookupError = "定位控件失败：" + error.Message;
                }
            }

            if (!ReferenceEquals(locatedBinding, entry))
            {
                return;
            }

            if (targetLookupError != null)
            {
                EditorGUILayout.HelpBox(targetLookupError, MessageType.Error);
                return;
            }

            if (locatedElements == null || locatedElements.Count == 0)
            {
                EditorGUILayout.HelpBox("当前 View 绑定边界内没有匹配的控件，请检查名称与组件类型。", MessageType.Error);
                return;
            }

            if (locatedElements.Count > 1)
            {
                EditorGUILayout.HelpBox("找到多个匹配控件，绑定存在歧义。请定位并修正重名，框架不会自动选择其中一个。", MessageType.Error);
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
                        if (GUILayout.Button("高亮", GUILayout.Width(48)))
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
                        sourceOpenError = "无法打开声明，请检查 Unity 的外部代码编辑器设置。";
                    }

                    return;
                }

                if (!File.Exists(path))
                {
                    sourceOpenError = "声明文件已移动或无法访问，请重新编译生成绑定：" + path;
                    return;
                }

                UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(Path.GetFullPath(path), entry.SourceLine);
            }
            catch (Exception error)
            {
                sourceOpenError = "打开绑定声明失败：" + error.Message;
            }
        }
    }
}
