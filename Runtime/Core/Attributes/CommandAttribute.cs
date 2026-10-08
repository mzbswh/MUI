using System;

namespace MUI
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class CommandAttribute : Attribute
    {
        public CommandAttribute(string name = null)
        {
            Name = name;
        }

        public string Name
        {
            get;
        }

        /// <summary>
        /// 当前模型或基类中可访问的布尔属性或无参布尔方法名。
        /// 条件成员遵循 C# 的成员遮蔽规则，属性必须具有可访问的 getter。
        /// </summary>
        public string CanExecute
        {
            get; set;
        }

        public CommandConcurrency Concurrency { get; set; } = CommandConcurrency.RejectWhileRunning;

        public int Capacity { get; set; } = 32;
    }
}
