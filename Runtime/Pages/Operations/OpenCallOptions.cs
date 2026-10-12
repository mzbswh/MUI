using System.Threading;

namespace MUI.Pages
{
    /// <summary>生成打开入口的导航选项，与业务参数分开传递。</summary>
    public readonly struct OpenCallOptions
    {
        public OpenCallOptions(CancellationToken cancellationToken = default, PageOwner owner = default)
        {
            CancellationToken = cancellationToken;
            Owner = owner;
        }

        public CancellationToken CancellationToken { get; }
        public PageOwner Owner { get; }
    }
}
