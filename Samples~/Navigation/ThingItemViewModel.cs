namespace MUI.Samples.Navigation
{
    /// <summary>借用的列表项状态，独立于父页面的导航结果。</summary>
    [ViewContract("ThingItem")]
    public partial class ThingItemViewModel : LabeledItemViewModel<string>
    {
        /// <summary>具体模型沿用泛型基类的标签绑定，资源契约仍由本模型声明。</summary>
        public ThingItemViewModel()
        {
            Item = "Wood";
            Label = "Wood × 10";
        }
    }
}
