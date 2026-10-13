using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MUI.Editor
{
    /// <summary>项目创作配置，仅在编辑器加载，不加入 Player 预加载资产。</summary>
    [FilePath("ProjectSettings/MUIEditorSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed partial class MUIEditorSettings : ScriptableSingleton<MUIEditorSettings>
    {
        [SerializeField] private bool outputCompilerGeneratedSources = true;
        public bool OutputCompilerGeneratedSources => outputCompilerGeneratedSources;

        internal void SaveSettings()
        {
            Save(true);
            CompilerGeneratedSources.RequestApplySettings();
        }

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            var expressions = new HashSet<string>(StringComparer.Ordinal);
            if (controlNamingRules != null)
                foreach (var rule in controlNamingRules)
                    if (rule == null || string.IsNullOrWhiteSpace(rule.Expression) || !expressions.Add(rule.Match + ":" + rule.Expression) || !Enum.IsDefined(typeof(ControlNameMatch), rule.Match) || rule.ComponentTypes == null || rule.ComponentTypes.Count == 0)
                        errors.Add("控件命名表达式不能为空或重复，且必须配置组件类型。");
            try
            {
                ResolvePageDirectory(pagePrefabDirectory, "Example");
                ResolvePageDirectory(pageScriptDirectory, "Example");
            }
            catch (ArgumentException error) { errors.Add(error.Message); }
            return errors;
        }
    }
}
