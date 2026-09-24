using System;

namespace MUI.Navigation
{
    /// <summary>依赖相对父页面的准备、激活和同层显示顺序；两种方式都在父显示前完成准备。</summary>
    public enum DependencyPlacement
    {
        RequiredBefore,
        AttachedAfter
    }

    /// <summary>父路由的共享依赖；参数工厂保留父参数与依赖参数的编译期类型约束。</summary>
    public abstract class RouteDependency<TParentArgs>
    {
        private protected RouteDependency()
        {
        }

        /// <summary>共享目标必须是单实例、Unit 结果路由，不能共享独立交互结果。</summary>
        public abstract Route Target
        {
            get;
        }

        /// <summary>相对父页面的准备、激活与同层显示位置。</summary>
        public abstract DependencyPlacement Placement
        {
            get;
        }

        /// <summary>必需依赖失败会终结父页面；可选依赖失败则撤销关系并发布降级通知。</summary>
        public abstract bool IsRequired
        {
            get;
        }

        /// <summary>声明父提交前必须准备成功的依赖；工厂应只根据父参数生成不可变参数。</summary>
        public static RouteDependency<TParentArgs> Required<TViewModel, TArgs>(
            Route<TViewModel, TArgs, Unit> route, Func<TParentArgs, TArgs> argsFactory,
            DependencyPlacement placement = DependencyPlacement.RequiredBefore)
            where TViewModel : ViewModel
        {
            return new SharedDependency<TViewModel, TArgs>(route, argsFactory, placement, true);
        }

        /// <summary>声明不需要业务参数的必需依赖。</summary>
        public static RouteDependency<TParentArgs> Required<TViewModel>(Route<TViewModel, Unit, Unit> route,
            DependencyPlacement placement = DependencyPlacement.RequiredBefore)
            where TViewModel : ViewModel => Required(route, _ => Unit.Value, placement);

        /// <summary>
        /// 声明可选依赖，明确允许失败后继续显示父页面。准备失败先完成回滚，运行期间退出则撤销关系。
        /// 通过 GetDependencyFailures 和 DependencyDegraded 生命周期事件读取原因；取消、配置环及清理失败不降级。
        /// </summary>
        public static RouteDependency<TParentArgs> Optional<TViewModel, TArgs>(
            Route<TViewModel, TArgs, Unit> route, Func<TParentArgs, TArgs> argsFactory,
            DependencyPlacement placement = DependencyPlacement.RequiredBefore)
            where TViewModel : ViewModel => new SharedDependency<TViewModel, TArgs>(route, argsFactory, placement, false);

        /// <summary>声明不需要业务参数的可选依赖。</summary>
        public static RouteDependency<TParentArgs> Optional<TViewModel>(Route<TViewModel, Unit, Unit> route,
            DependencyPlacement placement = DependencyPlacement.RequiredBefore)
            where TViewModel : ViewModel => Optional(route, _ => Unit.Value, placement);

        internal abstract DependencyRequest Resolve(TParentArgs args);

        private sealed class SharedDependency<TViewModel, TArgs> : RouteDependency<TParentArgs>
                    where TViewModel : ViewModel
        {
            private readonly Route<TViewModel, TArgs, Unit> route;
            private readonly Func<TParentArgs, TArgs> argsFactory;

            internal SharedDependency(Route<TViewModel, TArgs, Unit> route, Func<TParentArgs, TArgs> argsFactory,
                            DependencyPlacement placement, bool isRequired)
            {
                if (!Enum.IsDefined(typeof(DependencyPlacement), placement))
                {
                    throw new ArgumentOutOfRangeException(nameof(placement));
                }
                Placement = placement;
                IsRequired = isRequired;
                this.route = route ?? throw new ArgumentNullException(nameof(route));
                this.argsFactory = argsFactory ?? throw new ArgumentNullException(nameof(argsFactory));
                if (route.Policy.AllowMultiple || route.Policy.MaxInstances != 1)
                {
                    throw new ArgumentException("共享依赖必须使用单实例路由。", nameof(route));
                }
            }

            public override bool IsRequired
            {
                get;
            }

            public override Route Target => route;

            public override DependencyPlacement Placement
            {
                get;
            }

            internal override DependencyRequest Resolve(TParentArgs args) =>
                            new DependencyRequest<TViewModel, TArgs>(route, argsFactory(args), Placement, IsRequired);
        }
    }

    /// <summary>内部异构候选请求；类型化实现负责创建及参数比较，不以 null 或 object 打开依赖。</summary>
    internal abstract class DependencyRequest
    {
        internal abstract Route Route
        {
            get;
        }

        internal abstract DependencyPlacement Placement
        {
            get;
        }

        internal abstract bool IsRequired
        {
            get;
        }

        internal abstract ViewInstance Create(Navigator navigator);

        internal abstract bool Matches(ViewInstance instance);
    }

    internal sealed class DependencyRequest<TViewModel, TArgs> : DependencyRequest where TViewModel : ViewModel
    {
        private readonly Route<TViewModel, TArgs, Unit> route;
        private readonly TArgs args;

        internal DependencyRequest(Route<TViewModel, TArgs, Unit> route, TArgs args, DependencyPlacement placement, bool isRequired)
        {
            this.route = route;
            this.args = args;
            Placement = placement;
            IsRequired = isRequired;
        }

        internal override bool IsRequired
        {
            get;
        }

        internal override Route Route => route;

        internal override DependencyPlacement Placement
        {
            get;
        }

        internal override ViewInstance Create(Navigator navigator) =>
                    navigator.NewInstance(route, args, null, explicitOwner: false);

        internal override bool Matches(ViewInstance instance) =>
                    instance is ViewInstance<TViewModel, TArgs, Unit> typed && route.ArgsEqual(typed.Args, args);
    }
}
