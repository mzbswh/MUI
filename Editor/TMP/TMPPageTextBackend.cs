using MUI.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.TMP.Editor
{
    /// <summary>将 TMP 页面模板接入通用向导；不向基础编辑器引入 TMP 依赖。</summary>
    [InitializeOnLoad]
    internal static class TMPPageTextBackend
    {
        static TMPPageTextBackend()
        {
            PageTextBackend.Register("tmp", "TextMeshPro", typeof(TMPTextElement), Validate, CreateText);
        }

        private static string Validate()
        {
            // 默认字体属性会直接访问配置；缺少基础资源时只返回提示，不触发导入窗口。
            var font = TMP_Settings.LoadDefaultSettings() == null ? null : TMP_Settings.defaultFontAsset;
            return font == null || !AssetDatabase.Contains(font)
                ? L.Get("editor.TMPPageTextBackend.539eed99df")
                : null;
        }

        private static Graphic CreateText(string name, Transform parent, string content)
        {
            var error = Validate();
            if (error != null)
            {
                throw new System.InvalidOperationException(error);
            }
            // 先加入向导拥有的层级，后续组件或字体设置失败也由整批回滚清理。
            var node = new GameObject(name, typeof(RectTransform));
            node.transform.SetParent(parent, false);
            var text = node.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 24;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }
    }
}
