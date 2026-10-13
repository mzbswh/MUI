using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    /// <summary>原生 UGUI 与 TMP 共用的模板规则，不激活或修改创作对象。</summary>
    public static class DropdownTemplateValidation
    {
        public static void Validate(RectTransform template, Component itemText, Component itemImage, string location, ICollection<string> errors)
        {
            if (errors == null)
            {
                throw new ArgumentNullException(nameof(errors));
            }

            if (template == null)
            {
                errors.Add(L.Format("editor.DropdownTemplateValidation.05ffe01799", location));
                return;
            }

            if (template.gameObject.activeSelf)
            {
                errors.Add(L.Format("editor.DropdownTemplateValidation.2323fb4b98", location));
            }

            Toggle item = null;
            // 原生初始化仅在 GetComponentInChildren<Toggle>() 前激活模板根节点。
            // 模拟该选择流程，不临时启用用户场景中的任何对象。
            foreach (var toggle in template.GetComponentsInChildren<Toggle>(true))
            {
                var available = true;
                for (var node = toggle.transform; node != template; node = node.parent)
                {
                    if (!node.gameObject.activeSelf)
                    {
                        available = false;
                        break;
                    }
                }

                if (!available)
                {
                    continue;
                }

                item = toggle;
                break;
            }

            if (item == null || item.transform == template)
            {
                errors.Add(L.Format("editor.DropdownTemplateValidation.5d2cad0d1c", location));
                return;
            }

            if (!(item.transform.parent is RectTransform))
            {
                errors.Add(L.Format("editor.DropdownTemplateValidation.aa11691f69", location));
            }

            if (itemText != null && !itemText.transform.IsChildOf(item.transform))
            {
                errors.Add(L.Format("editor.DropdownTemplateValidation.c4d2ef65fd", location));
            }

            if (itemImage != null && !itemImage.transform.IsChildOf(item.transform))
            {
                errors.Add(L.Format("editor.DropdownTemplateValidation.d98d996fa4", location));
            }
        }
    }
}
