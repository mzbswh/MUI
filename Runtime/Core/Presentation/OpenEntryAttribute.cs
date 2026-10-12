using System;

namespace MUI
{
    /// <summary>
    /// 声明显示前的业务初始化入口；调用时 ViewModel、绑定和 Context 已建立。
    /// 普通逻辑使用 void；返回 Task 或 ValueTask 时，框架等待完成后才提交打开并显示界面。
    /// 可选的末尾 CancellationToken 由框架注入。此入口不表示界面已显示或入场动画已完成。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OpenEntryAttribute : Attribute
    {
        /// <summary>按业务参数签名生成打开重载；同一 Presenter 可声明多个。</summary>
        public OpenEntryAttribute() { }

        public OpenEntryAttribute(string name) => Name = name;

        public string Name { get; }
    }
}
