using System;
using MUI.UGUI;

namespace MUI.Samples.Navigation
{
    /// <summary>奖励条目的同步删除入口，业务只修改集合，不等待控件回收自己。</summary>
    [ViewContract("RewardItem")]
    public partial class RewardItemViewModel : ViewModel
    {
        [ObservableProperty]
        [Bind("ItemLabel", nameof(TextElement.Content))]
        private string label;
        [ObservableProperty]
        [Bind("Icon", nameof(ImageElement.SpriteSource))]
        private string iconKey = "Warm";
        [ObservableProperty]
        [Bind("ItemLabel", nameof(TextElement.FontSource))]
        private string fontKey = "DefaultFont";

        public Action<RewardItemViewModel> RemoveRequested
        {
            get; set;
        }

        /// <summary>同键重试必须重新通知绑定，不能依赖相等值的属性赋值。</summary>
        public void RefreshFont()
        {
            OnPropertyChanged(nameof(FontKey));
        }

        [Command]
        [BindCommand("Remove", nameof(ButtonElement.Clicked))]
        private void Remove()
        {
            var handler = RemoveRequested;
            if (handler != null)
            {
                handler(this);
            }
        }
    }
}
