using System;
using UnityEngine.UI;

namespace MUI.UGUI
{
    public sealed partial class InputFieldElement
    {
        /// <summary>是否禁止用户编辑；仍允许模型主动更新文本。</summary>
        public bool ReadOnly
        {
            get => Target.readOnly;
            set
            {
                var control = Target;
                if (control.readOnly == value)
                {
                    return;
                }

                control.readOnly = value;
                if (IsAlive && control != null)
                {
                    NotifyChanged();
                }
            }
        }

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
        public InputField.ContentType ContentType
        {
            get => Target.contentType;
            set
            {
                if (!Enum.IsDefined(typeof(InputField.ContentType), value))
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
        public InputField.LineType LineType
        {
            get => Target.lineType;
            set
            {
                if (!Enum.IsDefined(typeof(InputField.LineType), value))
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
        public InputField.CharacterValidation CharacterValidation
        {
            get => Target.characterValidation;
            set
            {
                if (!Enum.IsDefined(typeof(InputField.CharacterValidation), value))
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
