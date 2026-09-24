using MUI.Localization;
using MUI.Resources;

namespace MUI.Samples.ResourceIntegration
{
    /// <summary>
    /// 独立同步语言目录契约，不要求实现异步方法。
    /// 成功交出目录凭证，失败同步清理部分构造；不能阻塞异步加载或启动后台释放。
    /// </summary>
    public interface ISynchronousLocalizationProvider
    {
        /// <summary>取得与规范语言名匹配的目录；回滚失败须抛出 SynchronousResourceLoadException。</summary>
        ISynchronousResourceLease<LocalizationCatalog> Load(string locale);
    }
}
