using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.UGUI
{
    public sealed partial class NestedViewElement
    {
        /// <summary>捕获已有借用的稳定责任；不查询项目对象，也不因再次借用或解除父引用而遗失旧失败。</summary>
        internal Func<bool> CaptureChildViewCleanupConfirmation()
        {
            childViewReleases.RemoveAll(release => release.CaptureSnapshot().State == CleanupResponsibilityState.Completed);
            var releases = childViewReleases.ToArray();
            return () =>
            {
                foreach (var release in releases)
                {
                    if (release.CaptureSnapshot().State != CleanupResponsibilityState.Completed)
                    {
                        return false;
                    }
                }

                return true;
            };
        }

        /// <summary>节点责任包含最终原生收尾；尚未请求节点收尾时只检查子视图凭证。</summary>
        internal Func<bool> CaptureNodeCleanupConfirmation()
        {
            var cleanup = nodeCleanup;
            return cleanup == null ? CaptureChildViewCleanupConfirmation() :
                () => cleanup.CaptureSnapshot().State == CleanupResponsibilityState.Completed;
        }

        /// <summary>
        /// 列表拥有的节点在子视图凭证归还后才执行最终清理。显式重试可重新检查依赖，
        /// 原生清理本身失败时保留节点与回调，不重复执行未知副作用。
        /// </summary>
        internal void ReleaseOwnedNode(Action release, string owner)
        {
            if (nodeCleanup == null)
            {
                var confirmed = CaptureChildViewCleanupConfirmation();
                var lifetime = parentLifetime;
                var elementCleanup = CleanupResponsibility;
                var views = new ViewHierarchyCleanup(gameObject);
                Exception nativeFailure = null;
                var failureRecorded = false;
                nodeCleanup = new CleanupResponsibility(() =>
                {
                    if (!confirmed())
                    {
                        throw new InvalidOperationException("Nested node is retained until its child View cleanup is confirmed.");
                    }

                    // 借用凭证归还只代表子激活已结束；销毁前还须确认包装控件和各子 View 的最终监听。
                    if (elementCleanup.CaptureSnapshot().State != CleanupResponsibilityState.Completed)
                    {
                        ObserveSynchronousCleanup(elementCleanup.DisposeAsync());
                    }
                    if (!views.IsCleanupConfirmed)
                    {
                        ObserveSynchronousCleanup(views.DisposeAsync());
                    }

                    if (nativeFailure != null)
                    {
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(nativeFailure).Throw();
                    }

                    try
                    {
                        release();
                    }
                    catch (Exception error)
                    {
                        nativeFailure = error;
                        throw;
                    }

                    return default;
                }, owner, error =>
                {
                    if (error != null && lifetime != null && !failureRecorded)
                    {
                        failureRecorded = true;
                        lifetime.RecordCleanupFailure(error, nodeCleanup);
                    }
                }, true, Thread.CurrentThread.ManagedThreadId);
            }

            // 此责任只执行框架的同步原生收尾；只读取已完成结果，禁止主线程等待未完成任务。
            ObserveSynchronousCleanup(nodeCleanup.DisposeAsync());
        }

        private static void ObserveSynchronousCleanup(ValueTask attempt)
        {
            if (!attempt.IsCompleted)
            {
                throw new InvalidOperationException("Native node cleanup must finish synchronously.");
            }

            attempt.GetAwaiter().GetResult();
        }

        private void RequireAvailableNode()
        {
            if (nodeCleanup != null)
            {
                throw new InvalidOperationException("A nested node scheduled for final cleanup cannot acquire new child content.");
            }
        }
    }
}
