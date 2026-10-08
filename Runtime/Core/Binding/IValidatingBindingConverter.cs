namespace MUI
{
    /// <summary>纯转换与可预期的输入校验；失败返回结果，不通过异常控制正常编辑流程。</summary>
    public interface IValidatingBindingConverter<TSource, TTarget>
    {
        TTarget Convert(TSource value);

        BindingConversionResult<TSource> TryConvertBack(TTarget value);
    }
}
