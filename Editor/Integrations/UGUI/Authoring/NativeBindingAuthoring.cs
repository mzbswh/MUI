using System;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;

namespace MUI.Editor
{
    public static class NativeBindingAuthoring
    {
        public static void Register(View view, Component component, string name = null)
        {
            if (view == null || component == null) throw new ArgumentNullException();
            var serialized = new SerializedObject(view);
            var entries = serialized.FindProperty("nativeBindings");
            for (var i = 0; i < entries.arraySize; i++)
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("Target").objectReferenceValue == component) return;
            var row = entries.GetArrayElementAtIndex(entries.arraySize++);
            row.FindPropertyRelative("Name").stringValue = name ?? component.name;
            row.FindPropertyRelative("Target").objectReferenceValue = component;
            row.FindPropertyRelative("ComponentType").stringValue = component.GetType().AssemblyQualifiedName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
