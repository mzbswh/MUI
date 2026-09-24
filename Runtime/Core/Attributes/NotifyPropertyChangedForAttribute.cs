using System;

namespace MUI
{
    /// <summary>
    /// 标记 ObservableProperty 字段：生成属性改变后，额外通知指定的计算属性。
    /// 仅发布通知，不求值、不赋值，也不推断或递归传播属性依赖。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
    public sealed class NotifyPropertyChangedForAttribute : Attribute
    {
        public NotifyPropertyChangedForAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }

        public string PropertyName
        {
            get;
        }
    }
}
