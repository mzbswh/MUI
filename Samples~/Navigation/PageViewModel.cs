using MUI.UGUI;

namespace MUI.Samples.Navigation
{
    [ViewContract("NavigationView")]
    public partial class PageViewModel : PageViewModelBase
    {
        [ObservableProperty]
        [Bind("NestedItem", nameof(NestedViewElement.ViewModel))]
        private ThingItemViewModel item = new ThingItemViewModel { Label = "Stone × 5" };
        [ObservableProperty]
        [Bind("DynamicItem", nameof(DynamicViewElement.Source))]
        private string dynamicSource = "ThingItem";
        [ObservableProperty]
        [Bind("DynamicItem", nameof(DynamicViewElement.ViewModel))]
        private ThingItemViewModel dynamicItem = new ThingItemViewModel { Label = "Iron × 3" };
        [ObservableProperty]
        [Bind("VirtualItems", nameof(VirtualListElement.Items))]
        private IReadOnlyObservableList<VirtualListItem> virtualItems = CreateVirtualItems(100);
        [ObservableProperty]
        [Bind("VirtualItems", nameof(VirtualListElement.SelectedKey), bindingMode: BindingMode.TwoWay)]
        private object selectedItemKey;

        public int Selection
        {
            get; set;
        }

        public static ObservableList<VirtualListItem> CreateVirtualItems(int count)
        {
            var values = new System.Collections.Generic.List<VirtualListItem>();
            for (var i = 0; i < count; i++)
            {
                values.Add(new VirtualListItem(i, new ThingItemViewModel { Label = "Item " + i }));
            }

            return new ObservableList<VirtualListItem>(values, itemKey: value => value.Key);
        }

        [Command]
        [BindCommand("Confirm", nameof(ButtonElement.Clicked))]
        private void Confirm(CommandContext context) => context.Complete(Selection);
    }
}
