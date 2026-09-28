using System;

namespace MUI
{
    /// <summary>共享生命周期执行器的可选诊断边界，不决定页面状态或清理顺序。</summary>
    internal enum ViewPreparationStep
    {
        PresenterCreate,
        Binding,
        PresenterOpen,
        PresenterOpenAsync
    }

    internal interface IViewPreparationTraceScope : IDisposable
    {
        void Complete();
    }
}
