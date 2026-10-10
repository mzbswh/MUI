using System.Collections.Generic;
using MUI.UGUI;
using UnityEngine.UI;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public static partial class ViewContractValidator
    {
        /// <summary>只检查原生序列化引用，不初始化控件或执行字符校验委托。</summary>
        private static void ValidateInputField(Element element, View view, ICollection<string> errors)
        {
            var path = Path(element.transform, view.transform);
            var input = element.GetComponent<InputField>();
            if (input == null)
            {
                errors.Add(L.Format("editor.ViewContractValidator.Input.6f265ce9c2", path));
                return;
            }

            var text = input.textComponent;
            if (text == null)
            {
                errors.Add(L.Format("editor.ViewContractValidator.Input.9efb123a24", path));
            }
            else
            {
                ValidateInputGraphic(text, input, view, path, L.Get("editor.ViewContractValidator.Input.14b69bd6ee"), errors);
            }

            var placeholder = input.placeholder;
            if (placeholder != null)
            {
                ValidateInputGraphic(placeholder, input, view, path, L.Get("editor.ViewContractValidator.Input.064eb4483e"), errors);
                if (text != null && placeholder == text)
                {
                    errors.Add(L.Format("editor.ViewContractValidator.Input.233424aeac", path));
                }
            }
        }

        private static void ValidateInputGraphic(Graphic graphic, InputField input, View view,
            string path, string role, ICollection<string> errors)
        {
            if (!graphic.transform.IsChildOf(input.transform))
            {
                errors.Add(L.Format("editor.ViewContractValidator.Input.bf1a049371", role, path));
            }

            if (graphic.GetComponentInParent<View>(true) != view)
            {
                errors.Add(L.Format("editor.ViewContractValidator.Input.47c241b87f", role, path));
            }
        }
    }
}
