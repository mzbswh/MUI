using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.Samples.ResourceIntegration
{
    /// <summary>语言目录加载入口；返回持有凭证，由服务负责释放。</summary>
    public interface ILocalizationProvider
    {
        /// <summary>加载指定规范语言名的目录；失败或取消不能遗留未交出的资源持有权。</summary>
        ValueTask<IAcquiredResource<LocalizationCatalog>> LoadAsync(string locale, CancellationToken cancellationToken);
    }
}
