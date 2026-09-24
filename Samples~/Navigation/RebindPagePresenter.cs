using System;
using System.ComponentModel;

namespace MUI.Samples.Navigation
{
    /// <summary>展示换绑时迁移模型订阅；钩子部分失败后仍能反向恢复。</summary>
    public sealed class RebindPagePresenter : PagePresenter
    {
        public int OpenCount
        {
            get; private set;
        }

        public int Notifications
        {
            get; private set;
        }

        public PageViewModel CurrentModel => ViewModel;

        protected override void OnCreate()
        {
            base.OnCreate();
            ViewModel.PropertyChanged += ModelChanged;
        }

        protected override void OnOpen(PageArgs args)
        {
            ++OpenCount;
            base.OnOpen(args);
        }

        protected override void OnViewModelChanged(PageViewModel previous)
        {
            previous.PropertyChanged -= ModelChanged;
            if (ViewModel.Title == "拒绝换绑")
            {
                // 故意在部分工作完成后失败，演示框架反向调用钩子恢复原订阅。
                throw new InvalidOperationException("示例拒绝该模型。");
            }

            ViewModel.PropertyChanged += ModelChanged;
        }

        protected override void OnDestroy()
        {
            ViewModel.PropertyChanged -= ModelChanged;
            base.OnDestroy();
        }

        private void ModelChanged(object sender, PropertyChangedEventArgs args) => ++Notifications;
    }
}
