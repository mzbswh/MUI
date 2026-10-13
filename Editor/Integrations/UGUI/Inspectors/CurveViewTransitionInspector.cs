using MUI.Editor.Localization;
using MUI.UGUI;
using UnityEditor;

namespace MUI.Editor
{
    [CustomEditor(typeof(CurveViewTransition)), CanEditMultipleObjects]
    public sealed class CurveViewTransitionInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI() => MUIEditorInspector.Draw(serializedObject);
    }
}
