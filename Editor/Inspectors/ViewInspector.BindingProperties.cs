using MUI.Unity;
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
    internal static class BindingModePopup
    {
        private static readonly GUIContent[] Modes =
        {
            new GUIContent("模型 → 界面", "ViewModel 属性变化时更新界面；界面变化不写回模型。"),
            new GUIContent("双向同步", "模型变化更新界面，界面变化写回模型；需要属性提供变化通知。"),
            new GUIContent("界面 → 模型", "建立绑定时读取界面当前值，之后通过变化通知更新模型；模型不向界面赋值。"),
            new GUIContent("仅绑定时赋值", "每次建立绑定时，从模型向界面赋值一次，之后不跟随模型变化；重新建立绑定会再次赋值。")
        };
        private static readonly GUIContent[] ForwardModes = { Modes[0], Modes[3] };

        internal static BindingMode Draw(BindingMode mode, bool forwardOnly = false)
        {
            return Draw(EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.Width(130)), mode, forwardOnly);
        }

        internal static BindingMode Draw(Rect rect, BindingMode mode, bool forwardOnly = false)
        {
            var index = forwardOnly ? (mode == BindingMode.OneTime ? 1 : 0) : (int)mode;
            var options = forwardOnly ? ForwardModes : Modes;
            var next = EditorGUI.Popup(rect, index, options);
            // 同时为收起状态提供当前模式的完整说明。
            GUI.Label(rect, new GUIContent("", options[Mathf.Clamp(index, 0, options.Length - 1)].tooltip), GUIStyle.none);
            return forwardOnly ? (next == 1 ? BindingMode.OneTime : BindingMode.OneWay) : (BindingMode)next;
        }
    }

    public sealed partial class ViewInspector
    {
        // Presentation state belongs to the editor session, not the serialized View / Undo stack.
        private string BindingFoldoutKey(string control) => "MUI.ViewInspector.BindingFoldout." + target.GetInstanceID() + "." + control;
        private bool IsBindingExpanded(string control) => target != null && SessionState.GetBool(BindingFoldoutKey(control), false);
        private void SetBindingExpanded(string control, bool expanded)
        {
            if (target != null)
                SessionState.SetBool(BindingFoldoutKey(control), expanded);
        }

        private static bool IsComponent(Type component, string fullName)
        {
            for (var type = component; type != null; type = type.BaseType)
                if (type.FullName == fullName) return true;
            return false;
        }

        private static Type ExtensionType(string name) => string.IsNullOrEmpty(name) ? null :
            TypeCache.GetTypesWithAttribute<ControlPropertyExtensionAttribute>().SingleOrDefault(type => type.FullName == name);

        private static Type ExtensionComponent(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(ControlPropertyExtension<>))
                    return current.GetGenericArguments()[0];
            return null;
        }

        private List<AutoBindingProperty> GetBindingChoices(ControlRow row)
        {
            var view = (View)target;
            var saved = view.AutoBindingSelections.FirstOrDefault(item => item.Control == row.Name && item.ComponentType == row.ComponentType?.AssemblyQualifiedName)
                ?? view.AutoBindingSelections.FirstOrDefault(item => item.Control == row.Name && string.IsNullOrEmpty(item.ComponentType) && view.NativeBindings.Count(binding => binding != null && binding.Name == row.Name) <= 1);
            if (saved != null)
                return saved.Properties.Select(item => new AutoBindingProperty { Property = item.Property, Source = item.Source, Mode = item.Mode, Native = item.Native, Extension = item.Extension }).ToList();
            if (row.Target == null)
                return new List<AutoBindingProperty>();
            using (var adapter = NativeControl.Create(row.Name, row.Target))
            {
                if (row.Rule != null && row.Rule.OverrideDefaultProperties)
                    return (row.Rule.DefaultBindingProperties ?? new List<string>()).Distinct().Select(name =>
                    {
                        var native = adapter.GetType().GetProperty(name) == null && adapter.GetType().GetEvent(name) == null;
                        var extensionMembers = BindableMembers(adapter.GetType(), row.Target.GetType()).Where(m => m.Extension != null && m.Name == name).ToArray();
                        if (native && row.Target.GetType().GetProperty(name) == null && extensionMembers.Length > 1)
                            throw new InvalidOperationException("默认扩展属性不唯一，请在页面选择：" + name);
                        var extension = native && row.Target.GetType().GetProperty(name) == null ? extensionMembers.FirstOrDefault() : null;
                        var choice = NewBindingChoice(row, name, !native && adapter.GetType().GetEvent(name) != null || native && IsNativeCommand(row.Target.GetType(), name));
                        if (extension != null)
                        {
                            choice.Extension = extension.Extension;
                            native = false;
                        }
                        choice.Native = native;
                        if (native)
                            choice.Mode = NativeNotificationProperty(row.Target.GetType(), name) == null ? BindingMode.OneWay : BindingMode.TwoWay;
                        if (row.Rule.OverrideBindingDirection)
                            choice.Mode = row.Rule.BindingDirection;
                        return choice;
                    }).ToList();
                string property = null;
                if (row.Target is UnityEngine.UI.Slider || row.Target is UnityEngine.UI.Scrollbar)
                    property = "value";
                else if (row.Target is UnityEngine.UI.Toggle)
                    property = "isOn";
                else if (row.Target is UnityEngine.UI.Text || IsComponent(row.Target.GetType(), "TMPro.TMP_Text"))
                    property = "text";
                else if (row.Target is UnityEngine.UI.Button)
                    property = "onClick";
                else if (row.Target is UnityEngine.UI.Image)
                    property = "sprite";
                else if (row.Target is UnityEngine.UI.RawImage)
                    property = "texture";
                else if (row.Target is UnityEngine.UI.ScrollRect)
                    property = "normalizedPosition";
                else if (row.Target is UnityEngine.UI.InputField || IsComponent(row.Target.GetType(), "TMPro.TMP_InputField"))
                    property = "text";
                else if (row.Target is UnityEngine.UI.Dropdown || IsComponent(row.Target.GetType(), "TMPro.TMP_Dropdown"))
                    property = "value";
                if (string.IsNullOrWhiteSpace(property))
                    return new List<AutoBindingProperty>();
                var command = adapter.GetType().GetEvent(property) != null || IsNativeCommand(row.Target.GetType(), property);
                var choice = NewBindingChoice(row, property, command, true);
                choice.Native = adapter.GetType().GetEvent(property) == null && adapter.GetType().GetProperty(property) == null;
                if (choice.Native && row.Target.GetType().GetProperty(property) == null)
                {
                    var extension = BindableMembers(adapter.GetType(), row.Target.GetType()).Where(m => m.Extension != null && m.Name == property).SingleOrDefault();
                    if (extension != null)
                    {
                        choice.Extension = extension.Extension;
                        choice.Native = false;
                    }
                }
                if (choice.Native)
                    choice.Mode = NativeNotificationProperty(row.Target.GetType(), property) == null ? BindingMode.OneWay : BindingMode.TwoWay;
                if (row.Rule != null && row.Rule.OverrideBindingDirection)
                    choice.Mode = row.Rule.BindingDirection;
                return new List<AutoBindingProperty> { choice };
            }
        }

        private static AutoBindingProperty NewBindingChoice(ControlRow row, string property, bool command, bool primary = false)
        {
            var settings = MUIEditorSettings.instance;
            var format = settings == null
                ? (command ? MUIEditorSettings.DefaultCommandMemberNameFormat : MUIEditorSettings.DefaultValueMemberNameFormat)
                : settings.ResolveMemberNameFormat(row.Target == null ? null : row.Target.GetType(), command);
            var propertyName = string.IsNullOrEmpty(property) ? "" : char.ToUpperInvariant(property[0]) + property.Substring(1);
            var suffix = command ? (property == "onClick" ? "" : propertyName) : primary || property == "Content" ? "" : propertyName;
            return new AutoBindingProperty
            {
                Property = property,
                Source = FormatBindingMemberName(format, row.Name, row.SharedName ? (row.ComponentType ?? row.Target.GetType()).Name : BindingComponentSuffix(row.Target == null ? null : row.Target.GetType()), propertyName, suffix),
                Mode = property == "Value" || property == "NormalizedPosition" ? BindingMode.TwoWay : BindingMode.OneWay
            };
        }

        internal static string FormatBindingMemberName(string format, string name, string component, string property, string defaultSuffix)
        {
            return format.Replace("{Name}", name).Replace("{Component}", component)
                .Replace("{Property}", property).Replace("{DefaultSuffix}", defaultSuffix);
        }

        internal static string BindingComponentSuffix(Type componentType)
        {
            if (componentType == null)
                return "";
            // 与 ElementNaming 的内置缩写保持一致；遍历基类以支持自定义组件和 TMP。
            for (var type = componentType; type != null; type = type.BaseType)
            {
                switch (type.FullName)
                {
                    case "UnityEngine.UI.Button":
                        return "Btn";
                    case "UnityEngine.UI.Text":
                    case "TMPro.TMP_Text":
                        return "Txt";
                    case "UnityEngine.UI.Image":
                    case "UnityEngine.UI.RawImage":
                        return "Img";
                    case "UnityEngine.UI.Slider":
                        return "Sld";
                    case "UnityEngine.UI.Scrollbar":
                        return "Sbr";
                    case "UnityEngine.UI.Toggle":
                        return "Tog";
                    case "UnityEngine.UI.InputField":
                    case "TMPro.TMP_InputField":
                        return "Inp";
                    case "UnityEngine.UI.Dropdown":
                    case "TMPro.TMP_Dropdown":
                        return "Ddp";
                    case "UnityEngine.UI.ScrollRect":
                        return "Scr";
                    case "UnityEngine.UI.Mask":
                    case "UnityEngine.UI.RectMask2D":
                        return "Msk";
                    case "UnityEngine.UI.LayoutGroup":
                    case "UnityEngine.UI.LayoutElement":
                    case "UnityEngine.UI.ContentSizeFitter":
                    case "UnityEngine.UI.AspectRatioFitter":
                        return "Lyt";
                }
            }
            return componentType.Name;
        }

        private void SaveBindingChoices(ControlRow row, List<AutoBindingProperty> choices, bool separateUndo = true)
        {
            var control = row.Name;
            var componentType = row.ComponentType?.AssemblyQualifiedName ?? "";
            serializedObject.Update();
            var selections = serializedObject.FindProperty("autoBindingSelections");
            var index = -1;
            for (var i = 0; i < selections.arraySize; i++)
                if (selections.GetArrayElementAtIndex(i).FindPropertyRelative("Control").stringValue == control && selections.GetArrayElementAtIndex(i).FindPropertyRelative("ComponentType").stringValue == componentType)
                    index = i;
            if (index < 0 && ((View)target).NativeBindings.Count(binding => binding != null && binding.Name == control) <= 1)
                for (var i = 0; i < selections.arraySize; i++)
                    if (selections.GetArrayElementAtIndex(i).FindPropertyRelative("Control").stringValue == control &&
                        string.IsNullOrEmpty(selections.GetArrayElementAtIndex(i).FindPropertyRelative("ComponentType").stringValue))
                        index = i;
            if (index < 0)
                index = selections.arraySize++;
            var selection = selections.GetArrayElementAtIndex(index);
            selection.FindPropertyRelative("Control").stringValue = control;
            selection.FindPropertyRelative("ComponentType").stringValue = componentType;
            var properties = selection.FindPropertyRelative("Properties");
            properties.arraySize = choices.Count;
            for (var i = 0; i < choices.Count; i++)
            {
                var property = properties.GetArrayElementAtIndex(i);
                property.FindPropertyRelative("Property").stringValue = choices[i].Property;
                property.FindPropertyRelative("Native").boolValue = choices[i].Native;
                property.FindPropertyRelative("Extension").stringValue = choices[i].Extension ?? "";
                property.FindPropertyRelative("Source").stringValue = choices[i].Source;
                property.FindPropertyRelative("Mode").intValue = (int)choices[i].Mode;
            }
            // Keep edits separate from preceding object creation / selection operations.
            if (separateUndo)
                Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("修改 " + control + " 的绑定属性");
            serializedObject.ApplyModifiedProperties();
            if (separateUndo)
                Undo.IncrementCurrentGroup();
            nativeBindingMessage = "绑定选择已修改；将自动同步并编译。";
            QueueBindingValidation();
            Repaint();
        }

        private static NativePropertyNotification NativeNotification(Type component, string property)
        {
            var settings = MUIEditorSettings.instance;
            var mapping = settings == null ? MUIEditorSettings.BuiltInPropertyNotification(component, property) : settings.ResolvePropertyNotification(component, property);
            if (mapping == null || string.IsNullOrEmpty(mapping.Event))
                return null;
            var member = component.GetProperty(mapping.Event);
            var field = component.GetField(mapping.Event);
            var signal = member != null && member.GetMethod != null && member.GetMethod.IsPublic && !member.GetMethod.IsStatic && member.GetIndexParameters().Length == 0
                ? member.PropertyType : field != null && !field.IsStatic ? field.FieldType : null;
            var supported = signal == typeof(UnityEngine.Events.UnityEvent);
            for (var current = signal; current != null; current = current.BaseType)
                if (current == typeof(UnityEngine.Events.UnityEvent) || current.IsGenericType && current.GetGenericTypeDefinition() == typeof(UnityEngine.Events.UnityEvent<>))
                    supported = true;
            if (!supported)
                throw new InvalidOperationException(component.Name + "." + property + "：变化事件必须是公开的零或单参数 UnityEvent：" + mapping.Event);
            if (!string.IsNullOrEmpty(mapping.Setter))
            {
                var target = component.GetProperty(property);
                var setter = target == null ? null : component.GetMethod(mapping.Setter, new[] { target.PropertyType });
                if (setter == null || setter.IsStatic || setter.ReturnType != typeof(void))
                    throw new InvalidOperationException(component.Name + "." + property + "：静默赋值方法无效：" + mapping.Setter);
            }
            return mapping;
        }

        private static string NativeNotificationProperty(Type component, string property) => NativeNotification(component, property)?.Event;

        private static bool IsNativeCommand(Type component, string member)
        {
            var type = component.GetProperty(member)?.PropertyType ?? component.GetField(member)?.FieldType;
            return IsUnityEvent(type);
        }

        private static bool IsUnityEvent(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
                if (current.Namespace == "UnityEngine.Events" &&
                    (current == typeof(UnityEngine.Events.UnityEvent) || current.IsGenericType &&
                    new[] { typeof(UnityEngine.Events.UnityEvent<>), typeof(UnityEngine.Events.UnityEvent<,>),
                        typeof(UnityEngine.Events.UnityEvent<,,>), typeof(UnityEngine.Events.UnityEvent<,,,>) }.Contains(current.GetGenericTypeDefinition())))
                    return true;
            return false;
        }

        internal sealed class BindingMember
        {
            public MemberInfo Member;
            public bool Native;
            public string Extension;
            public bool Notifies => Member.IsDefined(typeof(NotifiesChangeAttribute), true);
            public string Reason;
            public bool Command => Member is EventInfo || Native &&
                (Member is PropertyInfo signal && IsUnityEvent(signal.PropertyType) ||
                 Member is FieldInfo field && IsUnityEvent(field.FieldType));
            public bool ReadOnly => Member is PropertyInfo property && (property.SetMethod == null || !property.SetMethod.IsPublic);
            public string Name => Member.Name;
        }

        internal static List<BindingMember> BindableMembers(Type adapterType, Type componentType)
        {
            var result = new List<BindingMember>();
            var extensions = TypeCache.GetTypesWithAttribute<ControlPropertyExtensionAttribute>()
                .Where(type => type.IsVisible && !type.IsNested && !type.IsAbstract && !type.ContainsGenericParameters &&
                    ExtensionComponent(type) != null && ExtensionComponent(type).IsAssignableFrom(componentType) &&
                    type.GetConstructor(new[] { typeof(NativeControl) }) != null).ToArray();
            foreach (var type in new[] { adapterType, componentType }.Concat(extensions))
                foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (type == adapterType && (new[] { "Name", "Target", "IsAlive", "ComponentType" }.Contains(property.Name)))
                        continue;
                    var reason = property.GetIndexParameters().Length != 0 ? "索引器需要参数" :
                        property.IsDefined(typeof(ObsoleteAttribute), true) ? "已弃用" :
                        (property.GetMethod == null || !property.GetMethod.IsPublic) && (property.SetMethod == null || !property.SetMethod.IsPublic) ? "没有公开访问器" :
                        property.PropertyType.IsByRef || property.PropertyType.IsPointer || property.PropertyType.ContainsGenericParameters || !property.PropertyType.IsVisible ? "类型不能用于生成属性" : null;
                    result.Add(new BindingMember { Member = property, Native = type == componentType, Extension = extensions.Contains(type) ? type.FullName : null, Reason = reason });
                }
            result.AddRange(componentType.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(field => IsUnityEvent(field.FieldType))
                .Select(field => new BindingMember { Member = field, Native = true,
                    Reason = field.IsDefined(typeof(ObsoleteAttribute), true) ? "已弃用" : null }));
            result.AddRange(adapterType.GetEvents(BindingFlags.Instance | BindingFlags.Public)
                .Where(item => item.EventHandlerType == typeof(Action) && adapterType.GetProperty("Interactable") != null)
                .Select(item => new BindingMember { Member = item }));
            return result.OrderBy(item => item.Native).ThenBy(item => item.Name, StringComparer.Ordinal).ToList();
        }

        private void DrawBindingProperties(ControlRow row)
        {
            if (row.IsElementBinding || row.Target == null || !IsBindingExpanded(row.Key))
                return;
            try
            {
                var choices = GetBindingChoices(row);
                var authored = selected < 0 ? new List<BindingEntry>() : manifests[selected].Entries.Where(entry => MatchesEntry(row, entry) && !IsAutoBinding(entry)).ToList();
                using (var adapter = NativeControl.Create(row.Name, row.Target))
                {
                    var members = BindableMembers(adapter.GetType(), row.Target.GetType());
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label(new GUIContent("控件属性 → ViewModel 属性名", "右侧输入框填写生成的代码成员名，例如 text → Title，代码使用 ViewModel.Title。修改后自动同步并编译。"), EditorStyles.miniLabel);
                            if (GUILayout.Button(new GUIContent("添加属性", "搜索原生组件的全部公开实例属性，以及扩展属性和控件事件；只读属性生成实时读取入口。"), EditorStyles.miniButton, GUILayout.Width(80)))
                            {
                                var available = members.Where(member => !choices.Any(c => c.Property == member.Name && c.Native == member.Native && (c.Extension ?? "") == (member.Extension ?? "")) && !authored.Any(entry => entry.TargetProperty == member.Name && (entry.ComponentType != null) == member.Native)).ToList();
                                PopupWindow.Show(GUILayoutUtility.GetLastRect(), new BindingPropertyPicker(available, member =>
                                {
                                    if (this == null || target == null)
                                        return;
                                    var current = GetBindingChoices(row);
                                    if (current.Any(c => c.Property == member.Name && c.Native == member.Native && (c.Extension ?? "") == (member.Extension ?? "")))
                                        return;
                                    var choice = NewBindingChoice(row, member.Name, member.Command);
                                    choice.Native = member.Native;
                                    choice.Extension = member.Extension;
                                    if (member.Native)
                                        choice.Mode = NativeNotificationProperty(row.Target.GetType(), member.Name) == null ? BindingMode.OneWay : BindingMode.TwoWay;
                                    current.Add(choice);
                                    SaveBindingChoices(row, current);
                                }));
                            }
                        }
                        DrawBindingTableHeader();
                        var tableRow = 0;
                        foreach (var entry in authored)
                        {
                            var cells = BindingTableRow(tableRow++);
                            GUI.Label(cells[0], new GUIContent(entry.TargetProperty, entry.TargetProperty));
                            GUI.Label(cells[1], new GUIContent(entry.Source, "ViewModel." + entry.Source));
                            if (GUI.Button(cells[2], new GUIContent("代码声明", "手写绑定优先；点击定位源码。"), EditorStyles.miniButton))
                                OpenBindingSource(entry);
                        }
                        for (var i = 0; i < choices.Count; i++)
                        {
                            var choice = choices[i];
                            if (authored.Any(entry => CanonicalBindingProperty(entry.TargetProperty) == CanonicalBindingProperty(choice.Property)))
                                continue;
                            var member = members.FirstOrDefault(item => item.Name == choice.Property && item.Native == choice.Native && (item.Extension ?? "") == (choice.Extension ?? ""));
                            {
                                var cells = BindingTableRow(tableRow++);
                                GUI.Label(cells[0], new GUIContent(choice.Property, choice.Property + "\n" + (member != null && member.Member is PropertyInfo property ? property.PropertyType.FullName : "命令事件")));
                                EditorGUI.BeginChangeCheck();
                                choice.Source = EditorGUI.TextField(cells[1], new GUIContent("", "生成的 ViewModel 属性名，初始名称由全局命名模板决定，例如 ProgressSld。修改即时保存，支持撤销；自动同步并编译后生效。"), choice.Source);
                                if (member != null && member.Command)
                                    GUI.Label(cells[2], "命令", EditorStyles.miniLabel);
                                else if (member != null && member.ReadOnly)
                                    GUI.Label(cells[2], new GUIContent("实时读取", "绑定期间读取当前值；引用对象可访问其内部属性。不是可写绑定，也不自动通知变化。"), EditorStyles.miniLabel);
                                else if ((choice.Native && NativeNotificationProperty(row.Target.GetType(), choice.Property) == null) || (!string.IsNullOrEmpty(choice.Extension) && (member == null || !member.Notifies)))
                                {
                                    choice.Mode = BindingModePopup.Draw(cells[2], choice.Mode, forwardOnly: true);
                                }
                                else
                                    choice.Mode = BindingModePopup.Draw(cells[2], choice.Mode);
                                if (EditorGUI.EndChangeCheck())
                                    SaveBindingChoices(row, choices);
                                if (GUI.Button(cells[3], new GUIContent("×", "移除此属性选择，可撤销；重新生成绑定后才会移除生成成员，不删除控件或手写源码。"), EditorStyles.miniButton))
                                {
                                    choices.RemoveAt(i);
                                    SaveBindingChoices(row, choices);
                                    break;
                                }
                            }
                            if (member == null)
                                EditorGUILayout.HelpBox("适配器不再支持 " + choice.Property + "，请移除此绑定或调整适配器。", MessageType.Error);
                            else if (member.Reason != null)
                                EditorGUILayout.HelpBox(choice.Property + "：" + member.Reason, MessageType.Error);
                            else if (choices.Count(item => item.Source == choice.Source) > 1 || ((View)target).AutoBindingSelections.Any(selection => (selection.Control != row.Name || selection.ComponentType != row.ComponentType?.AssemblyQualifiedName) && selection.Properties.Any(item => item.Source == choice.Source)))
                                EditorGUILayout.HelpBox("生成名称重复：" + choice.Source, MessageType.Error);
                            else if (!PageSourceTemplate.IsIdentifier(choice.Source))
                                EditorGUILayout.HelpBox("ViewModel 成员名称不是有效的 C# 标识符。", MessageType.Error);
                        }
                        if (choices.Count == 0 && authored.Count == 0)
                            EditorGUILayout.LabelField("未选择绑定属性，仅保留控件引用。", EditorStyles.miniLabel);
                    }
                }
            }
            catch (Exception error) { EditorGUILayout.HelpBox(error.Message, MessageType.Error); }
        }

        private float bindingPropertySplit = 0.25f;
        private float bindingModeSplit = 0.72f;

        private void RestoreBindingColumnWidths()
        {
            // Editor is a ScriptableObject: Unity APIs cannot run in field initializers.
            bindingPropertySplit = Mathf.Clamp(SessionState.GetFloat("MUI.BindingTable.PropertySplit", 0.25f), 0.12f, 0.68f);
            bindingModeSplit = Mathf.Clamp(SessionState.GetFloat("MUI.BindingTable.ModeSplit", 0.72f), bindingPropertySplit + 0.18f, 0.86f);
        }

        private Rect[] BindingTableCells(Rect row)
        {
            var width = Mathf.Max(1, row.width - 24);
            var first = width * bindingPropertySplit;
            var second = width * bindingModeSplit;
            return new[]
            {
                new Rect(row.x, row.y, Mathf.Max(1, first - 4), row.height),
                new Rect(row.x + first, row.y, Mathf.Max(1, second - first - 4), row.height),
                new Rect(row.x + second, row.y, Mathf.Max(1, width - second - 4), row.height),
                new Rect(row.xMax - 20, row.y, 20, row.height)
            };
        }

        private Rect[] BindingTableRow(int index)
        {
            var row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 2);
            if (Event.current.type == EventType.Repaint && index % 2 == 0)
                EditorGUI.DrawRect(row, new Color(0.5f, 0.5f, 0.5f, 0.07f));
            return BindingTableCells(row);
        }

        private void DrawBindingTableHeader()
        {
            var row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 3);
            EditorGUI.DrawRect(row, new Color(0.5f, 0.5f, 0.5f, 0.14f));
            var cells = BindingTableCells(row);
            GUI.Label(cells[0], new GUIContent("控件属性", "拖拽表头分隔线调整列宽，双击恢复默认。"), EditorStyles.miniBoldLabel);
            GUI.Label(cells[1], new GUIContent("ViewModel 属性名", "生成的代码成员名称；修改后重新生成绑定。"), EditorStyles.miniBoldLabel);
            GUI.Label(cells[2], "绑定方式", EditorStyles.miniBoldLabel);
            for (var i = 0; i < 2; i++)
            {
                var handle = new Rect(cells[i + 1].x - 4, row.y, 7, row.height);
                var id = GUIUtility.GetControlID(FocusType.Passive);
                EditorGUIUtility.AddCursorRect(handle, MouseCursor.ResizeHorizontal);
                EditorGUI.DrawRect(new Rect(handle.x + 3, row.y + 2, 1, row.height - 4), new Color(0.5f, 0.5f, 0.5f, 0.6f));
                var evt = Event.current;
                if (evt.type == EventType.MouseDown && evt.button == 0 && handle.Contains(evt.mousePosition))
                {
                    if (evt.clickCount == 2)
                    {
                        bindingPropertySplit = 0.25f;
                        bindingModeSplit = 0.72f;
                        SaveBindingColumnWidths();
                    }
                    else
                        GUIUtility.hotControl = id;
                    evt.Use();
                }
                if (GUIUtility.hotControl != id)
                    continue;
                if (evt.type == EventType.MouseDrag)
                {
                    var ratio = (evt.mousePosition.x - row.x) / Mathf.Max(1, row.width - 24);
                    if (i == 0)
                        bindingPropertySplit = Mathf.Clamp(ratio, 0.12f, bindingModeSplit - 0.18f);
                    else
                        bindingModeSplit = Mathf.Clamp(ratio, bindingPropertySplit + 0.18f, 0.86f);
                    SaveBindingColumnWidths();
                    evt.Use();
                }
                else if (evt.type == EventType.MouseUp)
                {
                    GUIUtility.hotControl = 0;
                    evt.Use();
                }
            }
        }

        private void SaveBindingColumnWidths()
        {
            SessionState.SetFloat("MUI.BindingTable.PropertySplit", bindingPropertySplit);
            SessionState.SetFloat("MUI.BindingTable.ModeSplit", bindingModeSplit);
            Repaint();
        }

        private sealed class BindingPropertyPicker : PopupWindowContent
        {
            private readonly List<BindingMember> members;
            private readonly Action<BindingMember> choose;
            private string search = "";
            private Vector2 scroll;
            private bool focusSearch = true;
            private bool showUnavailable;
            public BindingPropertyPicker(List<BindingMember> members, Action<BindingMember> choose)
            {
                this.members = members;
                this.choose = choose;
            }
            public override Vector2 GetWindowSize() => new Vector2(470, 340);
            public override void OnGUI(Rect rect)
            {
                GUI.SetNextControlName("MUIBindingPropertySearch");
                EditorGUI.BeginChangeCheck();
                search = EditorGUILayout.TextField(new GUIContent("", "搜索属性名、类型或声明类型，多个关键词用空格分隔。"), search, EditorStyles.toolbarSearchField);
                if (EditorGUI.EndChangeCheck())
                    scroll = Vector2.zero;
                if (focusSearch)
                {
                    EditorGUI.FocusTextInControl("MUIBindingPropertySearch");
                    focusSearch = false;
                }
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                {
                    editorWindow.Close();
                    GUIUtility.ExitGUI();
                }
                showUnavailable = EditorGUILayout.ToggleLeft("显示不可用 / 已弃用属性", showUnavailable);
                var words = search.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var matches = members.Where(item => (showUnavailable || item.Reason == null) && words.All(word => (item.Name + " " + (item.Member is PropertyInfo info ? info.PropertyType.FullName : "事件") + " " + item.Member.DeclaringType.FullName + (item.Native ? " 原生" : item.Extension != null ? " 扩展 " + item.Extension : " 事件与行为")).IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                EditorGUILayout.LabelField("匹配 " + matches.Count + " / " + members.Count, EditorStyles.miniLabel);
                using (var scope = new EditorGUILayout.ScrollViewScope(scroll))
                {
                    scroll = scope.scrollPosition;
                    foreach (var member in matches)
                    {
                        var type = member.Member is PropertyInfo property ? property.PropertyType.Name : "事件";
                        using (new EditorGUI.DisabledScope(member.Reason != null))
                            if (GUILayout.Button(new GUIContent(member.Name + "  ·  " + type + (member.Native ? "  原生" : member.Extension != null ? "  扩展 " + member.Member.DeclaringType.Name : "  事件与行为") + (member.ReadOnly ? "  只读访问" : "") + (member.Reason == null ? "" : "  (" + member.Reason + ")"),
                                member.Reason ?? (member.ReadOnly ? "生成实时读取入口，绑定期间可读取；引用对象可修改内部属性，但不能替换引用。" : member.Native ? "生成器按需生成属性包装；没有变化事件的属性只支持单向更新。" : "适配器绑定属性")), EditorStyles.miniButton))
                            {
                                choose(member);
                                editorWindow.Close();
                                GUIUtility.ExitGUI();
                            }
                    }
                    if (matches.Count == 0)
                        EditorGUILayout.LabelField(members.Count == 0 ? "没有更多可绑定属性。" : "没有匹配的属性，请更换关键词。", EditorStyles.miniLabel);
                }
            }
        }
    }
}
