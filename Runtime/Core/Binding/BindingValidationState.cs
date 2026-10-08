using System;

namespace MUI
{
    /// <summary>可绑定的输入校验结果；默认值表示有效，不保留输入对象或异常引用。</summary>
    public readonly struct BindingValidationState : IEquatable<BindingValidationState>
    {
        private BindingValidationState(string message)
        {
            Message = message;
        }

        public bool IsValid => Message == null;

        public string Message
        {
            get;
        }

        public static BindingValidationState Invalid(string message) =>
            new BindingValidationState(string.IsNullOrWhiteSpace(message) ? "输入值无法转换。" : message);

        public bool Equals(BindingValidationState other) => StringComparer.Ordinal.Equals(Message, other.Message);

        public override bool Equals(object obj) => obj is BindingValidationState other && Equals(other);

        public override int GetHashCode() => Message == null ? 0 : StringComparer.Ordinal.GetHashCode(Message);
    }
}
