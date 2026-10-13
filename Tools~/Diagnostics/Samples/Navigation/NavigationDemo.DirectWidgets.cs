using System;
using System.Threading.Tasks;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        [SerializeField] private bool directWidgetsOnly;

        public bool DirectWidgetsOnly
        {
            get => directWidgetsOnly; set => directWidgetsOnly = value;
        }

        private static async Task DemonstrateDirectWidgetAsync(View view, PageViewModel vm)
        {
            var widget = view.GetElement<WidgetReference>("NestedItem");
            Debug.Log("MUI direct Widget initial: " + ((ThingItemViewModel)widget.DisplayedViewModel).Label);
            var previous = vm.Item;
            vm.Item = new ThingItemViewModel { Label = "Stone × 8" };
            await widget.PendingChange;
            previous.Label = "Detached old item";
            Debug.Log("MUI direct Widget replaced: " + ((ThingItemViewModel)widget.DisplayedViewModel).Label);
            vm.Item = null;
            await widget.PendingChange;
            Debug.Log("MUI direct Widget null clears: " + (widget.DisplayedViewModel == null));
            vm.Item = new ThingItemViewModel { Label = "Stone × 12" };
            await widget.PendingChange;
            Debug.Log("MUI direct Widget restored: " + ((ThingItemViewModel)widget.DisplayedViewModel).Label);
            Debug.Log("MUI direct Widget hierarchy unchanged: " + (view.ChildWidgets[0].Target.transform.parent == view.transform));
        }
    }
}
