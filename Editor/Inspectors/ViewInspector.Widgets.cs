using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;

namespace MUI.Editor
{
    public sealed partial class ViewInspector
    {
        private GameObject widgetPrefab;
        private string widgetMessage;

        internal static Type ChildWidgetModel(View view)
        {
            if (view == null || string.IsNullOrEmpty(view.AutoBindingsMetadata))
                return null;
            var data = XElement.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(view.AutoBindingsMetadata)));
            var model = Type.GetType((string)data.Attribute("model") + ", " + (string)data.Attribute("assembly"), false);
            return model != null && model.GetCustomAttribute<UIWidgetAttribute>() != null ? model : null;
        }

        private List<ChildWidgetBinding> FindChildWidgets()
        {
            var view = (View)target;
            var result = view.ChildWidgets.Select(item => item == null ? null : new ChildWidgetBinding
            {
                Id = item.Id,
                Name = item.Name,
                Source = item.Source,
                Target = item.Target,
                CreateVM = item.CreateVM
            }).ToList();
            foreach (var child in view.GetComponentsInChildren<View>(true))
            {
                if (!view.OwnsChildWidget(child) || result.Any(item => item != null && item.Target == child))
                    continue;
                if (ChildWidgetModel(child) == null)
                    continue;
                var name = child.name;
                if (name.EndsWith("(Clone)", StringComparison.Ordinal))
                    name = name.Substring(0, name.Length - 7);
                result.Add(new ChildWidgetBinding
                {
                    Name = name,
                    Source = name.EndsWith("View", StringComparison.Ordinal) ? name.Substring(0, name.Length - 4) : name,
                    Target = child,
                    CreateVM = ChildWidgetModel(child).GetConstructor(Type.EmptyTypes) != null
                });
            }
            return result;
        }

        private void DrawChildWidgets()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("子 Widget", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("拖入 Widget 预制体或将实例放入当前 View。配置变化后自动登记引用，不添加包装节点；子控件由子 View 管理。", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    widgetPrefab = (GameObject)EditorGUILayout.ObjectField("Widget Prefab", widgetPrefab, typeof(GameObject), false);
                    if (GUILayout.Button("添加引用", GUILayout.Width(80)))
                    {
                        try
                        {
                            if (widgetPrefab == null || !PrefabUtility.IsPartOfPrefabAsset(widgetPrefab) || AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(widgetPrefab)) != widgetPrefab)
                                throw new InvalidOperationException("请选择 Widget 预制体根资产。");
                            if (ChildWidgetModel(widgetPrefab.GetComponent<View>()) == null)
                                throw new InvalidOperationException("预制体根 View 必须关联有效的 UIWidget 模型。");
                            RequireWidgetPrefabEditing();
                            var parent = ((View)target).transform;
                            var selection = Selection.activeTransform;
                            if (selection != null && selection.IsChildOf(parent) && selection.GetComponentInParent<View>(true) == target)
                                parent = selection;
                            var instance = (GameObject)PrefabUtility.InstantiatePrefab(widgetPrefab, parent);
                            Undo.RegisterCreatedObjectUndo(instance, "添加 Widget 引用");
                            widgetMessage = "已添加实例；确认属性名后自动同步绑定。";
                            QueueBindingValidation();
                        }
                        catch (Exception error) { widgetMessage = error.Message; }
                    }
                }
                try
                {
                    var rows = FindChildWidgets();
                    var changed = false;
                    foreach (var row in rows)
                    {
                        if (row == null)
                        {
                            EditorGUILayout.HelpBox("存在空 Widget 引用记录，请修复序列化数据。", MessageType.Error);
                            continue;
                        }
                        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                        {
                            EditorGUI.BeginChangeCheck();
                            row.Target = (View)EditorGUILayout.ObjectField("子 View", row.Target, typeof(View), true);
                            row.Name = EditorGUILayout.TextField("绑定名称", row.Name);
                            row.Source = EditorGUILayout.TextField("父 VM 属性", row.Source);
                            row.CreateVM = EditorGUILayout.Toggle("自动创建子 VM", row.CreateVM);
                            changed |= EditorGUI.EndChangeCheck();
                            var model = ChildWidgetModel(row.Target);
                            EditorGUILayout.LabelField("子 VM 类型", model == null ? "关联失效" : model.FullName);
                            if (string.IsNullOrEmpty(row.Id))
                                EditorGUILayout.LabelField("待登记：生成绑定时保存");
                            if (GUILayout.Button("移除实例和引用"))
                            {
                                var kept = rows.Where(item => !ReferenceEquals(item, row)).ToList();
                                RequireWidgetPrefabEditing();
                                Undo.IncrementCurrentGroup();
                                var undo = Undo.GetCurrentGroup();
                                Undo.SetCurrentGroupName("移除 Widget 引用");
                                if (row.Target != null && ((View)target).OwnsChildWidget(row.Target))
                                    Undo.DestroyObjectImmediate(row.Target.gameObject);
                                SaveChildWidgets(kept);
                                Undo.CollapseUndoOperations(undo);
                                widgetMessage = "已移除；将自动更新绑定声明。";
                                GUIUtility.ExitGUI();
                            }
                        }
                    }
                    if (changed)
                        SaveChildWidgets(rows);
                }
                catch (ExitGUIException) { throw; }
                catch (Exception error) { widgetMessage = error.Message; }
            }
            if (!string.IsNullOrEmpty(widgetMessage))
                EditorGUILayout.HelpBox(widgetMessage, MessageType.Info);
        }

        private void RequireWidgetPrefabEditing()
        {
            var view = (View)target;
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(view.gameObject);
            if (stage == null || stage.prefabContentsRoot != view.gameObject)
                throw new InvalidOperationException("请打开父 Prefab 编辑模式并选择其根 View，再配置或生成子 Widget 引用；不在嵌套子资产上写入覆盖。");
        }

        private void SaveChildWidgets(List<ChildWidgetBinding> rows)
        {
            if (rows.Count > 0 || ((View)target).ChildWidgets.Count > 0)
                RequireWidgetPrefabEditing();
            serializedObject.Update();
            var entries = serializedObject.FindProperty("childWidgets");
            entries.arraySize = rows.Count;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i] ?? throw new InvalidOperationException("空 Widget 引用。");
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("Id").stringValue = string.IsNullOrEmpty(row.Id) ? Guid.NewGuid().ToString("N") : row.Id;
                entry.FindPropertyRelative("Name").stringValue = row.Name;
                entry.FindPropertyRelative("Source").stringValue = row.Source;
                entry.FindPropertyRelative("Target").objectReferenceValue = row.Target;
                entry.FindPropertyRelative("CreateVM").boolValue = row.CreateVM;
            }
            serializedObject.ApplyModifiedProperties();
            QueueBindingValidation();
        }

        private static Type WidgetPresenterModel(Type type)
        {
            for (var current = type.BaseType; current != null; current = current.BaseType)
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Presenter<,,>) && current.GetGenericArguments()[2] == typeof(Unit))
                    return current.GetGenericArguments()[0];
            return null;
        }

        private void AppendWidgetMetadata(XElement metadata, BindingManifest manifest)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var targets = new HashSet<View>();
            var sources = new HashSet<string>(metadata.Elements("binding").Select(entry => (string)entry.Attribute("source")), StringComparer.Ordinal);
            foreach (var row in FindChildWidgets())
            {
                if (row == null || !((View)target).OwnsChildWidget(row.Target) || !targets.Add(row.Target) ||
                    !PageSourceTemplate.IsIdentifier(row.Name) || !PageSourceTemplate.IsIdentifier(row.Source) || !names.Add(row.Name))
                    throw new InvalidOperationException("子 Widget 需要有效且唯一的名称、属性名和所属子 View。");
                if (metadata.Elements("binding").Any(entry => (string)entry.Attribute("name") == row.Name))
                    throw new InvalidOperationException("Widget 与原生控件绑定名称冲突：" + row.Name);
                var child = ChildWidgetModel(row.Target);
                if (child == null || child.IsAbstract || child.IsGenericType || !child.IsVisible)
                    throw new InvalidOperationException(row.Name + "：子 View 的 UIWidget 模型关联不可用。");
                var parentAssembly = manifest.ViewModelType.Assembly.GetName().Name;
                var childAssembly = child.Assembly.GetName().Name;
                if (parentAssembly != childAssembly)
                {
                    var compilation = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Player)
                        .FirstOrDefault(assembly => assembly.name == parentAssembly);
                    if (compilation == null || !compilation.allReferences.Any(path => System.IO.Path.GetFileNameWithoutExtension(path) == childAssembly))
                        throw new InvalidOperationException(row.Name + "：父模型程序集 " + parentAssembly + " 需要引用子模型程序集 " + childAssembly + "，请先配置 asmdef 引用。");
                }
                var widget = child.GetCustomAttribute<UIWidgetAttribute>();
                if (widget.PresenterType == null)
                {
                    var candidates = TypeCache.GetTypesDerivedFrom(typeof(Presenter<,,>)).Where(type => !type.IsAbstract && type.IsVisible && WidgetPresenterModel(type) == child).ToArray();
                    if (candidates.Length > 1)
                        throw new InvalidOperationException(row.Name + "：存在多个 Widget Presenter，请在 UIWidget.PresenterType 显式选择：" + string.Join(", ", candidates.Select(type => type.FullName)));
                }
                if (row.CreateVM && child.GetConstructor(Type.EmptyTypes) == null)
                    throw new InvalidOperationException(row.Name + "：自动创建需要公共无参构造函数，或关闭自动创建并由业务赋值。");
                var authored = manifest.Entries.FirstOrDefault(entry => entry.ElementName == row.Name &&
                    entry.ElementType == typeof(WidgetReference) && entry.TargetProperty == nameof(WidgetReference.VM) && !IsAutoBinding(entry));
                if (authored != null)
                    continue;
                if (!sources.Add(row.Source))
                    throw new InvalidOperationException("生成成员名称冲突：" + row.Source);
                var existing = manifest.ViewModelType.GetMember(row.Source, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (existing.Length != 0 && !manifest.Entries.Any(entry => entry.Source == row.Source && IsAutoBinding(entry)))
                    throw new InvalidOperationException(row.Source + " 与手写成员冲突，请显式声明 WidgetReference.VM 绑定。");
                metadata.Add(new XElement("binding", new XAttribute("name", row.Name), new XAttribute("source", row.Source),
                    new XAttribute("contract", typeof(WidgetReference).FullName), new XAttribute("property", nameof(WidgetReference.VM)),
                    new XAttribute("sourceType", child.FullName), new XAttribute("createVM", row.CreateVM ? "true" : "false"),
                    new XAttribute("mode", "OneWay"), new XAttribute("command", "false")));
            }
        }
    }
}
