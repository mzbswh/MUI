using MUI.UGUI;

namespace MUI.Samples.Navigation
{
    /// <summary>奖励页只提供条目集合，生成绑定负责连接回收列表。</summary>
    [ViewContract("RewardsView")]
    public partial class RewardsViewModel : ViewModel
    {
        [ObservableProperty]
        [Bind("Lst_Rewards", nameof(RecyclingListElement.Items))]
        private IReadOnlyObservableList<ViewModel> rewards;
    }
}
