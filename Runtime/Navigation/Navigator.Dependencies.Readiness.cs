using System;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        /// <summary>
        /// 只读取直接依赖的就绪状态，不创建任务。表现重算按依赖优先顺序完成就绪，
        /// 因此直接依赖的结果已经包含它自己的必需依赖。
        /// </summary>
        internal bool AreDependenciesReady(ViewInstance parent, out Exception degradation)
        {
            degradation = null;
            if (parent.DependencyFailures != null)
            {
                foreach (var failure in parent.DependencyFailures.Values)
                {
                    degradation = failure.Error;
                    break;
                }
            }
            foreach (var handle in ownership.CaptureDependencies(parent.Handle))
            {
                if (!entries.TryGetValue(handle, out var dependency) || !dependency.IsActive ||
                    dependency.HasCloseStarted || retiringDependencies.Contains(handle) ||
                    !dependency.TryGetReadiness(out var readiness) || !readiness.IsReady)
                {
                    return false;
                }
                if (degradation == null && readiness.IsDegraded)
                {
                    degradation = readiness.Error;
                }
            }
            return true;
        }

        private void CompleteReadinessSnapshot(ViewInstance[] snapshot)
        {
            if (!ownership.HasDependencyEdges)
            {
                foreach (var instance in snapshot)
                {
                    instance.CompleteReadiness();
                }
                return;
            }
            // 关闭排序为父优先，反向求值即可使所有必需依赖先产生直接结果。
            // 通知仍使用原表现快照，避免在求值过程中执行外部回调。
            var parentsFirst = ownership.OrderCloseBatch(snapshot);
            for (var i = parentsFirst.Length - 1; i >= 0; --i)
            {
                parentsFirst[i].CompleteReadiness();
            }
        }
    }
}
