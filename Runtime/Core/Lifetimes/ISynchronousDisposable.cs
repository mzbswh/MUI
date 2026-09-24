using System;

namespace MUI
{
    /// <summary>
    /// 带同步释放准入检查的资源。查询必须只读、无副作用，不启动工作；
    /// 所有者在取消和释放前检查，避免子资源拒绝时父资源已被部分销毁。
    /// 真正释放时仍需重新检查，调用应遵守资源本身的线程约束。
    /// </summary>
    public interface ISynchronousDisposable : IDisposable
    {
        bool CanDisposeSynchronously
        {
            get;
        }
    }
}
