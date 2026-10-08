namespace MUI
{
    /// <summary>
    /// 表现状态不拥有特定 View。借用的 ViewModel 可以比
    /// 其投影存活更久，单个 View 不能将其销毁。
    /// </summary>
    public abstract class ViewModel : ObservableObject
    {
    }
}
