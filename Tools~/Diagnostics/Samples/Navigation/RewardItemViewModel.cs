using System;
using MUI.UGUI;

namespace MUI.Samples.Navigation
{
    /// <summary>奖励条目的同步删除入口，业务只修改集合，不等待控件回收自己。</summary>
    [UIWidget("RewardItem")]
    public partial class RewardItemViewModel : ViewModel
    {
        [ObservableProperty]
        [Bind("ItemLabel", nameof(UnityEngine.UI.Text.text))]
        private string label;
        [ObservableProperty]
        [Bind("Icon", nameof(ImageResources.SpriteSource))]
        private string iconKey = "Warm";
        [ObservableProperty]
        [Bind("ItemLabel", nameof(TextResources.FontSource))]
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
        [BindCommand("Remove", nameof(UnityEngine.UI.Button.onClick))]
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
