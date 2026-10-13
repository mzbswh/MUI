using System;
using TMPro;

namespace MUI.TMP
{
    [GenerateControlProperty(nameof(Target), "characterLimit", "CharacterLimit")]
    [GenerateControlProperty(nameof(Target), "contentType", "ContentType", NotifyAll = true)]
    [GenerateControlProperty(nameof(Target), "lineType", "LineType", NotifyAll = true)]
    [GenerateControlProperty(nameof(Target), "characterValidation", "CharacterValidation", NotifyAll = true)]
    public sealed partial class TMPInputFieldElement
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
        partial void OnContentTypeChanging(TMP_InputField.ContentType value)
        {
            if (!Enum.IsDefined(typeof(TMP_InputField.ContentType), value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        /// <summary>原生单行或多行模式，可能将内容预设切换为 Custom。</summary>
        partial void OnLineTypeChanging(TMP_InputField.LineType value)
        {
            if (!Enum.IsDefined(typeof(TMP_InputField.LineType), value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        /// <summary>原生逐字字符过滤策略，不等于业务字段校验。</summary>
        partial void OnCharacterValidationChanging(TMP_InputField.CharacterValidation value)
        {
            if (!Enum.IsDefined(typeof(TMP_InputField.CharacterValidation), value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }
    }
}
