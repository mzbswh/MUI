namespace MUI.Resources
{
    /// <summary>显式允许视图凭证移交到缓存，必要后端依赖必须一直有效至实际归还。</summary>
    public interface ICacheableViewAcquisition : IAcquiredView
    {
        /// <summary>不能延长后端依赖生命周期的凭证应返回 false。</summary>
        bool SupportsCaching
        {
            get;
        }

        /// <summary>
        /// 在缓存命中后、建立新激活之前同步注入当前资源配置。
        /// 不建立绑定或启动业务工作；失败时淘汰凭证，不复用受损实例。
        /// </summary>
        void PrepareForReuse();
    }
}
