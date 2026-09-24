namespace MUI
{
    public enum BindingMode
    {
        OneWay,
        TwoWay,
        OneWayToSource,
        /// <summary>每次绑定时向控件写入一次，不订阅后续属性变化。</summary>
        OneTime
    }
}
