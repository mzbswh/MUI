namespace MUI
{
    /// <summary>有效可见性，包含宿主、局部和原生激活门控。</summary>
    public interface IVisibilityView : IView
    {
        bool IsVisible
        {
            get;
        }
    }
}
