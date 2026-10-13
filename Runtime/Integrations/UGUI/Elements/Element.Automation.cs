namespace MUI.UGUI
{
    public abstract partial class Element
    {
        /// <summary>复用真实输入门控，未初始化的元素不能通过自动化触发原生事件。</summary>
        internal bool CanReceiveAutomationInput(UnityEngine.UI.Selectable control) =>
            elementInitialized && CanReceiveInput(control);
    }
}
