using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MUI.UGUI
{
    /// <summary>当前准备请求的状态；只描述默认资源键，不代表所有 UI 资源。</summary>
    public enum ResourcePreparationState
    {
        Loading,
        Applied,
        NotApplied,
        Cancelled,
        Failed
    }

    /// <summary>不可变诊断条目，不保留控件、加载任务或异常对象。</summary>
    public sealed class ResourcePreparationEntry
    {
        internal ResourcePreparationEntry(string target, ResourcePreparationState state)
        {
            Target = target;
            State = state;
        }

        public string Target
        {
            get;
        }

        public ResourcePreparationState State
        {
            get;
        }
    }

    /// <summary>主线程显式采集的准备账本，提交后条目清空，不作为历史资源清单。</summary>
    public sealed class ViewResourcePreparationSnapshot
    {
        internal ViewResourcePreparationSnapshot(bool enabled, bool hasContext, bool active,
                    bool committed, int total, int pending, int failed, int cancelled, int notApplied,
                    List<ResourcePreparationEntry> entries)
        {
            Enabled = enabled;
            HasContext = hasContext;
            ActivationActive = active;
            Committed = committed;
            TotalCount = total;
            PendingCount = pending;
            FailedCount = failed;
            CancelledCount = cancelled;
            NotAppliedCount = notApplied;
            Entries = new ReadOnlyCollection<ResourcePreparationEntry>(entries);
        }

        public bool Enabled
        {
            get;
        }

        public bool HasContext
        {
            get;
        }

        public bool ActivationActive
        {
            get;
        }

        public bool Committed
        {
            get;
        }

        public int TotalCount
        {
            get;
        }

        public int PendingCount
        {
            get;
        }

        public int FailedCount
        {
            get;
        }

        public int CancelledCount
        {
            get;
        }

        public int NotAppliedCount
        {
            get;
        }

        public IReadOnlyList<ResourcePreparationEntry> Entries
        {
            get;
        }

        public bool IsTruncated => Entries.Count < TotalCount;
    }
}
