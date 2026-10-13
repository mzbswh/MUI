using MUI.UGUI;
using UnityEditor;

namespace MUI.Editor
{
    [CustomEditor(typeof(ModalBackdropStyle))]
    public sealed class ModalBackdropStyleInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            Localization.MUIEditorInspector.DrawField(serializedObject, "tint", "modal.backdrop.tint.help");
            Localization.MUIEditorInspector.DrawField(serializedObject, "visualPrefab", "modal.backdrop.prefab.help");
        }
    }
}
