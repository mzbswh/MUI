using System;

namespace MUI.Navigation
{
    /// <summary>仅导航内容释放使用的诊断作用域，不参与资源所有权或清理调度。</summary>
    internal interface IViewResourceReleaseTraceScope : IDisposable
    {
        void Complete();

        void Fail(Exception error);
    }
}
