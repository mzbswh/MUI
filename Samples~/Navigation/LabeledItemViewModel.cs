using MUI.UGUI;

namespace MUI.Samples.Navigation
{
    /// <summary>
    /// 项目侧通用条目状态，借用业务数据，不负责加载、保存或释放它。
    /// 泛型绑定工厂可显式闭合注册，具体派生模型仍通过程序集入口注册。
    /// </summary>
    [ViewContract("ThingItem")]
    [MUI.Navigation.ViewRoute(typeof(Unit), typeof(Unit))]
    public partial class LabeledItemViewModel<TItem> : ViewModel where TItem : class
    {
        [ObservableProperty]
        private TItem item;
        [ObservableProperty]
        [Bind("ItemLabel", nameof(TextElement.Content))]
        private string label = string.Empty;
    }
}
