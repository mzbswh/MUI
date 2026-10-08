using System.Collections.Generic;
using MUI.UGUI;
using UnityEngine.UI;

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
                errors.Add($"输入控件缺少同节点的 InputField：{path}。");
                return;
            }

            var text = input.textComponent;
            if (text == null)
            {
                errors.Add($"输入框缺少文字组件：{path}。");
            }
            else
            {
                ValidateInputGraphic(text, input, view, path, "文字", errors);
            }

            var placeholder = input.placeholder;
            if (placeholder != null)
            {
                ValidateInputGraphic(placeholder, input, view, path, "占位", errors);
                if (text != null && placeholder == text)
                {
                    errors.Add($"输入框的占位组件不能与可编辑文字共用同一组件：{path}。");
                }
            }
        }

        private static void ValidateInputGraphic(Graphic graphic, InputField input, View view,
            string path, string role, ICollection<string> errors)
        {
            if (!graphic.transform.IsChildOf(input.transform))
            {
                errors.Add($"输入框{role}组件必须属于输入框层级：{path}。");
            }

            if (graphic.GetComponentInParent<View>(true) != view)
            {
                errors.Add($"输入框{role}引用不能跨越 View 边界：{path}。");
            }
        }
    }
}
