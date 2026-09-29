using UnityEditor;
using UnityEditor.SceneManagement;

namespace MUI.BasicExample.Editor
{
    public static class BasicExampleMenu
    {
        private const string ScenePath = "Assets/Basic/Basic.unity";

        [MenuItem("MUI/Basic Example/Open Scene")]
        public static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
