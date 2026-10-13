using System;

namespace MUI.UGUI
{
    public sealed partial class View
    {
        /// <summary>自动化只使用已建立的绑定索引，不初始化或重新收集界面。</summary>
        internal TElement FindAutomationElement<TElement>(string elementName) where TElement : class, IElement
        {
            RequireAlive();
            if (index == null)
            {
                throw new InvalidOperationException("自动化查找要求 View 已初始化。");
            }
            return index.Get<TElement>(elementName);
        }

        internal UIAutomationStatus CheckAutomationReadiness()
        {
            if (!IsAlive || index == null || childViews == null || !childViews.IsActive)
            {
                return UIAutomationStatus.NotReady;
            }
            return UIAutomationStatus.Accepted;
        }
    }
}
