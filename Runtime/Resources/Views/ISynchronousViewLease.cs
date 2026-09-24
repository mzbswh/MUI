using System;

namespace MUI.Resources
{
    /// <summary>
    /// 可同步归还的视图持有权。宿主须先结束 Presenter、绑定和子视图，
    /// 再释放原生视图；归还不得启动后台清理或阻塞等待任务。
    /// </summary>
    public interface ISynchronousViewLease : IDisposable
    {
        IView View
        {
            get;
        }
    }
}
