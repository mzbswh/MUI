namespace MUI
{
    /// <summary>纯转换，实现不得加载资源或修改服务。</summary>
    public interface IBindingConverter<TSource, TTarget>
    {
        TTarget Convert(TSource value);

        TSource ConvertBack(TTarget value);
    }
}
