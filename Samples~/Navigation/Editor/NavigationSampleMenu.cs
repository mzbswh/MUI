using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MUI.Samples.Navigation.Editor
{
    /// <summary>示例启动器，也支持限定时长的无界面运行模式预览。</summary>
    public static class NavigationSampleMenu
    {
        private static double deadline;
        private static double stopAt;
        private static bool failed;
        private static bool completed;

        [MenuItem("Tools/MUI/Samples/Open Recycling List Scene")]
        public static void OpenRecyclingList()
        {
            OpenStandaloneSample<SynchronousRecyclingListDemo>("RecyclingList");
        }

        [MenuItem("Tools/MUI/Samples/Open Resource Binding Scene")]
        public static void OpenResourceBinding()
        {
            OpenStandaloneSample<ResourceImageDemo>("ResourceBinding");
        }

        /// <summary>仅创建并保存独立示例场景，由用户配置加载模式后进入运行模式。</summary>
        private static void OpenStandaloneSample<T>(string sceneName) where T : MonoBehaviour
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sample = new GameObject(typeof(T).Name, typeof(T));
            if (!AssetDatabase.IsValidFolder("Assets/MUI Samples"))
            {
                AssetDatabase.CreateFolder("Assets", "MUI Samples");
            }

            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/MUI Samples/" + sceneName + ".unity");
            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new InvalidOperationException("示例场景保存失败：" + sceneName);
            }

            Selection.activeGameObject = sample;
        }

        [MenuItem("Tools/MUI/Samples/Open Navigation Scene")]
        public static void Open() => OpenNavigationScene(false);

        [MenuItem("Tools/MUI/Samples/Open Navigation Lifecycle Trace Scene")]
        public static void OpenLifecycleTrace() => OpenNavigationScene(true);

        private static void OpenNavigationScene(bool lifecycleTrace)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()))
            {
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sample = new GameObject("NavigationDemo", typeof(NavigationDemo)).GetComponent<NavigationDemo>();
            sample.AutomaticWalkthrough = Application.isBatchMode;
            sample.RecordLifecycleTrace = lifecycleTrace;
            sample.ShowEnterTransitionPreview = lifecycleTrace;
            sample.ShowCacheWalkthrough = lifecycleTrace;
            if (!AssetDatabase.IsValidFolder("Assets/MUI Samples"))
            {
                AssetDatabase.CreateFolder("Assets", "MUI Samples");
            }

            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/MUI Samples/Navigation.unity");
            EditorSceneManager.SaveScene(scene, path);
        }

        public static void RunPreviewBatch() => RunPreviewBatch(false);

        public static void RunLifecycleTracePreviewBatch() => RunPreviewBatch(true);

        private static void RunPreviewBatch(bool lifecycleTrace)
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("Use Open Navigation Scene for interactive preview.");
            }

            OpenNavigationScene(lifecycleTrace);
            SessionState.SetBool("MUI.NavigationPreview", true);
            SessionState.SetFloat("MUI.NavigationPreviewStart", (float)EditorApplication.timeSinceStartup);
            // 在进入运行模式前订阅，兼容关闭域重载的编辑器配置。
            ResumePreview();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void ResumePreview()
        {
            if (!SessionState.GetBool("MUI.NavigationPreview", false))
            {
                return;
            }

            deadline = SessionState.GetFloat("MUI.NavigationPreviewStart", 0) + 45;
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
            if (type == LogType.Log && string.Equals(condition, "MUI Navigation shutdown complete", StringComparison.Ordinal))
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

            SessionState.SetBool("MUI.NavigationPreview", false);
            EditorApplication.update -= UpdatePreview;
            Application.logMessageReceived -= OnLog;
            var timedOut = !completed;
            Debug.Log(timedOut ? "MUI Navigation 演示超时，未收到清理完成标记。" : "MUI Navigation Play-mode preview finished.");
            EditorApplication.Exit(failed || timedOut ? 1 : 0);
        }
    }
}
