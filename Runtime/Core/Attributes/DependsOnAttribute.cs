using System;

namespace MUI
{
    /// <summary>计算属性依赖当前模型的生成属性；实际变化时发布通知，不递归传播依赖。</summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public sealed class DependsOnAttribute : Attribute
    {
        public DependsOnAttribute(string propertyName) => PropertyName = propertyName;

        public string PropertyName { get; }
    }
}
