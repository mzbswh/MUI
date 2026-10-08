using System.Threading;
using System.Threading.Tasks;

namespace MUI.Resources
{
    public interface IPreloadViewProvider : IViewProvider
    {
        /// <summary>取得独立驻留凭证，不创建或激活页面；本地资源可以立即完成。</summary>
        Task<IAcquiredPreload> PreloadAsync(ViewResource resource, CancellationToken cancellationToken = default);
    }
}
