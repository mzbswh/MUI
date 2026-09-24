using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MUI.Editor
{
    /// <summary>阻止已登记 UI 目录带着已知错误进入 Player 构建。</summary>
    public sealed class UIBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var result = UIBuildValidation.Validate();
            if (result.CatalogCount == 0)
            {
                Debug.LogWarning(result.ExportText());
                return;
            }
            if (!result.IsValid)
            {
                throw new BuildFailedException(result.ExportText());
            }
        }
    }
}
