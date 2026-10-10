using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        /// <summary>当前父页面的可选依赖降级快照，每个目标最多保留首个原因；终态或未知句柄返回空集合。</summary>
        public IReadOnlyList<DependencyFailure> GetDependencyFailures(ViewHandle handle)
        {
            AssertThread();
            if (!entries.TryGetValue(handle, out var parent) || parent.DependencyFailures == null)
            {
                return Array.Empty<DependencyFailure>();
            }
            return new List<DependencyFailure>(parent.DependencyFailures.Values).AsReadOnly();
        }

        private void RecordDependencyDegradation(ViewInstance parent, Route target, ViewHandle dependency,
            DependencyFailureStage stage, Exception error)
        {
            if (parent.DependencyFailures == null)
            {
                parent.DependencyFailures = new Dictionary<Route, DependencyFailure>();
            }
            if (parent.DependencyFailures.ContainsKey(target))
            {
                return;
            }
            // 一个父页面的声明最多 64 项；同一目标不重复追加异常或事件。
            var failure = new DependencyFailure(target, dependency, stage, error);
            parent.DependencyFailures.Add(target, failure);
            parent.CommitVersion = ++commitVersion;
            QueueLifecycleEvent(parent, NavigationEventKind.DependencyDegraded, dependencyFailure: failure);
        }

        private bool CanDegradeDependencyPreparation(ViewInstance parent, RouteDependencyDescriptor descriptor,
            Exception error, bool acquired)
        {
            if (descriptor.IsRequired || parent.HasCloseStarted || parent.ActivationToken.IsCancellationRequested ||
                parent.State != ViewState.Opening || ContainsNonDegradableError(error))
            {
                return false;
            }
            if (!acquired)
            {
                foreach (var handle in ownership.CaptureDependencies(parent.Handle))
                {
                    if (entries.TryGetValue(handle, out var existing) && ReferenceEquals(existing.Route, descriptor.Target))
                    {
                        // 同一声明重复取得时，不能把新参数的失败当作已经持有的目标缺席。
                        // 此次没有新关系可以回滚，交给父事务整体拒绝并释放它实际取得的关系。
                        return false;
                    }
                }
            }
            if (error is NavigationPreparationRejectedException rejection)
            {
                return IsRecoverableDependencyRejection(rejection.Rejection);
            }
            return true;
        }

        private static bool IsRecoverableDependencyRejection(OpenRejection rejection) =>
            rejection == OpenRejection.ConflictingData || rejection == OpenRejection.Busy ||
            rejection == OpenRejection.DependencyMissing || rejection == OpenRejection.RenderOrderCapacity;

        private static bool ContainsNonDegradableError(Exception error, bool allowCancellation = false)
        {
            // 聚合的后端失败也可能包含取消；不把取消误当成可以忽略的资源失败。
            var pendingErrors = new Stack<Exception>();
            var visited = new HashSet<Exception>();
            pendingErrors.Push(error);
            while (pendingErrors.Count != 0)
            {
                var current = pendingErrors.Pop();
                if (current == null || !visited.Add(current))
                {
                    continue;
                }
                if ((!allowCancellation && current is OperationCanceledException) || (current is ResourceLoadException resourceFailure && !resourceFailure.CleanupCompletion.IsCompletedSuccessfully) ||
                    (current is NavigationPreparationRejectedException rejection && !IsRecoverableDependencyRejection(rejection.Rejection)))
                {
                    return true;
                }
                if (current is AggregateException aggregate)
                {
                    foreach (var inner in aggregate.InnerExceptions)
                    {
                        pendingErrors.Push(inner);
                    }
                }
                else
                {
                    pendingErrors.Push(current.InnerException);
                }
            }
            return false;
        }

        /// <summary>
        /// 只有退出协调器已经明确撤销可选关系时，才能将依赖自身取消视为退出；
        /// 父请求取消仍由调用方检查，配置与资源回滚故障也不能因此被忽略。
        /// </summary>
        private bool CanContinueAfterOptionalExit(ViewInstance parent, RouteDependencyDescriptor descriptor,
            ViewInstance dependency, Exception error)
        {
            if (descriptor.IsRequired || dependency == null || IsShutdown || parent.State != ViewState.Opening ||
                parent.HasCloseStarted || parent.ActivationToken.IsCancellationRequested ||
                parent.DependencyFailures == null ||
                !parent.DependencyFailures.TryGetValue(descriptor.Target, out var failure) ||
                failure.Stage != DependencyFailureStage.RuntimeExit || failure.Dependency != dependency.Handle ||
                ownership.HasDependency(parent.Handle, dependency.Handle) ||
                ContainsNonDegradableError(error, allowCancellation: true))
            {
                return false;
            }
            if (entries.ContainsKey(dependency.Handle))
            {
                // 可选直连之外，父页面还可能经必需链间接依赖同一目标。
                // 此时退出协调器仍须关闭父页面，准备不能反过来等待整个父链关闭。
                foreach (var requiredParent in CaptureDependentParents(dependency.Handle))
                {
                    if (ReferenceEquals(requiredParent, parent))
                    {
                        return false;
                    }
                }
            }
            return dependency.HasCloseStarted || dependencyClosures.ContainsKey(dependency.Handle) ||
                dependency.CompletedCloseOutcome.HasValue;
        }

        private async ValueTask WaitForOptionalExitCleanupAsync(ViewInstance dependency)
        {
            CloseOutcome outcome;
            if (dependency.CompletedCloseOutcome.HasValue)
            {
                outcome = dependency.CompletedCloseOutcome.Value;
            }
            else if (dependencyClosures.TryGetValue(dependency.Handle, out var closing))
            {
                outcome = await WaitForActualCleanupAsync(dependency, closing);
            }
            else if (dependency.CleanupCompletion != null)
            {
                outcome = await dependency.CleanupCompletion;
            }
            else
            {
                throw new InvalidOperationException("可选依赖退出缺少清理归属，不能继续准备父页面。");
            }
            RequireOptionalExitCleanupComplete(outcome);
        }

        private static void RequireOptionalExitCleanupComplete(CloseOutcome outcome)
        {
            // 故障退出可能报告 Failed，但操作失败与资源清理失败是两个维度。
            // 只要已经终结并确认物理清理完成，操作故障由已有降级事件呈现。
            if (outcome.Cleanup == CleanupStatus.Complete &&
                (outcome.Status == CloseStatus.Closed || outcome.Status == CloseStatus.AlreadyClosed ||
                    outcome.Status == CloseStatus.Failed))
            {
                return;
            }
            throw new InvalidOperationException("可选依赖退出后资源未能安全释放，不能继续提交父页面。", outcome.Error);
        }

        private async ValueTask RollbackDependencyAsync(ViewInstance parent, ViewInstance dependency, bool acquired)
        {
            if (!acquired || dependency == null)
            {
                return;
            }
            if (ownership.ReleaseDependency(parent.Handle, dependency.Handle, out var unowned) && unowned)
            {
                // 逻辑关闭超时不是回滚完成，继续等待真实清理；取消不能遗弃已经取得的资源。
                RequireDependencyRollbackComplete(await BeginCloseForCleanup(dependency, DismissReason.OpenCancelled));
            }
        }

        private static void RequireDependencyRollbackComplete(CloseOutcome outcome)
        {
            if ((outcome.Status == CloseStatus.Closed || outcome.Status == CloseStatus.AlreadyClosed) &&
                outcome.Cleanup == CleanupStatus.Complete && outcome.Error == null)
            {
                return;
            }
            var error = outcome.Error ?? new InvalidOperationException($"可选依赖回滚未完成：{outcome.Status}/{outcome.Cleanup}。");
            throw new InvalidOperationException("可选依赖未能安全回滚，不能继续提交父页面。", error);
        }

        /// <summary>目标已经决定退出时，撤销可选父关系；必需关系留给父链关闭流程。</summary>
        private void DetachOptionalOwners(ViewInstance dependency, DismissReason reason)
        {
            foreach (var handle in ownership.CaptureOwners(dependency.Handle))
            {
                if (ownership.IsRequiredDependency(handle, dependency.Handle))
                {
                    continue;
                }
                ownership.ReleaseDependency(handle, dependency.Handle, out _);
                if (entries.TryGetValue(handle, out var parent) && !parent.HasCloseStarted)
                {
                    RecordDependencyDegradation(parent, dependency.Route, dependency.Handle,
                        DependencyFailureStage.RuntimeExit, dependency.Failure ??
                        new InvalidOperationException($"可选依赖 {dependency.Route.Key} 已退出：{reason}。"));
                }
            }
        }
    }
}
