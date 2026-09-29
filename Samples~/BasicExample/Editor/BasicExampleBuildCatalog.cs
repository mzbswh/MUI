using MUI.Editor;
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
                var route = BasicPageViewModelRoute.Create(() => new BasicPageViewModel());
                var prefabPath = AssetDatabase.GUIDToAssetPath(PrefabGuid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                catalog.AddPage(route, prefab, BasicPageViewModelBindingFactory.Manifest);
            });
        }
    }
}
