using System;
using MUI.UGUI;

namespace MUI.Examples
{
    [UIWidget("RewardItem")]
    public partial class RewardViewModel : ViewModel
    {
        public Action RemoveRequested;

        [Command]
        private void Remove() => RemoveRequested?.Invoke();
    }

    [UIPage(typeof(Unit), typeof(Unit), ResourceKey = "MUIExamples/Collections")]
    public partial class CollectionsViewModel : ViewModel
    {
        private int next;
        private readonly int capacity;

        public CollectionsViewModel(int capacity = 128)
        {
            this.capacity = capacity;
            for (var i = 0; i < 4; ++i)
            {
                Add();
            }
        }

        [Bind("Rewards", nameof(RecyclingListElement.Items))]
        public ObservableList<ViewModel> Items { get; } = new ObservableList<ViewModel>();

        [Command]
        private void Add()
        {
            if (Items.Count >= capacity)
            {
                Status = "全部 " + capacity + " 个槽位已占满，请先移除一个奖励。";
                return;
            }

            var item = new RewardViewModel
            {
                Title = "物资箱 " + ++next,
                Detail = "物资 · 待领取"
            };
            item.RemoveRequested = () =>
            {
                for (var i = 0; i < Items.Count; ++i)
                {
                    if (ReferenceEquals(Items[i], item))
                    {
                        Items.RemoveAt(i);
                        break;
                    }
                }

                Status = Items.Count + " 项奖励在集合中";
            };
            Items.Add(item);
            Status = Items.Count + " 项奖励在集合中";
        }
    }

    [UIPage(typeof(Unit), typeof(Unit), ResourceKey = "MUIExamples/VirtualList")]
    public partial class VirtualListViewModel : ViewModel
    {
        [ObservableProperty, Bind("Inventory", nameof(VirtualListElement.Columns))]
        private int columns = 1;

        public VirtualListViewModel()
        {
            for (var i = 0; i < 2000; ++i)
            {
                Items.Add(new VirtualListItem(i, new ItemViewModel { Title = "物品 " + (i + 1).ToString("D4"), Detail = "稳定键：" + i + " · 有库存" }));
            }
        }

        [Bind("Inventory", nameof(VirtualListElement.Items))]
        public ObservableList<VirtualListItem> Items { get; } = new ObservableList<VirtualListItem>(itemKey: item => item.Key);

        [Command]
        private void Grid() => Columns = Columns == 1 ? 3 : 1;
    }
}
