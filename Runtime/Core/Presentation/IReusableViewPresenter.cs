namespace MUI
{
    /// <summary>
    /// 旧版 Presenter 复用标记，仅保留源码兼容性，不再决定缓存资格。
    /// 缓存只保留 ICacheableView 及可移交凭证，每次打开创建新的 Presenter。
    /// </summary>
    [System.Obsolete("Presenter instances are not cached. Use ICacheableView and ICacheableViewAcquisition.")]
    public interface IReusableViewPresenter
    {
    }
}
