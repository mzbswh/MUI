namespace MUI
{
    /// <summary>声明渲染实例可以在旧激活及业务实例完全清理后移交给缓存。</summary>
    public interface ICacheableView : IView
    {
        /// <summary>
        /// 清除旧激活的焦点、输入、模型引用及活动资源配置，保持隐藏且不可交互。
        /// 此调用不启动新工作；失败的实例直接归还，不进入缓存。
        /// </summary>
        void ResetForCache();
    }
}
