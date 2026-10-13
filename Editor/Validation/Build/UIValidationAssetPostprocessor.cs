using UnityEditor;

namespace MUI.Editor
{
    /// <summary>导入回调只登记路径，实际规则在 Editor 空闲时执行。</summary>
    internal sealed class UIValidationAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            PrefabBindingCompilation.Changed(importedAssets);
            PrefabBindingCompilation.Changed(movedAssets);
            PrefabBindingCompilation.Changed(deletedAssets, deleted: true);
            PrefabBindingCompilation.Changed(movedFromAssetPaths, deleted: true);
            if (deletedAssets.Length != 0 || movedAssets.Length != 0 || movedFromAssetPaths.Length != 0)
            {
                // 删除和移动后依赖查询可能已丢失旧路径，完整重采集避免漏检。
                UIIncrementalValidation.RequestAll();
            }
            else
            {
                UIIncrementalValidation.Request(importedAssets);
            }
        }
    }
}
