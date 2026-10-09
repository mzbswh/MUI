using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MUI.UGUI
{
    public static partial class PrefabViewFactory
    {
        /// <summary>提供方结束后仍保留未确认回滚所在的挂载根；恢复只检查子节点是否实际归还。</summary>
        public static CleanupResponsibility CreateStagingCleanup(GameObject root, string owner, Action endAdmission = null)
        {
            UnityMainThread.Require();
            var started = false;
            var destructionRequested = false;
            Exception nativeFailure = null;
            return new CleanupResponsibility(async () =>
            {
                UnityMainThread.Require();
                if (!started)
                {
                    started = true;
                    try
                    {
                        endAdmission?.Invoke();
                        endAdmission = null;
                    }
                    catch (Exception error)
                    {
                        nativeFailure = error;
                    }
                }
                if (nativeFailure != null)
                {
                    ExceptionDispatchInfo.Capture(nativeFailure).Throw();
                }
                if (!destructionRequested)
                {
                    if (root != null && root.transform.childCount != 0)
                    {
                        throw new InvalidOperationException("Prefab staging is retained until all rollback instances are returned.");
                    }
                    try
                    {
                        if (root != null)
                        {
                            UnityEngine.Object.Destroy(root);
                        }
                        destructionRequested = true;
                    }
                    catch (Exception error)
                    {
                        nativeFailure = error;
                        throw;
                    }
                }
                while (root != null)
                {
                    await Task.Yield();
                    UnityMainThread.Require();
                }
            }, owner, true, Thread.CurrentThread.ManagedThreadId);
        }
    }
}
