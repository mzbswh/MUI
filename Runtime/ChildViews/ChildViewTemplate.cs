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
                    bool supportsSynchronousPreparation = false,
                    bool pauseTickWhenHidden = false,
                    int maxTickCatchUp = 4,
                    bool supportsSynchronousLifecycle = false)
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
            SupportsSynchronousPreparation = supportsSynchronousPreparation || supportsSynchronousLifecycle;
            SupportsSynchronousLifecycle = supportsSynchronousLifecycle;
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

        /// <summary>声明允许同步准备；实际提供方和 Presenter 仍须满足同步能力要求。</summary>
        public bool SupportsSynchronousPreparation
        {
            get;
        }

        /// <summary>
        /// 工厂声明完整同步生命周期：模型可同步释放、Presenter 无必需异步钩子，绑定及子依赖支持同步。
        /// 此声明不仅表示同步准备；工厂返回的派生实例也必须遵守本契约。
        /// </summary>
        public bool SupportsSynchronousLifecycle
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
