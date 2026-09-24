using System;

namespace MUI
{
    /// <summary>
    /// 在生成属性实际变化且 SetProperty 成功返回后调用标记方法；批量通知期间回调仍立即执行。方法可无参数，
    /// 或接收新值，或依次接收旧值与新值；参数类型必须与生成属性一致。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class OnChangedAttribute : Attribute
    {
        public OnChangedAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }

        /// <summary>当前模型中由 ObservableProperty 生成的属性名称。</summary>
        public string PropertyName
        {
            get;
        }
    }
}
