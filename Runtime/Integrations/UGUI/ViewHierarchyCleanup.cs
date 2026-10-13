using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>
    /// 原生节点拥有者捕获节点内各 View 的最终责任。扫描边界内的控件仍由各 View 独占清理，
    /// 此容器不重复扫描 Element；节点销毁必须等待全部视图责任确认。
    /// </summary>
    internal sealed class ViewHierarchyCleanup : IAsyncDisposable, ICleanupResponsibilitySource
    {
        private readonly LifetimeScope lifetime = new LifetimeScope();
        private Exception failure;
        private bool started;

        internal ViewHierarchyCleanup(GameObject root, CleanupResponsibility knownRoot = null)
        {
            UnityMainThread.Require();
            if (knownRoot != null)
            {
                lifetime.Own(knownRoot);
            }
            if (root != null)
            {
                foreach (var view in root.GetComponentsInChildren<View>(true))
                {
                    if (view == null)
                    {
                        continue;
                    }
                    var responsibility = view.CleanupResponsibility;
                    if (!ReferenceEquals(responsibility, knownRoot))
                    {
                        lifetime.Own(responsibility);
                    }
                }
            }
            CleanupResponsibility = new CleanupResponsibility(ReleaseAsync,
                "ViewHierarchy.Cleanup", true, Thread.CurrentThread.ManagedThreadId);
        }

        public CleanupResponsibility CleanupResponsibility
        {
            get;
        }

        internal bool IsCleanupConfirmed => CleanupResponsibility.CaptureSnapshot().State == CleanupResponsibilityState.Completed;

        public ValueTask DisposeAsync() => CleanupResponsibility.DisposeAsync();

        private async ValueTask ReleaseAsync()
        {
            if (!started)
            {
                started = true;
                try
                {
                    await lifetime.DisposeAsync();
                }
                catch (Exception error)
                {
                    failure = error;
                    throw;
                }
            }
            if (!lifetime.IsCleanupConfirmed)
            {
                throw failure ?? new InvalidOperationException("Native node still owns unconfirmed View cleanup.");
            }
            failure = null;
        }
    }
}
