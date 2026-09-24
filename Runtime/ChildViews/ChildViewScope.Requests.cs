using System;
using System.Collections.Generic;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewScope
    {
        // 每个自有句柄至多一个请求；数量受 Scope 当前实例数约束。
        private readonly HashSet<ChildViewHandle> synchronousCloseRequests = new HashSet<ChildViewHandle>();
        private bool pumpingCloseRequests;

        internal void RequestSynchronousClose(ChildViewHandle handle)
        {
            RequireActive();
            if (Mode != LifetimeMode.Synchronous || !handle.BelongsTo(this) || !handles.Contains(handle))
            {
                throw new InvalidOperationException("Synchronous close requests require an owned child handle.");
            }

            if (handle.IsExecuting || !handle.CanCloseSynchronously)
            {
                if (synchronousCloseRequests.Add(handle))
                {
                    RefreshTicks();
                }

                return;
            }

            synchronousCloseRequests.Remove(handle);
            CloseSynchronouslyAndReport(handle);
        }

        /// <summary>在命令及生命周期回调退出后同步派发关闭请求；Tick 也会调用。嵌套调用不重复派发。</summary>
        public void Pump()
        {
            RequireThread();
            if (Mode != LifetimeMode.Synchronous || pumpingCloseRequests)
            {
                return;
            }

            if (!IsActive)
            {
                synchronousCloseRequests.Clear();
                return;
            }

            if (synchronousCloseRequests.Count == 0 && tickHandles.Count == 0)
            {
                return;
            }

            pumpingCloseRequests = true;
            try
            {
                // 本轮只处理入口快照；回调新增的请求留给下一次显式派发。
                foreach (var handle in new List<ChildViewHandle>(synchronousCloseRequests))
                {
                    if (!IsActive)
                    {
                        synchronousCloseRequests.Clear();
                        break;
                    }

                    if (!synchronousCloseRequests.Contains(handle))
                    {
                        continue;
                    }

                    if (!handles.Contains(handle))
                    {
                        synchronousCloseRequests.Remove(handle);
                        continue;
                    }

                    if (handle.IsExecuting || !handle.CanCloseSynchronously)
                    {
                        continue;
                    }

                    synchronousCloseRequests.Remove(handle);
                    CloseSynchronouslyAndReport(handle);
                }

                // 请求活跃性通过 Tick 注册向祖先传播，但派发不受隐藏暂停策略限制。
                foreach (var handle in new List<ChildViewHandle>(tickHandles))
                {
                    if (!IsActive)
                    {
                        break;
                    }

                    if (!handles.Contains(handle))
                    {
                        continue;
                    }

                    try
                    {
                        handle.PumpChildRequests();
                    }
                    catch (Exception error)
                    {
                        UIErrors.Report(error);
                    }
                }
            }
            finally
            {
                pumpingCloseRequests = false;
                RefreshTicks();
            }
        }

        internal void CloseSynchronouslyAndReport(ChildViewHandle handle)
        {
            try
            {
                handle.Dispose();
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }
    }
}
