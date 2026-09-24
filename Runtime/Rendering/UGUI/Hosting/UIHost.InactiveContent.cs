using System.Threading.Tasks;

namespace MUI.UGUI
{
    public sealed partial class UIHost
    {
        /// <summary>显式清理本宿主预加载及停用页面，不监听平台内存事件或操作项目资源池。</summary>
        public ValueTask ClearInactiveContentAsync() => Navigator.ClearInactiveContentAsync();
    }
}
