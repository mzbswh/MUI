using System;
using System.Collections.Generic;
using MUI.Resources;

namespace MUI.Navigation
{
    /// <summary>不可变身份、创建配置与策略；缓存可复用兼容的实例组。</summary>
    public abstract class Route
    {
        protected Route(string key, ViewResource resource, RoutePolicy policy,
                    long? estimatedRetainedBytes = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Route key is required.", nameof(key));
            }

            if (estimatedRetainedBytes.HasValue && estimatedRetainedBytes.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(estimatedRetainedBytes));
            }

            EstimatedRetainedBytes = estimatedRetainedBytes;
            Key = key;
            Resource = resource ?? throw new ArgumentNullException(nameof(resource));
            Policy = policy ?? RoutePolicy.Default;
        }

        /// <summary>只读的依赖描述，不执行参数工厂；供导航注册及编辑器图校验使用。</summary>
        public virtual IReadOnlyList<RouteDependencyDescriptor> DependencyDescriptors => Array.Empty<RouteDependencyDescriptor>();

        /// <summary>单实例停用后保留内容的保守字节估算；null 表示未知，不包含活动业务内存。</summary>
        public long? EstimatedRetainedBytes
        {
            get;
        }

        public string Key
        {
            get;
        }

        public ViewResource Resource
        {
            get;
        }

        public RoutePolicy Policy
        {
            get;
        }

    }

    public sealed class Route<TViewModel, TArgs, TResult> : Route where TViewModel : ViewModel
    {
        public Route(string key,
                    ViewResource resource,
                    Func<TViewModel> viewModelFactory,
                    Func<TViewModel, Presenter<TViewModel, TArgs, TResult>> presenterFactory = null,
                    Func<IView, TViewModel, BindingContext<TViewModel>> bindingFactory = null,
                    RoutePolicy policy = null,
                    Func<TArgs, TArgs, bool> argsEqual = null,
                    long? estimatedRetainedBytes = null,
                    IReadOnlyList<RouteDependency<TArgs>> dependencies = null) : base(key,
                    resource,
                    policy,
                    estimatedRetainedBytes)
        {
            var copiedDependencies = new RouteDependency<TArgs>[dependencies == null ? 0 : dependencies.Count];
            if (copiedDependencies.Length > 64)
            {
                throw new ArgumentException("每个路由最多声明 64 个直接依赖。", nameof(dependencies));
            }
            for (var i = 0; i < copiedDependencies.Length; ++i)
            {
                copiedDependencies[i] = dependencies[i] ?? throw new ArgumentException("依赖声明不能为空。", nameof(dependencies));
            }
            Dependencies = Array.AsReadOnly(copiedDependencies);
            var descriptors = new RouteDependencyDescriptor[copiedDependencies.Length];
            for (var i = 0; i < descriptors.Length; ++i)
            {
                descriptors[i] = new RouteDependencyDescriptor(copiedDependencies[i].Target, copiedDependencies[i].Placement, copiedDependencies[i].IsRequired);
            }
            DependencyDescriptors = Array.AsReadOnly(descriptors);
            ModelFactory = viewModelFactory ?? throw new ArgumentNullException(nameof(viewModelFactory));
            PresenterFactory = presenterFactory ?? (model => new EmptyPresenter<TViewModel, TArgs, TResult>());
            BindingFactory = bindingFactory ?? BindingRegistry.CreateExact<TViewModel>;
            ArgsEqual = argsEqual ?? System.Collections.Generic.EqualityComparer<TArgs>.Default.Equals;
        }

        /// <summary>按声明顺序准备的必需依赖；构造时复制，不允许外部修改路由配置。</summary>
        public IReadOnlyList<RouteDependency<TArgs>> Dependencies
        {
            get;
        }

        public override IReadOnlyList<RouteDependencyDescriptor> DependencyDescriptors
        {
            get;
        }

        internal Func<TViewModel> ModelFactory
        {
            get;
        }

        internal Func<TViewModel, Presenter<TViewModel, TArgs, TResult>> PresenterFactory
        {
            get;
        }

        internal Func<IView, TViewModel, BindingContext<TViewModel>> BindingFactory
        {
            get;
        }

        internal Func<TArgs, TArgs, bool> ArgsEqual
        {
            get;
        }
    }
}
