using System.Threading;
using System.Threading.Tasks;

namespace MUI.Resources
{
    /// <summary>取得隐藏视图的使用凭证。交付前的构造失败由提供方回滚；交付后由调用方归还。</summary>
    public interface IViewProvider
    {
        /// <summary>
        /// 本地资源可以立即完成，不强制切线程或等待下一帧。
        /// 取消采用合作语义；取消后仍返回的凭证也必须由调用方接管并归还。
        /// </summary>
        Task<IAcquiredView> AcquireAsync(ViewResource resource, CancellationToken cancellationToken = default);
    }
}
