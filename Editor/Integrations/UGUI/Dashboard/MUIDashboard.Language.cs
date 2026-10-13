using UnityEngine.UIElements;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public sealed partial class MUIDashboard
    {
        private void BindLanguage()
        {
            // 保留词条热更新；控制台不再提供语言选择。
            L.Track(rootVisualElement.Q<VisualElement>(className: "mui-dashboard"), RefreshLanguage);
            RefreshLanguage();
        }

        private void RefreshLanguage()
        {
            titleContent.text = L.Get("editor.MUIDashboard.788f44b794");
            rootVisualElement.Q<IntegerField>("snapshotLimit").tooltip = L.Get("uxml.MUIDashboard.68289cbdb5");
            rootVisualElement.Q<IntegerField>("traceCapacity").tooltip = L.Get("uxml.MUIDashboard.ee29a6789d");
            viewDiagnostics = null;
            Capture();
            ShowTrace();
            RefreshValidationReport();
        }
    }
}
