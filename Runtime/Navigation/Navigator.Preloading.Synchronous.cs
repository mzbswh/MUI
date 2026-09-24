using System;
using System.Collections.Generic;
using MUI.Resources;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private readonly Dictionary<ViewResource, ISynchronousPreloadLease> synchronousPreloads =
            new Dictionary<ViewResource, ISynchronousPreloadLease>();
        private object synchronousPreloadVersion;
        private bool clearingSynchronousPreloads;

        /// <summary>直接加载并持有资源；准备、驻留及释放失败均计入统一预加载容量。</summary>
        private PreloadOutcome PreloadUntraced(Route route)
        {
            RequireSynchronousNavigation();
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }
            if (IsShutdown)
            {
                return new PreloadOutcome(PreloadStatus.HostClosed);
            }
            if (IsReentrant || HasCloseEvaluation || clearingSynchronousPreloads)
            {
                return new PreloadOutcome(PreloadStatus.Reentrant);
            }
            if (IsClearingInactiveContent)
            {
                return new PreloadOutcome(PreloadStatus.InactiveContentClearing);
            }
            if (!(synchronousProvider is ISynchronousPreloadViewProvider preloader))
            {
                return new PreloadOutcome(PreloadStatus.Unsupported);
            }

            Register(route);
            ISynchronousPreloadLease lease = null;
            var reserved = false;
            try
            {
                RefreshSynchronousPreloadVersion();
                if (synchronousPreloads.ContainsKey(route.Resource))
                {
                    return new PreloadOutcome(PreloadStatus.Ready, reusedReservation: true);
                }
                if (preloadReservations >= preloadCapacity)
                {
                    return new PreloadOutcome(PreloadStatus.CapacityExceeded);
                }

                ++preloadReservations;
                reserved = true;
                using (EnterCallback(null))
                {
                    lease = preloader.Preload(route.Resource);
                    if (lease == null || !route.Resource.Equals(lease.Resource))
                    {
                        throw new InvalidOperationException("Synchronous provider returned an invalid preload lease.");
                    }
                }
                if (!ReferenceEquals(synchronousPreloadVersion, CaptureProviderVersion()))
                {
                    ReleaseRejectedPreload(ref lease, ref reserved);
                    return new PreloadOutcome(PreloadStatus.Superseded);
                }

                synchronousPreloads.Add(route.Resource, lease);
                lease = null;
                reserved = false;
                return new PreloadOutcome(PreloadStatus.Ready);
            }
            catch (Exception failure)
            {
                if (reserved && lease == null &&
                    (failure is SynchronousResourceLoadException || failure is ResourceLoadException))
                {
                    // 提供方未交出凭证且明确报告回滚失败；没有可再释放的对象，也不能归还占用。
                    reserved = false;
                    preloadCleanupErrors.Add(failure);
                }
                try
                {
                    ReleaseRejectedPreload(ref lease, ref reserved);
                }
                catch (Exception cleanup)
                {
                    failure = new AggregateException("Synchronous preload and cleanup failed.", failure, cleanup);
                }
                return new PreloadOutcome(PreloadStatus.Failed, failure);
            }
        }

        private void ReleaseRejectedPreload(ref ISynchronousPreloadLease lease, ref bool reserved)
        {
            var saved = lease;
            lease = null;
            var releaseReservation = reserved;
            reserved = false;
            if (!releaseReservation)
            {
                return;
            }
            try
            {
                using (EnterCallback(null))
                {
                    if (saved != null)
                    {
                        saved.Dispose();
                    }
                }
                --preloadReservations;
            }
            catch (Exception error)
            {
                // 释放失败不重试、不返还额度；退出仍能报告原始失败。
                preloadCleanupErrors.Add(error);
                throw;
            }
        }

        private void RefreshSynchronousPreloadVersion()
        {
            if (IsShutdown)
            {
                return;
            }
            var version = CaptureProviderVersion();
            if (synchronousPreloadVersion != null && !ReferenceEquals(version, synchronousPreloadVersion))
            {
                ClearSynchronousPreloadsCore();
                // 销毁回调可能再次更换提供方代际，不能采用回调前的令牌。
                version = CaptureProviderVersion();
            }
            synchronousPreloadVersion = version;
        }

        /// <summary>清空目录后逐项同步释放，不销毁活动页面持有的独立资源。</summary>
        public void ClearPreloads()
        {
            RequireSynchronousNavigation();
            if (IsReentrant || HasCloseEvaluation || clearingSynchronousPreloads)
            {
                throw new InvalidOperationException("Cannot clear preloads from navigation callbacks.");
            }
            ClearSynchronousPreloadsCore();
        }

        private void ClearSynchronousPreloadsCore()
        {
            clearingSynchronousPreloads = true;
            var errors = new List<Exception>();
            var saved = new List<ISynchronousPreloadLease>(synchronousPreloads.Values);
            synchronousPreloads.Clear();
            synchronousPreloadVersion = null;
            try
            {
                for (var index = saved.Count - 1; index >= 0; --index)
                {
                    var lease = saved[index];
                    var reserved = true;
                    try
                    {
                        ReleaseRejectedPreload(ref lease, ref reserved);
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                    }
                }
            }
            finally
            {
                clearingSynchronousPreloads = false;
            }
            if (errors.Count != 0)
            {
                throw new AggregateException("Synchronous preload cleanup failed.", errors);
            }
        }
    }
}
