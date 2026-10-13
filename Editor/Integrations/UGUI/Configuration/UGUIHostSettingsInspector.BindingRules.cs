using MUI.Unity;
using System;
using System.Linq;
using System.Collections.Generic;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public sealed partial class UGUIHostSettingsInspector
    {
        private string RuleFoldoutKey(int index) => "MUI.ControlRules." + target.GetInstanceID() + "." + index;

        private static Rect[] RuleCells(Rect row)
        {
            var modeWidth = Mathf.Min(82, row.width * 0.22f);
            var remaining = Mathf.Max(2, row.width - modeWidth - 48);
            var expressionWidth = remaining * 0.48f;
            return new[] {
                new Rect(row.x, row.y, 16, row.height),
                new Rect(row.x + 18, row.y, modeWidth, row.height),
                new Rect(row.x + 22 + modeWidth, row.y, expressionWidth, row.height),
                new Rect(row.x + 26 + modeWidth + expressionWidth, row.y, remaining - expressionWidth, row.height),
                new Rect(row.xMax - 20, row.y, 20, row.height)
            };
        }

        private void DrawControlNamingRules()
        {
            if (this == null || target == null)
                return;
            authoringSettings.UpdateIfRequiredOrScript();
            DrawMemberNameFormats();
            EditorGUILayout.PropertyField(authoringSettings.FindProperty("nativePropertyNotifications"), new GUIContent("原生属性变化事件映射"), true);
            EditorGUILayout.HelpBox("内置值属性已映射 onValueChanged。自定义映射填写组件类型、属性、UnityEvent 和可选静默赋值方法；优先于内置映射。映射后可选择双向绑定，重新生成绑定后生效。事件必须在该属性变化时触发。", MessageType.Info);
            var rules = authoringSettings.FindProperty("controlNamingRules");
            var header = RuleCells(EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight));
            GUI.Label(header[1], new GUIContent("匹配方式", L.Get("ruleMatch.help")), EditorStyles.miniBoldLabel);
            GUI.Label(header[2], new GUIContent("表达式", "匹配节点名称，例如 Img_、_Icon；正则可用 (?<name>...) 提取绑定名。"), EditorStyles.miniBoldLabel);
            GUI.Label(header[3], new GUIContent("组件类型", "点击搜索并多选；展开行可配置默认属性和绑定方向。"), EditorStyles.miniBoldLabel);
            var modes = new[] { L.Get("ruleMatch.prefix"), L.Get("ruleMatch.suffix"), L.Get("ruleMatch.contains"), L.Get("ruleMatch.regex") };
            for (var i = 0; i < rules.arraySize; i++)
            {
                var rule = rules.GetArrayElementAtIndex(i);
                var prefix = rule.FindPropertyRelative("Expression");
                var types = rule.FindPropertyRelative("ComponentTypes");
                var row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 2);
                if (Event.current.type == EventType.Repaint && i % 2 == 0)
                    EditorGUI.DrawRect(row, new Color(0.5f, 0.5f, 0.5f, 0.07f));
                var cells = RuleCells(row);
                var key = RuleFoldoutKey(i);
                var expanded = SessionState.GetBool(key, false);
                var nextExpanded = EditorGUI.Foldout(cells[0], expanded, new GUIContent("", "展开默认属性和绑定方向设置"));
                if (nextExpanded != expanded)
                    SessionState.SetBool(key, nextExpanded);
                var mode = rule.FindPropertyRelative("Match");
                mode.enumValueIndex = EditorGUI.Popup(cells[1], mode.enumValueIndex, modes);
                GUI.Label(cells[1], new GUIContent("", L.Get("ruleMatch.help")), GUIStyle.none);
                prefix.stringValue = EditorGUI.TextField(cells[2], new GUIContent("", "匹配表达式；前缀如 Img_，后缀如 _Icon。不是生成的属性名。"), prefix.stringValue);
                var summary = string.Join(" / ", Enumerable.Range(0, types.arraySize).Select(t =>
                {
                    var value = types.GetArrayElementAtIndex(t).stringValue;
                    var type = Type.GetType(value, false);
                    return type == null ? value.Split(',')[0] : type.Name;
                }));
                if (GUI.Button(cells[3], new GUIContent(string.IsNullOrEmpty(summary) ? "选择组件…" : summary,
                    "搜索并多选匹配组件。同一节点匹配多个组件时分别生成绑定。\n" + summary), EditorStyles.popup))
                {
                    if (authoringSettings.ApplyModifiedProperties())
                        MUIEditorSettings.instance.SaveSettings();
                    PopupWindow.Show(cells[3], new ControlTypePicker(target, types.propertyPath, prefix.stringValue));
                }
                if (GUI.Button(cells[4], new GUIContent("×", "删除此规则，可撤销；不删除节点、组件或已有页面绑定。"), EditorStyles.miniButton))
                {
                    for (var j = i; j < rules.arraySize - 1; j++)
                        SessionState.SetBool(RuleFoldoutKey(j), SessionState.GetBool(RuleFoldoutKey(j + 1), false));
                    SessionState.EraseBool(RuleFoldoutKey(rules.arraySize - 1));
                    rules.DeleteArrayElementAtIndex(i);
                    break;
                }
                if (nextExpanded)
                    DrawRuleDefaults(rule);
                if (string.IsNullOrWhiteSpace(prefix.stringValue) || types.arraySize == 0)
                    EditorGUILayout.LabelField("请填写表达式并选择至少一种组件。", EditorStyles.miniLabel);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("＋ 添加规则", "添加一条名称匹配规则；填写表达式并选择组件后生效。支持撤销。"), EditorStyles.miniButton, GUILayout.Width(100)))
                {
                    var index = rules.arraySize;
                    var rule = rules.GetArrayElementAtIndex(rules.arraySize++);
                    rule.FindPropertyRelative("OverrideDefaultProperties").boolValue = false;
                    rule.FindPropertyRelative("DefaultBindingProperties").arraySize = 0;
                    rule.FindPropertyRelative("OverrideBindingDirection").boolValue = false;
                    rule.FindPropertyRelative("BindingDirection").enumValueIndex = 0;
                    rule.FindPropertyRelative("Expression").stringValue = "";
                    rule.FindPropertyRelative("Match").enumValueIndex = 0;
                    rule.FindPropertyRelative("ComponentTypes").arraySize = 0;
                    SessionState.SetBool(RuleFoldoutKey(index), true);
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label(rules.arraySize + " 条规则", EditorStyles.miniLabel);
            }
            if (authoringSettings.ApplyModifiedProperties())
                MUIEditorSettings.instance.SaveSettings();
        }

        private void DrawMemberNameFormats()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("全局 ViewModel 成员命名", EditorStyles.boldLabel);
                DrawMemberNameFormat("valueMemberNameFormat", "值属性格式", MUIEditorSettings.DefaultValueMemberNameFormat, false);
                DrawMemberNameFormat("commandMemberNameFormat", "命令格式", MUIEditorSettings.DefaultCommandMemberNameFormat, true);
                EditorGUILayout.HelpBox("占位符（可复制到格式中）：\n{Name}：规则提取的控件名，如 Sld_Progress → Progress。\n{Component}：组件简写，如 Slider → Sld、Button → Btn。\n{Property}：当前属性或事件名，首字母大写，如 value → Value、Clicked → Clicked。\n{DefaultSuffix}：默认主属性、Content 和 Clicked 为空，其余为属性名。\n例如 {Name}Cmd → CloseCmd；{Name}{Component}{Property} → ProgressSldValue。\n可调整顺序或添加固定文字。全局格式留空恢复默认；展开下方规则可按组件覆盖。仅影响新建名称，已保存名称不会自动改写。", MessageType.Info);
            }
        }

        private void DrawMemberNameFormat(string field, string label, string fallback, bool command, string component = null)
        {
            var property = authoringSettings.FindProperty(field);
            EditorGUILayout.PropertyField(property, new GUIContent(label));
            var format = string.IsNullOrWhiteSpace(property.stringValue) ? fallback : property.stringValue;
            var primary = ViewInspector.FormatBindingMemberName(format, command ? "Close" : "Progress", component ?? (command ? "Btn" : "Sld"), command ? "Clicked" : "Value", "");
            var additional = ViewInspector.FormatBindingMemberName(format, command ? "Close" : "Progress", component ?? (command ? "Btn" : "Sld"), command ? "Pressed" : "MaxValue", command ? "Pressed" : "MaxValue");
            EditorGUILayout.LabelField("预览", primary + " / " + additional);
            if (!PageSourceTemplate.IsIdentifier(primary) || !PageSourceTemplate.IsIdentifier(additional))
                EditorGUILayout.HelpBox("格式包含未知占位符，或生成的名称不是有效的 C# 标识符。", MessageType.Error);
        }

        private void DrawComponentMemberNames(SerializedProperty rule)
        {
            var types = rule.FindPropertyRelative("ComponentTypes");
            var entries = authoringSettings.FindProperty("componentMemberNaming");
            for (var i = 0; i < types.arraySize; i++)
            {
                var typeName = types.GetArrayElementAtIndex(i).stringValue;
                if (string.IsNullOrEmpty(typeName))
                    continue;
                var type = Type.GetType(typeName, false);
                var index = -1;
                for (var j = 0; j < entries.arraySize; j++)
                {
                    var saved = entries.GetArrayElementAtIndex(j).FindPropertyRelative("ComponentType").stringValue;
                    if (saved == typeName || (type != null && !string.IsNullOrEmpty(saved) && Type.GetType(saved, false) == type))
                    {
                        index = j;
                        break;
                    }
                }
                var custom = EditorGUILayout.ToggleLeft(new GUIContent((type == null ? typeName.Split(',')[0] : type.Name) + "：自定义成员命名",
                    "同一组件类型在所有规则中共享此配置。未设置时继承基类配置或全局格式。"), index >= 0);
                if (!custom)
                {
                    if (index >= 0)
                        entries.DeleteArrayElementAtIndex(index);
                    continue;
                }
                if (index < 0)
                {
                    index = entries.arraySize++;
                    var added = entries.GetArrayElementAtIndex(index);
                    added.FindPropertyRelative("ComponentType").stringValue = typeName;
                    added.FindPropertyRelative("ValueFormat").stringValue = "";
                    added.FindPropertyRelative("CommandFormat").stringValue = "";
                }
                var entry = entries.GetArrayElementAtIndex(index);
                var settings = MUIEditorSettings.instance;
                DrawMemberNameFormat(entry.FindPropertyRelative("ValueFormat").propertyPath, "值属性格式",
                    settings.ResolveMemberNameFormat(type == null ? null : type.BaseType, false), false, ViewInspector.BindingComponentSuffix(type));
                DrawMemberNameFormat(entry.FindPropertyRelative("CommandFormat").propertyPath, "命令格式",
                    settings.ResolveMemberNameFormat(type == null ? null : type.BaseType, true), true, ViewInspector.BindingComponentSuffix(type));
                EditorGUILayout.LabelField("留空继承基类或全局格式；预览使用 Progress / Close 示例。", EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawRuleDefaults(SerializedProperty rule)
        {
            var previousWidth = EditorGUIUtility.labelWidth;
            try
            {
                EditorGUIUtility.labelWidth = 80;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawComponentMemberNames(rule);
                    var custom = rule.FindPropertyRelative("OverrideDefaultProperties");
                    var properties = rule.FindPropertyRelative("DefaultBindingProperties");
                    var useCustom = custom.boolValue;
                    var next = EditorGUILayout.Popup(new GUIContent("默认属性", "只决定尚未保存属性选择的控件如何预选，不覆盖页面已有配置。"), useCustom ? 1 : 0,
                        new[] { new GUIContent("自动选择", "文本 text、图片 sprite、按钮 Clicked；输入控件选择对应值属性。"), new GUIContent("自定义列表", "指定一个或多个属性或事件名；空列表表示不预选。") });
                    if ((next == 1) != useCustom)
                    {
                        custom.boolValue = next == 1;
                        useCustom = next == 1;
                    }
                    if (useCustom)
                    {
                        var selectedNames = Enumerable.Range(0, properties.arraySize).Select(index => properties.GetArrayElementAtIndex(index).stringValue).ToArray();
                        var rect = EditorGUILayout.GetControlRect();
                        rect = EditorGUI.PrefixLabel(rect, new GUIContent("预选属性", "只影响尚未保存选择的控件；空列表表示不预选。"));
                        if (GUI.Button(rect, new GUIContent(selectedNames.Length == 0 ? "选择属性…" : string.Join(", ", selectedNames),
                            "搜索并连续勾选属性或事件；只读属性生成实时读取入口。\n" + string.Join(", ", selectedNames)), EditorStyles.popup))
                        {
                            rule.serializedObject.ApplyModifiedProperties();
                            PopupWindow.Show(rect, new RulePropertyPicker(rule.serializedObject.targetObject, rule.propertyPath));
                        }
                    }
                    var overrideDirection = rule.FindPropertyRelative("OverrideBindingDirection");
                    var direction = rule.FindPropertyRelative("BindingDirection");
                    var selectedDirection = overrideDirection.boolValue ? direction.intValue + 1 : 0;
                    var directionOptions = new[] {
                        new GUIContent("自动（按属性能力）", "有通知的输入属性双向，其余属性默认单向；只读属性实时读取。"),
                        new GUIContent("模型 → 界面", "模型变化时更新控件。"), new GUIContent("双向同步", "需属性提供变化通知，否则页面校验会报错。"),
                        new GUIContent("界面 → 模型", "读取控件值并监听变化；需要变化通知。"), new GUIContent("仅绑定时赋值", "每次建立绑定时从模型向控件赋值一次。") };
                    EditorGUI.BeginChangeCheck();
                    selectedDirection = EditorGUILayout.Popup(new GUIContent("默认方向", "仅影响新属性选择；命令事件和只读访问不使用此方向。"), selectedDirection, directionOptions);
                    if (EditorGUI.EndChangeCheck())
                    {
                        overrideDirection.boolValue = selectedDirection != 0;
                        if (selectedDirection != 0)
                            direction.intValue = selectedDirection - 1;
                    }
                }
            }
            finally { EditorGUIUtility.labelWidth = previousWidth; }
        }
        private sealed class RulePropertyPicker : PopupWindowContent
        {
            private readonly UnityEngine.Object owner;
            private readonly string path;
            private readonly string pattern;
            private readonly Dictionary<string, string> available = new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly Dictionary<string, string> unavailable = new Dictionary<string, string>(StringComparer.Ordinal);
            private string search = "";
            private bool selectedOnly;
            private bool focusSearch = true;
            private Vector2 scroll;

            internal RulePropertyPicker(UnityEngine.Object owner, string path)
            {
                this.owner = owner;
                this.path = path;
                using (var state = new SerializedObject(owner))
                {
                    var rule = state.FindProperty(path);
                    pattern = rule.FindPropertyRelative("Expression").stringValue;
                    var types = rule.FindPropertyRelative("ComponentTypes");
                    var componentTypes = Enumerable.Range(0, types.arraySize).Select(i => Type.GetType(types.GetArrayElementAtIndex(i).stringValue, false)).ToArray();
                    var catalogs = new List<List<ViewInspector.BindingMember>>();
                    foreach (var type in componentTypes)
                    {
                        if (type == null)
                        {
                            catalogs.Add(new List<ViewInspector.BindingMember>());
                            continue;
                        }
                        // Inspect type metadata only; never create a GameObject or run adapter constructors here.
                        var adapter = TypeCache.GetTypesDerivedFrom<NativeControl>().Where(t => !t.IsAbstract && !t.ContainsGenericParameters &&
                            (t.Namespace == "MUI.UGUI" || t.Namespace == "MUI.TMP"))
                            .Select(t => new
                            {
                                Type = t,
                                Constructor = t.GetConstructors().FirstOrDefault(c =>
                                {
                                    var args = c.GetParameters();
                                    return args.Length == 2 && args[0].ParameterType == typeof(string) && args[1].ParameterType.IsAssignableFrom(type);
                                })
                            }).Where(t => t.Constructor != null)
                            .OrderBy(t => t.Constructor.GetParameters()[1].ParameterType == type ? 0 : t.Type == typeof(NativeComponent) ? 2 : 1)
                            .Select(t => t.Type).FirstOrDefault() ?? typeof(NativeComponent);
                        catalogs.Add(ViewInspector.BindableMembers(adapter, type).Where(m => m.Reason == null).ToList());
                    }
                    foreach (var name in catalogs.SelectMany(c => c).Select(m => m.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal))
                    {
                        var matches = catalogs.Select(c => c.Where(m => m.Name == name).ToArray()).ToArray();
                        var sources = string.Join(" / ", componentTypes.Where(t => t != null).Select(t => t.Name));
                        if (matches.Any(m => m.Length != 1))
                            unavailable[name] = "并非所有所选组件都支持此名称，或存在同名扩展。请拆分规则，或在页面中单独选择。";
                        else
                            available[name] = sources + " · " + string.Join(" / ", matches.Select(m => m[0].ReadOnly ? "只读访问" : m[0].Member is System.Reflection.EventInfo ? "事件" : ((System.Reflection.PropertyInfo)m[0].Member).PropertyType.Name).Distinct());
                    }
                }
            }

            private static string[] ReadNames(SerializedProperty rule)
            {
                if (!rule.FindPropertyRelative("OverrideDefaultProperties").boolValue)
                {
                    return Array.Empty<string>();
                }
                var properties = rule.FindPropertyRelative("DefaultBindingProperties");
                return Enumerable.Range(0, properties.arraySize).Select(i => properties.GetArrayElementAtIndex(i).stringValue).ToArray();
            }

            public override Vector2 GetWindowSize() => new Vector2(470, 390);
            public override void OnGUI(Rect rect)
            {
                if (owner == null)
                {
                    editorWindow.Close();
                    return;
                }
                using (var state = new SerializedObject(owner))
                {
                    state.Update();
                    var rule = state.FindProperty(path);
                    if (rule == null || rule.FindPropertyRelative("Expression").stringValue != pattern)
                    {
                        editorWindow.Close();
                        return;
                    }
                    var selected = new HashSet<string>(ReadNames(rule), StringComparer.Ordinal);
                    GUI.SetNextControlName("MUI.RulePropertySearch");
                    EditorGUI.BeginChangeCheck();
                    search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
                    if (EditorGUI.EndChangeCheck())
                        scroll = Vector2.zero;
                    if (focusSearch)
                    {
                        EditorGUI.FocusTextInControl("MUI.RulePropertySearch");
                        focusSearch = false;
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        selectedOnly = GUILayout.Toggle(selectedOnly, "仅看已选", EditorStyles.miniButton, GUILayout.Width(80));
                        GUILayout.Label("已选 " + selected.Count + " · 勾选即时保存", EditorStyles.miniLabel);
                    }
                    var words = search.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var names = available.Keys.Concat(unavailable.Keys).Concat(selected).Distinct().Where(n => (!selectedOnly || selected.Contains(n)) &&
                        words.All(w => (n + " " + (available.TryGetValue(n, out var info) ? info : "")).IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0))
                        .OrderBy(n => n, StringComparer.Ordinal).ToArray();
                    using (var scope = new EditorGUILayout.ScrollViewScope(scroll))
                    {
                        scroll = scope.scrollPosition;
                        foreach (var name in names)
                        {
                            var wasSelected = selected.Contains(name);
                            var supported = available.TryGetValue(name, out var info);
                            var reason = unavailable.TryGetValue(name, out var why) ? why : "属性已不可用；保留已有配置，可取消勾选。";
                            bool next;
                            using (new EditorGUI.DisabledScope(!supported && !wasSelected))
                                next = EditorGUILayout.ToggleLeft(new GUIContent(name + (supported ? "  ·  " + info : "  ·  不可用"), supported ? info : reason), wasSelected);
                            if (next == wasSelected)
                                continue;
                            var ordered = ReadNames(rule).Where(n => n != name).ToList();
                            if (next)
                                ordered.Add(name);
                            rule.FindPropertyRelative("OverrideDefaultProperties").boolValue = true;
                            var properties = rule.FindPropertyRelative("DefaultBindingProperties");
                            properties.arraySize = ordered.Count;
                            for (var i = 0; i < ordered.Count; i++)
                                properties.GetArrayElementAtIndex(i).stringValue = ordered[i];
                            Undo.IncrementCurrentGroup();
                            Undo.SetCurrentGroupName("修改规则默认属性");
                            if (state.ApplyModifiedProperties() && owner is MUIEditorSettings authoring)
                                authoring.SaveSettings();
                            Undo.IncrementCurrentGroup();
                            if (next)
                                selected.Add(name);
                            else
                                selected.Remove(name);
                            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                        }
                        if (names.Length == 0)
                            EditorGUILayout.HelpBox("没有匹配属性。请先选择组件类型，或更换搜索词。", MessageType.Info);
                    }
                    if (GUILayout.Button(new GUIContent("完成", "关闭选择器；勾选已即时保存，支持 Undo。")))
                        editorWindow.Close();
                    if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                    {
                        editorWindow.Close();
                        Event.current.Use();
                    }
                }
            }
        }
    }
}
