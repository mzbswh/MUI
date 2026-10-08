namespace MUI
{
    /// <summary>反向转换的值或输入校验失败；失败时 Value 不得写入模型。</summary>
    public readonly struct BindingConversionResult<T>
    {
        private readonly BindingValidationState validation;

        private BindingConversionResult(bool succeeded, T value, BindingValidationState validation)
        {
            Succeeded = succeeded;
            Value = value;
            this.validation = validation;
        }

        public bool Succeeded
        {
            get;
        }

        public T Value
        {
            get;
        }

        public BindingValidationState Validation => !Succeeded && validation.IsValid
            ? BindingValidationState.Invalid(null) : validation;

        public static BindingConversionResult<T> Success(T value) =>
            new BindingConversionResult<T>(true, value, default);

        public static BindingConversionResult<T> Failure(string message) =>
            new BindingConversionResult<T>(false, default, BindingValidationState.Invalid(message));
    }
}
