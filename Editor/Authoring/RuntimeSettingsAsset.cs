using System;
using System.Linq;
using MUI.UGUI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    /// <summary>项目级配置入口，统一 Editor 发现与 Player 预加载。</summary>
    [InitializeOnLoad]
    public sealed class RuntimeSettingsAsset : IPreprocessBuildWithReport
    {
        static RuntimeSettingsAsset()
        {
            EditorApplication.projectChanged += Synchronize;
            EditorApplication.delayCall += Synchronize;
        }

        public int callbackOrder => -1000;

        public static bool TryGet(out MUISettings settings, out string error)
        {
            try
            {
                settings = MUISettings.Global;
                error = null;
                return true;
            }
            catch (InvalidOperationException failure)
            {
                settings = null;
                error = failure.Message;
                if (error.StartsWith("MUISettings must be unique.", StringComparison.Ordinal))
                {
                    var details = error.IndexOf('\n');
                    error = L.Get("globalSettings.duplicates") + (details < 0 ? string.Empty : error.Substring(details));
                }
                return false;
            }
        }

        [MenuItem("Assets/Create/MUI/配置")]
        public static void OpenOrCreate()
        {
            try
            {
                var settings = GetOrCreate();
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
            }
            catch (Exception failure) { Debug.LogException(failure); }
        }

        public static MUISettings GetOrCreate()
        {
            var settings = MUISettings.Global;
            if (settings == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/MUI"))
                    AssetDatabase.CreateFolder("Assets", "MUI");
                const string path = "Assets/MUI/MUISettings.asset";
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                    throw new InvalidOperationException("Cannot create MUISettings: " + path + " is already occupied.");
                settings = ScriptableObject.CreateInstance<MUISettings>();
                try
                {
                    AssetDatabase.CreateAsset(settings, path);
                    AssetDatabase.SaveAssets();
                }
                catch
                {
                    if (settings != null && !EditorUtility.IsPersistent(settings))
                        UnityEngine.Object.DestroyImmediate(settings);
                    throw;
                }
            }
            IncludeInPlayer(settings);
            return settings;
        }

        private static void Synchronize()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;
            if (TryGet(out var settings, out _))
                IncludeInPlayer(settings);
        }

        private static void IncludeInPlayer(MUISettings settings)
        {
            var current = PlayerSettings.GetPreloadedAssets();
            var next = current.Where(asset => !(asset is MUISettings)).Concat(settings == null ? Array.Empty<UnityEngine.Object>() : new UnityEngine.Object[] { settings }).ToArray();
            if (!current.SequenceEqual(next))
                PlayerSettings.SetPreloadedAssets(next);
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            try
            {
                var settings = MUISettings.Global;
                if (settings != null)
                {
                    settings.RequireValid();
                }
                IncludeInPlayer(settings);
            }
            catch (Exception failure) { throw new BuildFailedException(failure.Message); }
        }
    }
}
