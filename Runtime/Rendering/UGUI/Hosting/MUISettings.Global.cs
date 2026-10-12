using System;
using MUI.Navigation;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class MUISettings
    {
        private static MUISettings global;

        /// <summary>项目唯一配置；Editor 自动查找 Assets，Player 使用构建时预加载的同一资产。</summary>
        public static MUISettings Global
        {
            get
            {
#if UNITY_EDITOR
                global = null;
                var ids = UnityEditor.AssetDatabase.FindAssets("t:MUISettings", new[] { "Assets" });
                if (ids.Length > 1)
                {
                    var paths = Array.ConvertAll(ids, UnityEditor.AssetDatabase.GUIDToAssetPath);
                    throw new InvalidOperationException("MUISettings must be unique. Keep one project settings asset:\n" + string.Join("\n", paths));
                }
                if (ids.Length == 1)
                    global = UnityEditor.AssetDatabase.LoadAssetAtPath<MUISettings>(UnityEditor.AssetDatabase.GUIDToAssetPath(ids[0]));
#else
                if (global != null) return global;
                var loaded = UnityEngine.Resources.FindObjectsOfTypeAll<MUISettings>();
                if (loaded.Length > 1)
                    throw new InvalidOperationException("MUISettings must be unique. Multiple settings assets were loaded in the Player.");
                if (loaded.Length == 1) global = loaded[0];
#endif
                return global;
            }
        }

        public static RoutePolicy ResolveGlobalPolicy(string preset = null, string layer = null, RoutePolicyOverrides overrides = null)
        {
            var configuration = Global;
            return configuration == null ? ResolveBuiltInPolicy(preset, layer, overrides)
                : configuration.ResolvePolicy(preset, layer, overrides);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetGlobal() => global = null;

    }
}
