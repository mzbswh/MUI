using MUI.UGUI;
using UnityEditor;
using UnityEngine;

namespace MUI.Editor
{
    public sealed class ElementAuthoringWindow : EditorWindow
    {
        private View view;
        [MenuItem("Tools/MUI/创建/控件绑定", false, 20)]
        private static void Open() => GetWindow<ElementAuthoringWindow>("控件绑定");
        private void OnGUI()
        {
            view = (View)EditorGUILayout.ObjectField("View", view, typeof(View), true);
            EditorGUILayout.HelpBox("在 View Inspector 的原生控件绑定中选择组件和属性。无需添加控件包装组件。", MessageType.Info);
            using (new EditorGUI.DisabledScope(view == null))
                if (GUILayout.Button("选择 View")) Selection.activeObject = view;
        }
    }
}
