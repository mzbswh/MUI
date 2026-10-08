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
            return CaptureSnapshot(null, maxEntries, out _);
        }

        /// <summary>只采集指定宿主；Guid.Empty 对应没有宿主身份的责任，不表示全部宿主。</summary>
        public static IReadOnlyList<CleanupResponsibilitySnapshot> CaptureSnapshot(Guid hostId, int maxEntries = 256)
        {
            return CaptureSnapshot(hostId, maxEntries, out _);
        }

        /// <summary>指定宿主当前在途或失败责任的总数，不受诊断快照截断影响。</summary>
        public static int GetUnconfirmedCount(Guid hostId)
        {
            lock (gate)
            {
                var count = 0;
                foreach (var responsibility in responsibilities.Values)
                {
                    if (responsibility.HostId == hostId)
                    {
                        ++count;
                    }
                }
                return count;
            }
        }

        /// <summary>同一已退役页面只占一个清理名额；无页面身份的责任逐项保守计入。</summary>
        internal static int GetUnconfirmedOwnerCount(Guid hostId, ISet<long> excludedViews)
        {
            lock (gate)
            {
                if (responsibilities.Count == 0)
                {
                    return 0;
                }
                var views = new HashSet<long>();
                var withoutView = 0;
                foreach (var responsibility in responsibilities.Values)
                {
                    if (responsibility.HostId != hostId)
                    {
                        continue;
                    }
                    var viewId = responsibility.ViewId;
                    if (viewId == 0)
                    {
                        ++withoutView;
                    }
                    else if (excludedViews == null || !excludedViews.Contains(viewId))
                    {
                        views.Add(viewId);
                    }
                }
                return views.Count + withoutView;
            }
        }

        internal static IReadOnlyList<CleanupResponsibilitySnapshot> CaptureSnapshot(Guid? hostId,
            int maxEntries, out int totalCount)
        {
            if (maxEntries < 1 || maxEntries > 4096)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEntries));
            }

            List<CleanupResponsibility> selected;
            totalCount = 0;
            lock (gate)
            {
                selected = new List<CleanupResponsibility>(Math.Min(maxEntries, responsibilities.Count));
                foreach (var responsibility in responsibilities.Values)
                {
                    if (hostId.HasValue && responsibility.HostId != hostId.Value)
                    {
                        continue;
                    }

                    ++totalCount;
                    if (selected.Count < maxEntries)
                    {
                        selected.Add(responsibility);
                    }
                }
            }

            // 不在账本锁内取得责任锁，释放完成可能正在按相反顺序移除自身。
            var result = new CleanupResponsibilitySnapshot[selected.Count];
            for (var i = 0; i < selected.Count; ++i)
            {
                result[i] = selected[i].CaptureSnapshot();
            }

            return Array.AsReadOnly(result);
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

        /// <summary>
        /// 所属操作收尾时归还候选，并把未确认责任登记到其激活作用域。
        /// 责任属性失效也继续执行一次真实清理；不根据项目状态查询推断归还成功。
        /// </summary>
        internal static async ValueTask ReleaseAsync(IAsyncDisposable resource, string owner, LifetimeScope lifetime)
        {
            if (resource == null)
            {
                return;
            }

            var child = resource as LifetimeScope;
            CleanupResponsibility responsibility = null;
            Exception registrationFailure = null;
            if (child == null)
            {
                try
                {
                    responsibility = GetResponsibility(resource, owner);
                    if (responsibility == null)
                    {
                        throw new InvalidOperationException("Cleanup adapter returned no responsibility.");
                    }
                }
                catch (Exception error)
                {
                    registrationFailure = error;
                    // 登记失败不表示候选已经归还；回退仍持有对象和一次清理回调，不声明安全重试。
                    responsibility = adapters.GetValue(resource, value => new CleanupResponsibility(value.DisposeAsync, owner));
                }
            }

            Exception releaseFailure = null;
            try
            {
                if (registrationFailure == null)
                {
                    await ReleaseAsync(resource, owner);
                }
                else
                {
                    await responsibility.DisposeAsync();
                }
            }
            catch (Exception error)
            {
                releaseFailure = error;
            }

            var failure = registrationFailure == null ? releaseFailure : releaseFailure == null ? registrationFailure :
                new AggregateException("Candidate cleanup registration and release failed.", registrationFailure, releaseFailure);
            if (failure != null)
            {
                if (lifetime != null)
                {
                    lifetime.RecordCleanupFailureWithConfirmation(failure, child == null ?
                        (Func<bool>)(() => responsibility.CaptureSnapshot().State == CleanupResponsibilityState.Completed) :
                        () => child.IsCleanupConfirmed);
                }
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }

        internal static void Retain(CleanupResponsibility responsibility)
        {
            lock (gate)
            {
                responsibilities[responsibility.Id] = responsibility;
            }
        }

        internal static CleanupResponsibility GetResponsibility(IAsyncDisposable resource, string owner)
        {
            if (resource is CleanupResponsibility responsibility)
            {
                return responsibility;
            }
            if (resource is ICleanupResponsibilitySource source)
            {
                return source.CleanupResponsibility;
            }
            return adapters.GetValue(resource, value => new CleanupResponsibility(value.DisposeAsync, owner));
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
