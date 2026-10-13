using MUI.UGUI;

namespace MUI.Examples
{
    [UIWidget("ItemCard")]
    public partial class ItemViewModel : ViewModel
    {
        // 普通状态由关联 Prefab 生成。
    }

    [UIPage(typeof(Unit), typeof(Unit), ResourceKey = "MUIExamples/Widgets")]
    public partial class WidgetsViewModel : ViewModel
    {
        private int revision;

        [ObservableProperty, Bind("ItemCard", nameof(WidgetReference.VM))]
        private ItemViewModel item = new ItemViewModel();

        [Command]
        private void Replace()
        {
            Item = new ItemViewModel
            {
                Title = "铁矿石",
                Detail = "替换次数：" + ++revision + " · 稀有"
            };
            Status = "模型已替换，复用已有子视图节点。";
        }

        [Command]
        private void Clear()
        {
            Item = null;
            Status = "子模型已清空，点击“替换物品”重新绑定。";
        }
    }
}
