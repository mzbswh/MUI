using System;

namespace MUI.UGUI
{
    public sealed partial class View
    {
        /// <summary>旧激活及业务实例排空后重置渲染状态，缓存不保留旧输入与资源加载器。</summary>
        public void ResetForCache()
        {
            RequireResourceConfigurationIdle();
            Initialize();
            // 静态子 View 和列表池节点也不能把旧焦点、输入阻挡或父资源配置带入新激活。
            foreach (var nested in GetComponentsInChildren<View>(true))
            {
                if (nested != null && nested.IsAlive)
                {
                    nested.ResetInactiveState();
                }
            }
        }

        private void ResetInactiveState()
        {
            RequireResourceConfigurationIdle();
            if (activeResourceContext != null)
            {
                throw new InvalidOperationException("View still retains its previous resource activation.");
            }
            hostVisible = false;
            hostInteractable = false;
            InvalidateInputGestures();
            ResetFocusState();
            ClearLocalBackHandlers();
            ReleaseModalBarrier();
            if (childViews != null)
            {
                childViews.TickActivityChanged -= NotifyChildTicks;
                childViews = null;
            }
            childActivationToken = default;
            activeActivation = null;
            resourceLoader = null;
            if (inputGate != null)
            {
                inputGate.Changed -= ApplyGates;
                inputGate.Dispose();
            }
            inputGate = new InputGate();
            inputGate.Changed += ApplyGates;
            localVisible = true;
            localInteractable = true;
            enterAlpha = 1;
            exitAlpha = 1;
            exitVisible = true;
            ApplyGates();
            foreach (var element in elements.ToArray())
            {
                if (element != null && element.IsAlive)
                {
                    element.ResetForCache();
                }
            }
        }
    }
}
