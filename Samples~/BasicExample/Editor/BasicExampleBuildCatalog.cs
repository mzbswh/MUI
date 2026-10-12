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

        [InitializeOnLoadMethod]
        private static void Register()
        {
            UIBuildValidation.RegisterCatalog("MUI.BasicExample", catalog =>
            {
                var route = BasicPageViewModelRoute.Create(() => new BasicPageViewModel(), policy: MUISettings.ResolveGlobalPolicy());
                var prefabPath = AssetDatabase.GUIDToAssetPath(PrefabGuid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                catalog.AddPage(route, prefab, BasicPageViewModelBindingFactory.Manifest);
            });
        }
    }
}
