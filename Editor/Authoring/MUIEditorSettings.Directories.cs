using System;
using UnityEngine;

namespace MUI.Editor
{
    public sealed partial class MUIEditorSettings
    {
        public const string DefaultPagePrefabDirectory = "Assets/{PageName}/Resources";
        public const string DefaultPageScriptDirectory = "Assets/{PageName}/Scripts";

        [SerializeField] private string pagePrefabDirectory = DefaultPagePrefabDirectory;
        [SerializeField] private string pageScriptDirectory = DefaultPageScriptDirectory;

        public string PagePrefabDirectory => pagePrefabDirectory;
        public string PageScriptDirectory => pageScriptDirectory;

        /// <summary>解析页面目录规则；仅允许 Assets 内的路径和 PageName 占位符。</summary>
        public static string ResolvePageDirectory(string rule, string pageName)
        {
            if (string.IsNullOrWhiteSpace(rule))
                throw new ArgumentException("Page directory rule cannot be empty.");
            var path = rule.Replace("{PageName}", pageName).TrimEnd('/');
            if (path.Equals("Assets", StringComparison.OrdinalIgnoreCase))
                path = "Assets";
            else if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                path = "Assets" + path.Substring(6);
            else
                throw new ArgumentException("Page directory rule must start with Assets/: " + rule);
            ValidateDirectory(path);
            return path;
        }

        private static void ValidateDirectory(string path)
        {
            foreach (var part in path.Split('/'))
            {
                if (string.IsNullOrWhiteSpace(part) || part == "." || part == ".." || part != part.Trim() ||
                    part.EndsWith(".", StringComparison.Ordinal) || part.IndexOfAny(new[] { '\\', ':', '*', '?', '"', '<', '>', '|', '{', '}' }) >= 0)
                    throw new ArgumentException("Invalid page directory or unsupported placeholder: " + path);
                foreach (var character in part)
                    if (char.IsControl(character))
                        throw new ArgumentException("Invalid character in page directory.");
            }
        }
    }
}
