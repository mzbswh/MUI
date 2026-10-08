using System.Collections.Generic;
using System.Threading.Tasks;

namespace MUI.UGUI
{
    internal sealed partial class ViewResourceContext
    {
        /// <summary>仅读取当前任务状态，不等待、不调用加载器、不观察项目异常的属性。</summary>
        internal ViewResourcePreparationSnapshot CapturePreparationSnapshot(int maxEntries)
        {
            var entries = new List<ResourcePreparationEntry>();
            var pending = 0;
            var failed = 0;
            var cancelled = 0;
            var notApplied = 0;
            if (preparations != null)
            {
                foreach (var preparation in preparations.Values)
                {
                    var operation = preparation.Operation;
                    var status = operation.Status;
                    ResourcePreparationState state;
                    if (status == TaskStatus.Faulted)
                    {
                        ++failed;
                        state = ResourcePreparationState.Failed;
                    }
                    else if (status == TaskStatus.Canceled)
                    {
                        ++cancelled;
                        state = ResourcePreparationState.Cancelled;
                    }
                    else if (status != TaskStatus.RanToCompletion)
                    {
                        ++pending;
                        state = ResourcePreparationState.Loading;
                    }
                    else if (!operation.Result)
                    {
                        ++notApplied;
                        state = ResourcePreparationState.NotApplied;
                    }
                    else
                    {
                        state = ResourcePreparationState.Applied;
                    }

                    if (entries.Count < maxEntries)
                    {
                        entries.Add(new ResourcePreparationEntry(preparation.Name, state));
                    }
                }
            }

            return new ViewResourcePreparationSnapshot(waitForSources, true, !released && !Scope.IsEnded,
                committed, preparations == null ? 0 : preparations.Count,
                pending, failed, cancelled, notApplied, entries);
        }
    }
}
