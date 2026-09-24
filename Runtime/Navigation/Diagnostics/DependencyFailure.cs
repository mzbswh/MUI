using System;

namespace MUI.Navigation
{
    /// <summary>可选依赖降级发生的阶段。</summary>
    public enum DependencyFailureStage
    {
        Preparation,
        RuntimeExit
    }

    /// <summary>可选依赖的降级快照；不包含参数、View 或 ViewModel 引用。</summary>
    public readonly struct DependencyFailure
    {
        internal DependencyFailure(Route target, ViewHandle dependency, DependencyFailureStage stage, Exception error)
        {
            Target = target;
            Dependency = dependency;
            Stage = stage;
            Error = error;
        }

        public Route Target
        {
            get;
        }

        /// <summary>未创建实例时为无效句柄；只用于诊断，不表示实例仍然存活。</summary>
        public ViewHandle Dependency
        {
            get;
        }

        public DependencyFailureStage Stage
        {
            get;
        }

        public Exception Error
        {
            get;
        }
    }
}
