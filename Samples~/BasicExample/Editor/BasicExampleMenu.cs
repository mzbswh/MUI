using UnityEditor;
using UnityEditor.SceneManagement;

namespace MUI.BasicExample.Editor
{
    public static class BasicExampleMenu
    {
        private const string SceneGuid = "c1237d8dbbdbb46c78a66de5682afdcf";

        [MenuItem("MUI/Basic Example/Open Scene")]
        public static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scenePath = AssetDatabase.GUIDToAssetPath(SceneGuid);
            if (string.IsNullOrEmpty(scenePath))
            {
                throw new System.InvalidOperationException("Basic Example scene is missing. Import the sample from Package Manager.");
            }

            EditorSceneManager.OpenScene(scenePath);
        }
    }
}
