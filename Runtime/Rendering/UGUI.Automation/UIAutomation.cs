using System;
using System.Threading;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>
    /// 由项目显式创建的本地自动化会话，必须在 Unity 主线程使用。
    /// 不安装远程端口，不初始化 View，不修改导航账本；模式决定是否允许派发异步业务。
    /// 所有方法直接返回，异步业务完成需要项目随后查询状态，不能通过阻塞任务模拟同步。
    /// </summary>
    public sealed partial class UIAutomation
    {
        private readonly View view;
        private readonly LifetimeMode mode;
        private readonly int thread;
        private bool dispatching;

        public UIAutomation(View view, LifetimeMode mode)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }
            if (!Enum.IsDefined(typeof(LifetimeMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }
            this.view = view;
            this.mode = mode;
            thread = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>沿当前 View 的既有绑定边界查找；缺失、歧义或失效按绑定契约报错。</summary>
        public TElement FindElement<TElement>(string name) where TElement : Element
        {
            RequireThread();
            if (view == null)
            {
                throw new ObjectDisposedException(nameof(View));
            }
            return view.FindAutomationElement<TElement>(name);
        }

        /// <summary>读取当前界面门控，不读取输入框文本或业务参数，不初始化界面。</summary>
        public ViewInputSnapshot QueryState(int maxBlockerReasons = 128)
        {
            RequireThread();
            if (view == null)
            {
                throw new ObjectDisposedException(nameof(View));
            }
            return view.CaptureInputSnapshot(maxBlockerReasons);
        }

        /// <summary>
        /// 经过元素和原生按钮资格校验后调用 onClick，沿真实绑定执行命令并保留其自身 CanExecute 检查。
        /// Accepted 仅说明已派发，按钮未绑定命令或命令自身拒绝时不能据此报告业务成功。
        /// 不模拟指针按下/拖动、射线遮挡或焦点变化；这些应通过输入系统验收。
        /// </summary>
        public UIAutomationStatus InvokeCommand(string buttonName)
        {
            var status = BeginDispatch();
            if (status != UIAutomationStatus.Accepted)
            {
                return status;
            }
            try
            {
                var element = FindElement<ButtonElement>(buttonName);
                var control = element.GetComponent<UnityEngine.UI.Button>();
                if (!CanDispatch(element, control))
                {
                    return UIAutomationStatus.InputBlocked;
                }
                control.onClick.Invoke();
                return UIAutomationStatus.Accepted;
            }
            finally
            {
                dispatching = false;
            }
        }

        /// <summary>派发文本赋值；标准输入框与 TMP 各自遵循原生赋值语义，不模拟逐字键盘输入。</summary>
        public UIAutomationStatus SetInput(string elementName, string value) => SetInput<string>(elementName, value);

        /// <summary>派发布尔输入；标准 Toggle 保留原生互斥组和变化通知。</summary>
        public UIAutomationStatus SetInput(string elementName, bool value) => SetInput<bool>(elementName, value);

        /// <summary>派发浮点输入；标准 Slider 拒绝非有限值并保留原生限幅和整数规则。</summary>
        public UIAutomationStatus SetInput(string elementName, float value) => SetInput<float>(elementName, value);

        /// <summary>派发整数输入；标准下拉框和 TMP 下拉框只接受有效选项索引。</summary>
        public UIAutomationStatus SetInput(string elementName, int value) => SetInput<int>(elementName, value);

        /// <summary>
        /// 按输入契约查找标准或自定义 Element，统一校验后同步派发到适配器。
        /// 适配器负责值校验和真实控件事件；返回 Accepted 不代表业务成功或值未被回调覆盖。
        /// </summary>
        public UIAutomationStatus SetInput<T>(string elementName, T value)
        {
            var status = BeginDispatch();
            if (status != UIAutomationStatus.Accepted)
            {
                return status;
            }
            try
            {
                var input = view.FindAutomationElement<IUIAutomationInput<T>>(elementName);
                var element = input as Element;
                if (element == null)
                {
                    throw new InvalidOperationException("自动化输入适配器必须继承 Element。");
                }
                var control = input.InputControl;
                if (!CanDispatch(element, control))
                {
                    return UIAutomationStatus.InputBlocked;
                }
                return input.TrySetInput(value) ? UIAutomationStatus.Accepted : UIAutomationStatus.InputBlocked;
            }
            finally
            {
                dispatching = false;
            }
        }

        private UIAutomationStatus BeginDispatch()
        {
            RequireThread();
            if (dispatching || evaluatingCondition)
            {
                return UIAutomationStatus.Reentrant;
            }
            var status = view == null ? UIAutomationStatus.NotReady : view.CheckAutomationMode(mode);
            if (status == UIAutomationStatus.Accepted)
            {
                dispatching = true;
            }
            return status;
        }

        private bool CanDispatch(Element element, UnityEngine.UI.Selectable control)
        {
            // IsInteractable 可执行项目覆写，之后再次确认对象和所属 View 的当前资格。
            return element != null && element.GetComponentInParent<View>(true) == view &&
                control != null && control.GetComponentInParent<View>(true) == view &&
                element.CanReceiveAutomationInput(control) &&
                view != null && view.CheckAutomationMode(mode) == UIAutomationStatus.Accepted &&
                element != null && element.IsAlive && element.GetComponentInParent<View>(true) == view &&
                control != null && control.GetComponentInParent<View>(true) == view && view.IsInputEnabled;
        }

        private void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("自动化会话只能在创建它的 Unity 主线程使用。");
            }
        }
    }
}
