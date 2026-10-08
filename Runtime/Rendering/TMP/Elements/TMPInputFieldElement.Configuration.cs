using System;
using TMPro;

namespace MUI.TMP
{
    public sealed partial class TMPInputFieldElement
    {
        /// <summary>用户输入的字符数上限，零表示不限；不承担项目业务校验。</summary>
        public int CharacterLimit
        {
            get => Target.characterLimit;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                var control = Target;
                if (control.characterLimit == value)
                {
                    return;
                }

                control.characterLimit = value;
                if (IsAlive && control != null)
                {
                    NotifyChanged();
                }
            }
        }

        /// <summary>原生内容预设，可能联动修改行模式和字符过滤。</summary>
        public TMP_InputField.ContentType ContentType
        {
            get => Target.contentType;
            set
            {
                if (!Enum.IsDefined(typeof(TMP_InputField.ContentType), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                var control = Target;
                if (control.contentType == value)
                {
                    return;
                }

                control.contentType = value;
                if (IsAlive && control != null)
                {
                    NotifyChanged(string.Empty);
                }
            }
        }

        /// <summary>原生单行或多行模式，可能将内容预设切换为 Custom。</summary>
        public TMP_InputField.LineType LineType
        {
            get => Target.lineType;
            set
            {
                if (!Enum.IsDefined(typeof(TMP_InputField.LineType), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                var control = Target;
                if (control.lineType == value)
                {
                    return;
                }

                control.lineType = value;
                if (IsAlive && control != null)
                {
                    NotifyChanged(string.Empty);
                }
            }
        }

        /// <summary>原生逐字字符过滤策略，不等于业务字段校验。</summary>
        public TMP_InputField.CharacterValidation CharacterValidation
        {
            get => Target.characterValidation;
            set
            {
                if (!Enum.IsDefined(typeof(TMP_InputField.CharacterValidation), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                var control = Target;
                if (control.characterValidation == value)
                {
                    return;
                }

                control.characterValidation = value;
                if (IsAlive && control != null)
                {
                    NotifyChanged(string.Empty);
                }
            }
        }
    }
}
