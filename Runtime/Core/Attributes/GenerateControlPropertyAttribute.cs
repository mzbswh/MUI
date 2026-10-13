using System;

namespace MUI
{
    /// <summary>
    /// 为 partial Element 生成原生属性转发。组件访问器负责存活检查；属性类型从原生组件推导。
    /// 特殊校验可实现 partial void On{属性名}Changing(属性类型 value)，赋值后可实现 On{属性名}Changed()。
    /// 原生无通知 setter 可通过 Setter 指定；资源持有等特殊语义继续手写。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class GenerateControlPropertyAttribute : Attribute
    {
        public GenerateControlPropertyAttribute(string target, string property, string name)
        {
            Target = target;
            Property = property;
            Name = name;
        }

        public string Target { get; }
        public string Property { get; }
        public string Name { get; }
        /// <summary>原生 setter 会联动其他属性时，发布整组变化。</summary>
        public bool NotifyAll { get; set; }

        /// <summary>可选原生 setter 方法，例如 SetValueWithoutNotify。</summary>
        public string Setter { get; set; }
    }
}
