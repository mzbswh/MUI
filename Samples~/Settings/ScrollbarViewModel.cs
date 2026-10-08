using MUI.UGUI;

namespace MUI.Samples.Settings
{
    /// <summary>独立滚动条绑定契约，供项目将名为 Position 的 ScrollbarElement 接入自有 View。</summary>
    [ViewContract("ScrollbarView")]
    public partial class ScrollbarViewModel : ViewModel
    {
        [ObservableProperty]
        [Bind("Position", nameof(ScrollbarElement.NumberOfSteps))]
        private int steps;
        [ObservableProperty]
        [Bind("Position", nameof(ScrollbarElement.Size))]
        private float handleSize = 0.2f;
        [ObservableProperty]
        [Bind("Position", nameof(ScrollbarElement.Value), bindingMode: BindingMode.TwoWay)]
        private float position = 0.25f;
    }
}
