using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>
    /// Element 的同步自动化输入适配契约，由 UIAutomation 统一检查模式、归属和输入门控。
    /// 项目实现不得启动异步工作；应通过真实控件事件通知绑定，不直接改写模型。
    /// </summary>
    public interface IUIAutomationInput<T> : IElement
    {
        /// <summary>返回实际接收输入的原生控件；获取时不得初始化界面或派发业务。</summary>
        Selectable InputControl
        {
            get;
        }

        /// <summary>
        /// 在会话完成资格校验后直接赋值；只读等控件限制返回 false，非法值抛出参数异常。
        /// 返回 true 仅表示接受派发，不保证发生变化；不伪造提交、失焦或编辑结束。
        /// 不应绕过 UIAutomation 直接调用此方法。
        /// </summary>
        bool TrySetInput(T value);
    }
}
