using L = MUI.Editor.Localization.MUIEditorLocalization;
using MUI.Editor;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;

namespace MUI.BasicExample.Editor
{
    public static class BasicExampleBuildCatalog
    {
        private const string PrefabGuid = "e2bfbc679d5ad4db984d56f676928255";
        private const string SettingsGuid = "90d9552856a54619a6d3bcd6c087c754";

        [InitializeOnLoadMethod]
        private static void Register()
        {
            UIBuildValidation.RegisterCatalog("MUI.BasicExample", catalog =>
            {
                var settings = AssetDatabase.LoadAssetAtPath<MUISettings>(AssetDatabase.GUIDToAssetPath(SettingsGuid));
                if (settings == null)
                {
                    throw new System.InvalidOperationException(L.Get("sample.basic.settingsMissing"));
                }
                var route = BasicPageViewModelRoute.Create(() => new BasicPageViewModel(), policy: settings.ResolvePolicy());
                var prefabPath = AssetDatabase.GUIDToAssetPath(PrefabGuid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                catalog.AddPage(route, prefab, BasicPageViewModelBindingFactory.Manifest);
            });
        }
    }
}
