using MUI.Editor.Localization;
using MUI.UGUI;
using UnityEditor;

namespace MUI.Editor
{
    public abstract class MUIComponentInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI() => MUIEditorInspector.Draw(serializedObject);
    }

    [CustomEditor(typeof(Element), true)]
    [CanEditMultipleObjects]
    internal sealed class ElementInspector : MUIComponentInspector
    {
    }

    [CustomEditor(typeof(UIBackInput))]
    internal sealed class UIBackInputInspector : MUIComponentInspector
    {
    }

    [CustomEditor(typeof(SafeAreaFitter))]
    internal sealed class SafeAreaFitterInspector : MUIComponentInspector
    {
    }

    [CustomEditor(typeof(TooltipTrigger))]
    internal sealed class TooltipTriggerInspector : MUIComponentInspector
    {
    }

    [CustomEditor(typeof(ContextMenuController))]
    internal sealed class ContextMenuControllerInspector : MUIComponentInspector
    {
    }

    [CustomEditor(typeof(VirtualListItemSelection))]
    internal sealed class VirtualListItemSelectionInspector : MUIComponentInspector
    {
    }
}
