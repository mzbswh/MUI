using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MUI.Examples.Editor
{
    // Keep the EditorWindow base: Unity can restore this type from older saved editor layouts.
    public sealed class ExamplesHub : EditorWindow
    {
        private void OnEnable()
        {
            titleContent = new GUIContent("MUI Examples");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("MUI Examples", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("新版示例集中在工作台场景中。进入 Play 后，可查看每课的演示内容、配置操作和代码说明。", MessageType.Info);
            if (GUILayout.Button("打开示例工作台"))
            {
                Open();
            }
        }

        [MenuItem("Tools/MUI/示例", false, 2)]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var script = AssetDatabase.GUIDToAssetPath("8be0b3a2b49747698fbf1c772a64c8ed");
            var root = Path.GetDirectoryName(Path.GetDirectoryName(script)).Replace('\\', '/');
            var scene = root + "/Scenes/MUIExamples.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null)
            {
                throw new InvalidOperationException("请先导入完整的 MUI Examples 示例。");
            }
            EditorSceneManager.OpenScene(scene);
        }
    }
}
