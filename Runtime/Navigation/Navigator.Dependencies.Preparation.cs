using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        /// <summary>当前实例直接持有的依赖快照；终态或未知句柄返回空集合。</summary>
        public IReadOnlyList<ViewHandle> GetDependencies(ViewHandle handle)
        {
            AssertThread();
            return entries.ContainsKey(handle) ? ownership.CaptureDependencies(handle) : Array.Empty<ViewHandle>();
        }

        /// <summary>当前实例的父拥有者快照，不包括显式打开关系。</summary>
        public IReadOnlyList<ViewHandle> GetOwners(ViewHandle handle)
        {
            AssertThread();
            return entries.ContainsKey(handle) ? ownership.CaptureOwners(handle) : Array.Empty<ViewHandle>();
        }

        private async ValueTask PrepareDependenciesAsync(ViewInstance parent, int depth, DependencyPlacement placement, CancellationToken token)
        {
            RequireDependencyDepth(depth);
            var declarations = parent.Route.DependencyDescriptors;
            for (var i = 0; i < declarations.Count; ++i)
            {
                var descriptor = declarations[i];
                if (descriptor.Placement != placement || IsDependencyAlreadyDegraded(parent, descriptor))
                {
                    continue;
                }
                token.ThrowIfCancellationRequested();
                parent.RequirePreparationCurrent();
                ViewInstance dependency = null;
                var acquired = false;
                try
                {
                    var request = parent.ResolveDependency(i);
                    dependency = AcquireDependency(parent, request, out var created, out acquired);
                    if (created)
                    {
                        await PrepareCandidateAsync(dependency, token, depth + 1);
                    }
                }
                catch (Exception error) when (!token.IsCancellationRequested &&
                    CanContinueAfterOptionalExit(parent, descriptor, dependency, error))
                {
                    // 显式保存异常，兼容 Unity 2022.3 编译器对嵌套异步 catch 的局部变量处理。
                    var preparationFailure = error;
                    try
                    {
                        await WaitForOptionalExitCleanupAsync(dependency);
                    }
                    catch (Exception cleanup)
                    {
                        parent.RecordPreparationCleanupFailure(cleanup);
                        throw new AggregateException("可选依赖准备中退出，资源清理失败。", preparationFailure, cleanup);
                    }
                    token.ThrowIfCancellationRequested();
                    parent.RequirePreparationCurrent();
                }
                catch (Exception error) when (!token.IsCancellationRequested &&
                    !(dependency == null && error is ResourceLoadException) &&
                    CanDegradeDependencyPreparation(parent, descriptor, error, acquired))
                {
                    // 跨 await 的诊断使用普通局部变量，不直接捕获异常过滤器变量。
                    var preparationFailure = error;
                    try
                    {
                        await RollbackDependencyAsync(parent, dependency, acquired);
                    }
                    catch (Exception cleanup)
                    {
                        parent.RecordPreparationCleanupFailure(cleanup);
                        throw new AggregateException("可选依赖准备与回滚均失败。", preparationFailure, cleanup);
                    }
                    token.ThrowIfCancellationRequested();
                    parent.RequirePreparationCurrent();
                    RecordDependencyDegradation(parent, descriptor.Target, dependency == null ? default : dependency.Handle,
                        DependencyFailureStage.Preparation, preparationFailure);
                }
            }
        }

        private static bool IsDependencyAlreadyDegraded(ViewInstance parent, RouteDependencyDescriptor descriptor) =>
            !descriptor.IsRequired && parent.DependencyFailures != null && parent.DependencyFailures.ContainsKey(descriptor.Target);

        private ViewInstance AcquireDependency(ViewInstance parent, DependencyRequest request, out bool created, out bool acquired)
        {
            created = false;
            acquired = false;
            Register(request.Route);
            ViewInstance found = null;
            foreach (var entry in entries.Values)
            {
                if (ReferenceEquals(entry.Route, request.Route) && OccupiesInstanceSlot(entry))
                {
                    found = entry;
                    break;
                }
            }
            if (found != null)
            {
                ownership.ValidateAcquisition(parent.Handle, found.Handle, request.Placement, request.IsRequired);
                // 比较器属于项目代码，不在字典迭代中执行，返回后再次核对实例身份。
                bool matches;
                using (EnterCallback(parent))
                {
                    matches = request.Matches(found);
                }

                parent.RequirePreparationCurrent();
                if (!entries.TryGetValue(found.Handle, out var current) || !ReferenceEquals(current, found) ||
                    found.HasCloseStarted || found.CloseRequest != null || found.IsUpdatingArgs || found.IsRebinding ||
                    retiringDependencies.Contains(found.Handle) ||
                    found.ActivationToken.IsCancellationRequested ||
                    (found.State != ViewState.Open && !(found.State == ViewState.Opening && found.PreparationComplete)))
                {
                    throw new NavigationPreparationRejectedException(OpenRejection.Busy,
                        $"共享依赖 {request.Route.Key} 尚未准备完成或已经退出。");
                }
                if (!matches)
                {
                    throw new NavigationPreparationRejectedException(OpenRejection.ConflictingData,
                        $"共享依赖 {request.Route.Key} 的参数与现有实例冲突。");
                }
                acquired = ownership.Acquire(parent.Handle, found.Handle, request.Placement, request.IsRequired);
                created = false;
                return found;
            }
            if (!HasCleanupCapacity)
            {
                throw new NavigationPreparationRejectedException(OpenRejection.CleanupCapacity,
                    "清理隔离容量不足，不能创建新的共享依赖。");
            }
            ownership.ValidateNewDependency(parent.Handle);
            var dependency = request.Create(this);
            dependency.PreparationOperationId = parent.PreparationOperationId;
            dependency.PreparationTraceSession = parent.PreparationTraceSession;
            // 工厂只构造内部候选，不运行项目回调；取得所有权后才开始任何可失败的准备。
            acquired = ownership.Acquire(parent.Handle, dependency.Handle, request.Placement, request.IsRequired);
            created = true;
            return dependency;
        }

        private static void RequireDependencyDepth(int depth)
        {
            if (depth > 64)
            {
                throw new NavigationPreparationRejectedException(OpenRejection.DependencyLimit,
                    "共享依赖嵌套超过 64 层，已停止继续准备。");
            }
        }

        private void RequireDependenciesCurrent(ViewInstance parent)
        {
            var visited = new HashSet<ViewHandle>();
            var pendingDependencies = new Stack<ViewHandle>(ownership.CaptureDependencies(parent.Handle));
            while (pendingDependencies.Count > 0)
            {
                var handle = pendingDependencies.Pop();
                if (!visited.Add(handle))
                {
                    continue;
                }
                if (!entries.TryGetValue(handle, out var dependency) || dependency.HasCloseStarted ||
                    retiringDependencies.Contains(handle) || dependency.ActivationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException("必需依赖在父页面提交前已失效。");
                }
                if (dependency.State == ViewState.Opening && dependency.PreparationComplete)
                {
                    dependency.RequirePreparationCurrent();
                }
                else if (!dependency.IsActive)
                {
                    throw new OperationCanceledException("必需依赖尚未准备完成。");
                }
                foreach (var nested in ownership.CaptureDependencies(handle))
                {
                    pendingDependencies.Push(nested);
                }
            }
            if (parent.State != ViewState.Opening || parent.HasCloseStarted ||
                parent.ActivationToken.IsCancellationRequested || !entries.ContainsKey(parent.Handle))
            {
                throw new OperationCanceledException("父页面在依赖校验期间退出。");
            }
            foreach (var handle in visited)
            {
                if (!entries.TryGetValue(handle, out var dependency) || dependency.HasCloseStarted ||
                    retiringDependencies.Contains(handle) || dependency.ActivationToken.IsCancellationRequested ||
                    (!dependency.IsActive && !(dependency.State == ViewState.Opening && dependency.PreparationComplete)))
                {
                    throw new OperationCanceledException("必需依赖在版本校验期间退出。");
                }
            }
        }

        private void CommitDependencies(ViewInstance parent, DependencyPlacement placement)
        {
            foreach (var handle in ownership.CaptureDependencies(parent.Handle, placement))
            {
                var dependency = entries[handle];
                if (dependency.State == ViewState.Opening)
                {
                    CommitOpen(dependency);
                }
            }
        }

        private void ActivateDependencies(ViewInstance parent, DependencyPlacement placement)
        {
            foreach (var handle in ownership.CaptureDependencies(parent.Handle, placement))
            {
                // 前一项的回调可能强制关闭本项；可选关系会在退出流程中撤销并记录降级。
                if (!parent.IsActive)
                {
                    return;
                }
                if (!ownership.HasDependency(parent.Handle, handle))
                {
                    continue;
                }
                var required = ownership.IsRequiredDependency(parent.Handle, handle);
                if (!entries.TryGetValue(handle, out var dependency) || !dependency.IsActive)
                {
                    throw new OperationCanceledException("依赖在激活前退出，但其拥有关系尚未收敛。");
                }
                try
                {
                    if (!dependency.ActivationCommitted)
                    {
                        Activate(dependency);
                    }
                    if (!dependency.IsActive || !dependency.ActivationCommitted)
                    {
                        throw new InvalidOperationException("依赖激活失败。");
                    }
                }
                catch (Exception error) when (!required && parent.IsActive &&
                    !parent.ActivationToken.IsCancellationRequested)
                {
                    if (entries.ContainsKey(dependency.Handle) && !dependency.HasCloseStarted)
                    {
                        // 激活已进入提交阶段：失败实例必须整体退出，不能继续服务其他拥有者。
                        dependency.SetFailure(error);
                        CloseAfterFailure(dependency, DismissReason.Forced);
                    }
                    if (ownership.HasDependency(parent.Handle, handle))
                    {
                        // 同步实例仍忙碌或退出未能提交时，不能宣称依赖已经安全降级。
                        throw;
                    }
                    RecordDependencyDegradation(parent, dependency.Route, handle, DependencyFailureStage.RuntimeExit, error);
                }
            }
        }
    }
}
