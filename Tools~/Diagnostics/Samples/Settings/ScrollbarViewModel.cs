using MUI.UGUI;

namespace MUI.Samples.Settings
{
    /// <summary>独立滚动条绑定契约，供项目将名为 Position 的 UnityEngine.UI.Scrollbar 接入自有 View。</summary>
    [UIWidget("ScrollbarView")]
    public partial class ScrollbarViewModel : ViewModel
    {
        [ObservableProperty]
        [Bind("Position", nameof(UnityEngine.UI.Scrollbar.numberOfSteps))]
        private int steps;
        [ObservableProperty]
        [Bind("Position", nameof(UnityEngine.UI.Scrollbar.size))]
        private float handleSize = 0.2f;
        [ObservableProperty]
        [Bind("Position", nameof(UnityEngine.UI.Scrollbar.value), bindingMode: BindingMode.TwoWay)]
        private float position = 0.25f;
    }
}
