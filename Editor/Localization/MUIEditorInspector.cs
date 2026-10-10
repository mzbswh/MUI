using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MUI.Editor.Localization
{
    /// <summary>保留序列化属性的编辑、撤销和属性抽屉，只替换显示名称。</summary>
    public static class MUIEditorInspector
    {
        public static void Draw(SerializedObject serialized, string propertyPath = null, bool includeRoot = true)
        {
            if (serialized.targetObject == null)
            {
                return;
            }
            serialized.UpdateIfRequiredOrScript();
            var property = propertyPath == null ? serialized.GetIterator() : serialized.FindProperty(propertyPath);
            if (property == null)
            {
                return;
            }
            var end = propertyPath == null ? null : property.GetEndProperty();
            var baseDepth = propertyPath == null ? 0 : property.depth + (includeRoot ? 0 : 1);
            var enterChildren = true;
            var first = propertyPath != null && includeRoot;
            while (first || property.NextVisible(enterChildren))
            {
                first = false;
                if (end != null && SerializedProperty.EqualContents(property, end))
                {
                    break;
                }
                using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
                {
                    var indent = EditorGUI.indentLevel;
                    try
                    {
                        EditorGUI.indentLevel = property.depth - baseDepth;
                        enterChildren = DrawProperty(property);
                    }
                    finally
                    {
                        EditorGUI.indentLevel = indent;
                    }
                }
            }
            serialized.ApplyModifiedProperties();
        }

        public static void DrawFields(SerializedObject serialized, params string[] paths)
        {
            if (serialized.targetObject == null)
            {
                return;
            }
            serialized.UpdateIfRequiredOrScript();
            foreach (var path in paths)
            {
                var property = serialized.FindProperty(path);
                if (property != null)
                {
                    DrawProperty(property);
                }
            }
            serialized.ApplyModifiedProperties();
        }

        public static void DrawField(SerializedObject serialized, string path, string tooltipKey)
        {
            if (serialized.targetObject == null)
            {
                return;
            }
            serialized.UpdateIfRequiredOrScript();
            var property = serialized.FindProperty(path);
            if (property != null)
            {
                DrawProperty(property, MUIEditorLocalization.Get(tooltipKey));
            }
            serialized.ApplyModifiedProperties();
        }

        private static bool DrawProperty(SerializedProperty property, string tooltip = null)
        {
            var label = new GUIContent(MUIEditorLocalization.PropertyName(property), tooltip ?? MUIEditorLocalization.Diagnostic(property.tooltip));
            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                property.isExpanded = EditorGUILayout.Foldout(property.isExpanded,
                    new GUIContent(label.text + " (" + property.arraySize + ")", label.tooltip), true);
                return property.isExpanded;
            }
            var type = property.propertyType == SerializedPropertyType.Enum ? FieldType(property) : null;
            if (type == null || !type.IsEnum)
            {
                return EditorGUILayout.PropertyField(property, label, false);
            }
            var previousMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            try
            {
                if (type.IsDefined(typeof(FlagsAttribute), false))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel(label);
                    if (EditorGUILayout.DropdownButton(new GUIContent(MUIEditorLocalization.Value(Enum.ToObject(type, property.intValue)), label.tooltip), FocusType.Keyboard))
                    {
                        ShowFlags(property, type);
                    }
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUI.BeginChangeCheck();
                    var choices = Array.ConvertAll(property.enumNames, name => MUIEditorLocalization.EnumName(type, name));
                    var next = EditorGUILayout.Popup(label, property.enumValueIndex, choices);
                    if (EditorGUI.EndChangeCheck())
                    {
                        property.enumValueIndex = next;
                    }
                }
            }
            finally
            {
                EditorGUI.showMixedValue = previousMixedValue;
            }
            return false;
        }

        private static void ShowFlags(SerializedProperty property, Type type)
        {
            var serialized = property.serializedObject;
            var path = property.propertyPath;
            var current = property.intValue;
            var menu = new GenericMenu();
            var names = Enum.GetNames(type);
            var values = Enum.GetValues(type);
            var all = 0;
            for (var i = 0; i < names.Length; ++i)
            {
                var mask = Convert.ToInt32(values.GetValue(i));
                if (mask <= 0 || (mask & (mask - 1)) != 0)
                {
                    continue;
                }
                all |= mask;
                var enabled = (current & mask) != 0;
                menu.AddItem(new GUIContent(MUIEditorLocalization.EnumName(type, names[i])), enabled,
                    () => SetFlags(serialized, path, mask, !enabled));
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent(MUIEditorLocalization.Get("value.none")), (current & all) == 0,
                () => SetFlags(serialized, path, all, false));
            menu.AddItem(new GUIContent(MUIEditorLocalization.Get("value.all")), (current & all) == all,
                () => SetFlags(serialized, path, all, true));
            menu.ShowAsContext();
        }

        private static void SetFlags(SerializedObject serialized, string path, int mask, bool enabled)
        {
            foreach (var target in serialized.targetObjects)
            {
                if (target == null)
                {
                    continue;
                }
                using (var item = new SerializedObject(target))
                {
                    var property = item.FindProperty(path);
                    if (property == null)
                    {
                        continue;
                    }
                    // 仅改变所选位；每个对象自己的其它位和未知位都保留。
                    property.intValue = enabled ? property.intValue | mask : property.intValue & ~mask;
                    item.ApplyModifiedProperties();
                }
            }
        }

        private static Type FieldType(SerializedProperty property)
        {
            var target = property.serializedObject.targetObject;
            if (target == null)
            {
                return null;
            }
            var type = target.GetType();
            var parts = property.propertyPath.Replace(".Array.data[", "[").Split('.');
            foreach (var part in parts)
            {
                var bracket = part.IndexOf('[');
                var name = bracket < 0 ? part : part.Substring(0, bracket);
                FieldInfo field = null;
                for (var current = type; current != null && field == null; current = current.BaseType)
                {
                    field = current.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                if (field == null)
                {
                    return null;
                }
                type = field.FieldType;
                if (bracket >= 0)
                {
                    type = type.IsArray ? type.GetElementType() : type.IsGenericType ? type.GetGenericArguments()[0] : null;
                    if (type == null)
                    {
                        return null;
                    }
                }
            }
            return type;
        }
    }
}
