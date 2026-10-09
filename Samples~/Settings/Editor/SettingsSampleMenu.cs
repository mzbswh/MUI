using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MUI.Samples.Settings.Editor
{
    /// <summary>示例启动器，也支持限定时长的无界面运行模式预览。</summary>
    public static class SettingsSampleMenu
    {
        private static double deadline;
        private static double stopAt;
        private static bool failed;
        private static bool completed;

        [MenuItem("Tools/MUI/Samples/Open Settings Scene")]
        public static void Open()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            // 保留模板相机，避免 Game 视图的无相机提示遮住 Overlay UI。
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var sample = new GameObject("SettingsDemo", typeof(SettingsDemo)).GetComponent<SettingsDemo>();
            sample.AutomaticWalkthrough = Application.isBatchMode;
            if (!AssetDatabase.IsValidFolder("Assets/MUI Samples"))
            {
                AssetDatabase.CreateFolder("Assets", "MUI Samples");
            }

            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/MUI Samples/Settings.unity");
            EditorSceneManager.SaveScene(scene, path);
        }

        public static void RunPreviewBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("Use Open Settings Scene for interactive preview.");
            }

            Open();
            SessionState.SetBool("MUI.SettingsPreview", true);
            SessionState.SetFloat("MUI.SettingsPreviewStart", (float)EditorApplication.timeSinceStartup);
            // 在进入运行模式前订阅，兼容关闭域重载的编辑器配置。
            ResumePreview();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void ResumePreview()
        {
            if (!SessionState.GetBool("MUI.SettingsPreview", false))
            {
                return;
            }

            deadline = SessionState.GetFloat("MUI.SettingsPreviewStart", 0) + 45;
            stopAt = 0;
            failed = false;
            completed = false;
            EditorApplication.update -= UpdatePreview;
            EditorApplication.update += UpdatePreview;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        private static void OnLog(string condition, string trace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            {
                failed = true;
            }

            // 只接受演示末尾的清理完成标记，进入运行模式本身不代表演示完成。
            if (type == LogType.Log && string.Equals(condition, "MUI Settings binding cleanup: state=Unbound; saveExecuting=False", StringComparison.Ordinal))
            {
                completed = true;
                stopAt = EditorApplication.timeSinceStartup + 0.25;
            }
        }

        private static void UpdatePreview()
        {
            var now = EditorApplication.timeSinceStartup;
            // 完成后留出少量帧接收迟到错误；未完成必须等到截止时间并报告失败。
            if (now < deadline && (!completed || now < stopAt))
            {
                return;
            }

            SessionState.SetBool("MUI.SettingsPreview", false);
            EditorApplication.update -= UpdatePreview;
            Application.logMessageReceived -= OnLog;
            var timedOut = !completed;
            Debug.Log(timedOut ? "MUI Settings 演示超时，未收到清理完成标记。" : "MUI Settings Play-mode preview finished.");
            EditorApplication.Exit(failed || timedOut ? 1 : 0);
        }
    }
}
