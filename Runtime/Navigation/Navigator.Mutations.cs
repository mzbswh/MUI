using System;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        /// <summary>参数更新和模型换绑共用实例资格；各操作分别检查依赖关系是否允许变更。</summary>
        private bool CanMutateInstance(ViewInstance instance) => instance.IsActive && instance.ActivationCommitted &&
            !instance.EnterPending && !instance.HasCloseStarted && instance.CloseRequest == null &&
            !retiringDependencies.Contains(instance.Handle);

        /// <summary>换绑可保留父页面的依赖，但不能替换其他父页面正在共享的模型。</summary>
        internal void RequireRebindOwnershipCurrent(ViewInstance instance)
        {
            AssertThread();
            if (!entries.TryGetValue(instance.Handle, out var current) || !ReferenceEquals(current, instance) ||
                !CanMutateInstance(instance) || ownership.HasOwners(instance.Handle))
            {
                throw new OperationCanceledException("界面的共享拥有关系不允许继续换绑。");
            }
            foreach (var handle in ownership.CaptureDependencies(instance.Handle))
            {
                if (!entries.TryGetValue(handle, out var dependency) || !IsRetainedDependencyCurrent(instance, dependency))
                {
                    throw new OperationCanceledException("换绑期间必需依赖已经失效。");
                }
            }
        }

        /// <summary>参数更新与换绑共用依赖资格规则；只读取框架状态，不执行项目回调。</summary>
        private bool IsRetainedDependencyCurrent(ViewInstance parent, ViewInstance dependency) =>
            entries.TryGetValue(parent.Handle, out var currentParent) && ReferenceEquals(currentParent, parent) &&
            CanMutateInstance(parent) && !ownership.HasOwners(parent.Handle) &&
            entries.TryGetValue(dependency.Handle, out var current) && ReferenceEquals(current, dependency) &&
            ownership.HasDependency(parent.Handle, dependency.Handle) && dependency.IsActive &&
            !dependency.HasCloseStarted && dependency.CloseRequest == null && !dependency.IsUpdatingArgs &&
            !dependency.IsRebinding && !dependency.ActivationToken.IsCancellationRequested &&
            !retiringDependencies.Contains(dependency.Handle);
    }
}
