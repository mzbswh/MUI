using System;
using UnityEngine.UI;

namespace MUI.UGUI
{
    [GenerateControlProperty(nameof(Target), "readOnly", "ReadOnly")]
    [GenerateControlProperty(nameof(Target), "characterLimit", "CharacterLimit")]
    [GenerateControlProperty(nameof(Target), "contentType", "ContentType", NotifyAll = true)]
    [GenerateControlProperty(nameof(Target), "lineType", "LineType", NotifyAll = true)]
    [GenerateControlProperty(nameof(Target), "characterValidation", "CharacterValidation", NotifyAll = true)]
    public sealed partial class InputFieldElement
    {
        /// <summary>用户输入的字符数上限，零表示不限；不承担项目业务校验。</summary>
        partial void OnCharacterLimitChanging(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        /// <summary>原生内容预设，可能联动修改行模式和字符过滤。</summary>
        partial void OnContentTypeChanging(InputField.ContentType value)
        {
            if (!Enum.IsDefined(typeof(InputField.ContentType), value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        /// <summary>原生单行或多行模式，可能将内容预设切换为 Custom。</summary>
        partial void OnLineTypeChanging(InputField.LineType value)
        {
            if (!Enum.IsDefined(typeof(InputField.LineType), value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        /// <summary>原生逐字字符过滤策略，不等于业务字段校验。</summary>
        partial void OnCharacterValidationChanging(InputField.CharacterValidation value)
        {
            if (!Enum.IsDefined(typeof(InputField.CharacterValidation), value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }
    }
}
