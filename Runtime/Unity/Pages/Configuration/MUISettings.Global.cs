using System;
using System.Collections.Generic;
using MUI.Pages;
using UnityEngine;

namespace MUI.Unity
{
    public partial class MUISettings
    {
        private static MUISettings global;

        /// <summary>项目唯一配置；Editor 自动查找 Assets，Player 使用构建时预加载的同一资产。</summary>
        public static MUISettings Global
        {
            get
            {
#if UNITY_EDITOR
                global = null;
                // Unity's type filter can omit assets of a derived settings class.
                // Discover extensions through TypeCache, without referencing any backend assembly.
                var matches = new HashSet<string>(UnityEditor.AssetDatabase.FindAssets("t:MUISettings", new[] { "Assets" }));
                foreach (var type in UnityEditor.TypeCache.GetTypesDerivedFrom<MUISettings>())
                    foreach (var id in UnityEditor.AssetDatabase.FindAssets("t:" + type.Name, new[] { "Assets" }))
                        matches.Add(id);
                var ids = new string[matches.Count];
                matches.CopyTo(ids);
                Array.Sort(ids, StringComparer.Ordinal);
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

        public static PagePolicy ResolveGlobalPolicy(string preset = null, string layer = null, PagePolicyOverrides overrides = null)
        {
            var configuration = Global;
            return configuration == null ? ResolveBuiltInPolicy(preset, layer, overrides)
                : configuration.ResolvePolicy(preset, layer, overrides);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetGlobal() => global = null;

    }
}
