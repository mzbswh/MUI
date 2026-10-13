using MUI.UGUI;
using UnityEditor;

namespace MUI.Editor
{
    internal static class UGUISettingsMenu
    {
        [MenuItem("Assets/Create/MUI/配置")]
        private static void OpenOrCreate() => RuntimeSettingsAsset.OpenOrCreate<UGUIHostSettings>();
    }
}
