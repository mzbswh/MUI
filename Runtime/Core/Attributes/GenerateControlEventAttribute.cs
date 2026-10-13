using System;

namespace MUI
{
    /// <summary>生成原生 UnityEvent 的订阅、输入门控及清理；可在发布业务事件前通知绑定属性。</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class GenerateControlEventAttribute : Attribute
    {
        public GenerateControlEventAttribute(string target, string signal, string name = null)
        {
            Target = target;
            Signal = signal;
            Name = name;
        }

        public string Target { get; }

        public string Signal { get; }

        public string Name { get; }

        public string NotifyProperty { get; set; }

        /// <summary>可选的 bool 属性；用于输入提交时机等特殊行为。</summary>
        public string Condition { get; set; }

        /// <summary>可选的无参实例方法，处理聚合通知等特殊行为。</summary>
        public string Handler { get; set; }

        /// <summary>允许未配置目标组件；初始化时跳过订阅。</summary>
        public bool Optional { get; set; }

        /// <summary>将原生事件参数保留为 Action 参数；默认生成无参通知。</summary>
        public bool ForwardArguments { get; set; }
    }
}
