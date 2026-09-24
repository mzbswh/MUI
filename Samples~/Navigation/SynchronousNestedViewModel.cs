using MUI.UGUI;

namespace MUI.Samples.Navigation
{
    public sealed partial class SynchronousNestedDemo
    {
        /// <summary>
        /// 同步父视图只借用当前道具模型，替换引用触发嵌套子视图重新绑定。
        /// 模型按示例组件归组；生成绑定及路由工厂保留同一外层作用域。
        /// </summary>
        [ViewContract("SynchronousNested")]
        [MUI.Navigation.ViewRoute(typeof(Unit), typeof(Unit), SupportsSynchronousLifecycle = true)]
        public partial class SynchronousNestedViewModel : ViewModel
        {
            [ObservableProperty]
            [Bind("NestedItem", nameof(NestedViewElement.ViewModel))]
            private ThingItemViewModel item = new ThingItemViewModel { Label = "嵌套同步道具 × 10" };
        }
    }
}
