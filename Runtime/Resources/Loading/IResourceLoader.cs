using System.Threading;
using System.Threading.Tasks;

namespace MUI.Resources
{
    /// <summary>
    /// 资源系统适配器。每次成功调用返回独立持有权，
    /// 失败时由适配器释放部分构造资源。取消是合作式的，
    /// 成功返回的迟到凭证仍由接收者负责。
    /// Unity 适配器检查原生对象空值，并在主线程执行 Unity 操作。
    /// </summary>
    public interface IResourceLoader
    {
        ValueTask<IAcquiredResource<T>> LoadAsync<T>(string key, CancellationToken cancellationToken)
            where T : class;
    }
}
