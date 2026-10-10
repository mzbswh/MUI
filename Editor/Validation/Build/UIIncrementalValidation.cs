using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    /// <summary>合并资产变更，在导入和编译结束后只读校验；域重载保留待检查标记。</summary>
    [InitializeOnLoad]
    internal static class UIIncrementalValidation
    {
        private const string PendingKey = "MUI.UIIncrementalValidation.Pending";
        private const int MaxPendingPaths = 2048;
        private static readonly HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
        private static bool all;
        private static bool scheduled;
        private static double readyAt;
        private static string previousReport;

        static UIIncrementalValidation()
        {
            if (SessionState.GetBool(PendingKey, false))
            {
                // 编译会改变 Manifest 和目录声明，重载后重新采集全部已登记目录。
                RequestAll();
            }
        }

        internal static void RequestAll()
        {
            all = true;
            paths.Clear();
            Schedule();
        }

        internal static void Request(string[] imported)
        {
            foreach (var path in imported)
            {
                if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".asmref", StringComparison.OrdinalIgnoreCase))
                {
                    RequestAll();
                    return;
                }
                if (!all && !string.IsNullOrEmpty(path))
                {
                    paths.Add(path);
                    if (paths.Count >= MaxPendingPaths)
                    {
                        RequestAll();
                        return;
                    }
                }
            }
            if (all || paths.Count != 0)
            {
                Schedule();
            }
        }

        private static void Schedule()
        {
            SessionState.SetBool(PendingKey, true);
            readyAt = EditorApplication.timeSinceStartup + 0.2;
            if (!scheduled)
            {
                scheduled = true;
                EditorApplication.update += Flush;
            }
        }

        private static void Flush()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup < readyAt)
            {
                return;
            }
            EditorApplication.update -= Flush;
            scheduled = false;
            ISet<string> affected = all ? null : new HashSet<string>(paths, StringComparer.Ordinal);
            all = false;
            paths.Clear();
            SessionState.SetBool(PendingKey, false);
            // 先消费本批，校验回调期间的新变更保留给下一批；不写资产，避免递归导入。
            try
            {
                var report = UIBuildValidation.ValidateAffected(affected);
                if (report.CatalogCount == 0 || report.PageCount == 0 && report.Issues.Count == 0)
                {
                    return;
                }
                var text = L.Get("editor.UIIncrementalValidation.06bf605b8a") + report.ExportText();
                if (text == previousReport)
                {
                    return;
                }
                previousReport = text;
                if (report.IsValid)
                {
                    Debug.Log(text);
                }
                else
                {
                    Debug.LogWarning(text);
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }
    }
}
