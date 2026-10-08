using System;
using MUI.Resources;

namespace MUI.ChildViews
{
    /// <summary>不可变的子界面创建定义，独立于顶层导航。</summary>
    public sealed class ChildViewTemplate<TViewModel, TArgs>
        where TViewModel : ViewModel
    {
        public ChildViewTemplate(ViewResource resource,
                    Func<TViewModel> modelFactory,
                    Func<IView, TViewModel, BindingContext> bindingFactory,
                    Func<TViewModel, Presenter<TViewModel, TArgs, Unit>> presenterFactory = null,
                    bool pauseTickWhenHidden = false,
                    int maxTickCatchUp = 4)
        {
            if (maxTickCatchUp < 1 || maxTickCatchUp > 32)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTickCatchUp));
            }

            PauseTickWhenHidden = pauseTickWhenHidden;
            MaxTickCatchUp = maxTickCatchUp;
            Resource = resource ?? throw new ArgumentNullException(nameof(resource));
            ModelFactory = modelFactory ?? throw new ArgumentNullException(nameof(modelFactory));
            BindingFactory = bindingFactory ?? throw new ArgumentNullException(nameof(bindingFactory));
            PresenterFactory = presenterFactory ?? (_ => new EmptyPresenter<TViewModel, TArgs, Unit>());
        }

        /// <summary>有效不可见时是否暂停 Tick；不结束激活或释放资源。</summary>
        public bool PauseTickWhenHidden
        {
            get;
        }

        /// <summary>单帧最多补偿的定时 Tick 次数，限制积压工作。</summary>
        public int MaxTickCatchUp
        {
            get;
        }

        public ViewResource Resource
        {
            get;
        }

        internal Func<TViewModel> ModelFactory
        {
            get;
        }

        internal Func<IView, TViewModel, BindingContext> BindingFactory
        {
            get;
        }

        internal Func<TViewModel, Presenter<TViewModel, TArgs, Unit>> PresenterFactory
        {
            get;
        }
    }
}
