namespace MUI.Resources
{
    /// <summary>
    /// 独立于加载模式的内容代际。内容未变时返回同一非空引用对象，
    /// 失效时更换新对象且不复用旧令牌；读取必须同步、快速、无副作用。
    /// 值类型每次读取会重新装箱，不能用作令牌。
    /// </summary>
    public interface IViewContentVersion
    {
        object ContentVersion
        {
            get;
        }
    }
}
