using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>
    /// 独立于宿主组件的未完成责任账本。只保存真实在途或失败责任，不保存已完成操作历史。
    /// 不自动重试或遗忘失败；诊断按调用方给出的上限采集，返回同一责任的稳定标识。
    /// </summary>
    public static class CleanupRegistry
    {
        private static readonly object gate = new object();
        private static readonly Dictionary<Guid, CleanupResponsibility> responsibilities = new Dictionary<Guid, CleanupResponsibility>();
        private static readonly ConditionalWeakTable<IAsyncDisposable, CleanupResponsibility> adapters = new ConditionalWeakTable<IAsyncDisposable, CleanupResponsibility>();

        public static int UnconfirmedCount
        {
            get
            {
                lock (gate)
                {
                    return responsibilities.Count;
                }
            }
        }

        public static IReadOnlyList<CleanupResponsibilitySnapshot> CaptureSnapshot(int maxEntries = 256)
        {
            if (maxEntries < 1 || maxEntries > 4096)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEntries));
            }

            List<CleanupResponsibility> selected;
            lock (gate)
            {
                selected = new List<CleanupResponsibility>(Math.Min(maxEntries, responsibilities.Count));
                foreach (var responsibility in responsibilities.Values)
                {
                    if (selected.Count == maxEntries)
                    {
                        break;
                    }

                    selected.Add(responsibility);
                }
            }

            // 不在账本锁内取得责任锁，释放完成可能正在按相反顺序移除自身。
            var result = new CleanupResponsibilitySnapshot[selected.Count];
            for (var i = 0; i < selected.Count; ++i)
            {
                result[i] = selected[i].CaptureSnapshot();
            }

            return result;
        }

        public static Task RetryAsync(Guid responsibilityId)
        {
            CleanupResponsibility responsibility;
            lock (gate)
            {
                if (!responsibilities.TryGetValue(responsibilityId, out responsibility))
                {
                    return Task.FromException(new InvalidOperationException("Cleanup responsibility was not found or is already confirmed complete."));
                }
            }

            return responsibility.RetryAsync();
        }

        /// <summary>归还外部凭证；未公开责任的适配器按不可安全重试处理，失败仍保留对象。</summary>
        public static ValueTask ReleaseAsync(IAsyncDisposable resource, string owner)
        {
            if (resource == null)
            {
                return default;
            }

            if (resource is ICleanupResponsibilitySource || resource is CleanupResponsibility || resource is LifetimeScope)
            {
                return resource.DisposeAsync();
            }

            return adapters.GetValue(resource, value => new CleanupResponsibility(value.DisposeAsync, owner)).DisposeAsync();
        }

        internal static void Retain(CleanupResponsibility responsibility)
        {
            lock (gate)
            {
                responsibilities[responsibility.Id] = responsibility;
            }
        }

        internal static void ConfirmCompleted(CleanupResponsibility responsibility)
        {
            lock (gate)
            {
                responsibilities.Remove(responsibility.Id);
            }
        }
    }
}
