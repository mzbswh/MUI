using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        // 强制收敛占位使父页面释放关系时不反向等待正在关闭父页面的依赖。
        private readonly HashSet<ViewHandle> retiringDependencies = new HashSet<ViewHandle>();
        private readonly Dictionary<ViewHandle, Task<CloseOutcome>> dependencyClosures =
            new Dictionary<ViewHandle, Task<CloseOutcome>>();

        internal async ValueTask ReleaseDependenciesAsync(ViewInstance owner, List<Exception> errors)
        {
            foreach (var handle in ownership.ReleaseDependencies(owner.Handle))
            {
                if (retiringDependencies.Contains(handle) || !entries.TryGetValue(handle, out var dependency))
                {
                    continue;
                }
                try
                {
                    // 父清理等待真实释放，不以依赖的逻辑关闭超时冒充资源已经归还。
                    await BeginClose(dependency, DismissReason.Closed);
                    var result = dependency.CleanupCompletion == null
                        ? new CloseOutcome(CloseStatus.Blocked, cleanup: CleanupStatus.NotRequired)
                        : await dependency.CleanupCompletion;
                    AddDependencyCleanupError(result, errors);
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }
        }

        private static void AddDependencyCleanupError(CloseOutcome result, List<Exception> errors)
        {
            if (result.Error != null)
            {
                errors.Add(result.Error);
            }
            else if ((result.Status != CloseStatus.Closed && result.Status != CloseStatus.AlreadyClosed) ||
                result.Cleanup != CleanupStatus.Complete)
            {
                errors.Add(new InvalidOperationException($"依赖界面清理未完成：{result.Status}/{result.Cleanup}。"));
            }
        }

        private Task<CloseOutcome> CloseOwnedDependencyAsync(ViewInstance dependency, DismissReason reason,
            Action acceptResult, ViewInstance replacement)
        {
            var completion = new TaskCompletionSource<CloseOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            retiringDependencies.Add(dependency.Handle);
            dependencyClosures.Add(dependency.Handle, completion.Task);
            _ = FinishOwnedDependencyCloseAsync(dependency, reason, acceptResult, replacement, completion);
            return completion.Task;
        }

        private async Task FinishOwnedDependencyCloseAsync(ViewInstance dependency, DismissReason reason,
            Action acceptResult, ViewInstance replacement, TaskCompletionSource<CloseOutcome> completion)
        {
            try
            {
                DetachOptionalOwners(dependency, reason);
                var errors = new List<Exception>();
                foreach (var parent in CaptureDependentParents(dependency.Handle))
                {
                    if (!entries.ContainsKey(parent.Handle))
                    {
                        continue;
                    }
                    PropagateDependencyFailure(dependency, parent);
                    await BeginClose(parent, DismissReason.Forced);
                    if (parent.CleanupCompletion != null)
                    {
                        AddDependencyCleanupError(await parent.CleanupCompletion, errors);
                    }
                }
                if (ownership.HasOwners(dependency.Handle))
                {
                    completion.TrySetResult(new CloseOutcome(CloseStatus.Blocked, cleanup: CleanupStatus.NotRequired));
                    return;
                }
                foreach (var error in errors)
                {
                    dependency.RecordPreparationCleanupFailure(error);
                }
                completion.TrySetResult(await BeginCloseCore(dependency, reason, acceptResult, replacement));
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
                completion.TrySetResult(new CloseOutcome(CloseStatus.Failed, error, CleanupStatus.Failed));
            }
            finally
            {
                dependencyClosures.Remove(dependency.Handle);
                retiringDependencies.Remove(dependency.Handle);
            }
        }

        private static void PropagateDependencyFailure(ViewInstance dependency, ViewInstance parent)
        {
            if (dependency.Failure != null && parent.Failure == null && !parent.HasCloseStarted)
            {
                parent.SetFailure(new InvalidOperationException(
                    $"必需依赖 {dependency.Route.Key} 失败，父页面无法继续保持有效。", dependency.Failure));
            }
        }

        private ViewInstance[] CaptureDependentParents(ViewHandle dependency)
        {
            var visited = new HashSet<ViewHandle>();
            var pendingParents = new Stack<ViewHandle>();
            foreach (var owner in ownership.CaptureOwners(dependency))
            {
                if (ownership.IsRequiredDependency(owner, dependency))
                {
                    pendingParents.Push(owner);
                }
            }
            var result = new List<ViewInstance>();
            while (pendingParents.Count > 0)
            {
                var handle = pendingParents.Pop();
                if (!visited.Add(handle))
                {
                    continue;
                }
                if (entries.TryGetValue(handle, out var parent))
                {
                    result.Add(parent);
                    foreach (var ancestor in ownership.CaptureOwners(handle))
                    {
                        if (ownership.IsRequiredDependency(ancestor, handle))
                        {
                            pendingParents.Push(ancestor);
                        }
                    }
                }
            }
            result.Sort((left, right) => right.Order.CompareTo(left.Order));
            return ownership.OrderCloseBatch(result.ToArray());
        }
    }
}
