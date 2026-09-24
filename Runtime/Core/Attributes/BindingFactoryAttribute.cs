using System;

namespace MUI
{
    /// <summary>供编辑器发现生成工厂的元数据；运行时仍使用显式注册。</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class BindingFactoryAttribute : Attribute
    {
        public BindingFactoryAttribute(Type viewModelType)
        {
            ViewModelType = viewModelType;
        }

        public Type ViewModelType
        {
            get;
        }
    }
}
