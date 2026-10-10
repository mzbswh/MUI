using L = MUI.Editor.Localization.MUIEditorLocalization;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace MUI.BasicExample.Editor
{
    public static class BasicExampleMenu
    {
        private const string SceneGuid = "c1237d8dbbdbb46c78a66de5682afdcf";

        [MenuItem("MUI/基础示例 (Basic Example)/打开场景 (Open Scene)")]
        public static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scenePath = AssetDatabase.GUIDToAssetPath(SceneGuid);
            if (string.IsNullOrEmpty(scenePath))
            {
                throw new System.InvalidOperationException(L.Get("sample.basic.sceneMissing"));
            }

            EditorSceneManager.OpenScene(scenePath);
        }
    }
}
