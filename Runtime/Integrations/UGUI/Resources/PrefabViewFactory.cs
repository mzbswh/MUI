using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>仅创建和清理已提供的 Prefab 界面实例；不加载或缓存资源。</summary>
    public static partial class PrefabViewFactory
    {
        /// <summary>等待失败创建的实际归还；首次回滚结果仍由 ResourceLoadException 保存。</summary>
        public static async ValueTask WaitForFailedInstanceAsync(Exception failure)
        {
            UnityMainThread.Require();
            if (failure is ResourceLoadException rollback)
            {
                if (rollback.InnerException is CreationFailure recovered && recovered.IsCleanupConfirmed)
                {
                    return;
                }
                await rollback.CleanupCompletion;
            }
        }

        public static async ValueTask WaitForReleasedInstanceAsync(GameObject instance, Exception failure)
        {
            UnityMainThread.Require();
            if (instance != null && failure != null &&
                (!(failure is ReleaseFailure release) || !release.DestructionRequested))
            {
                // 未发出销毁请求时保留依赖，不能等待一个不会发生的原生销毁。
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
            while (instance != null)
            {
                await Task.Yield();
                UnityMainThread.Require();
            }
        }

        public static AcquiredView Create(GameObject prefab, Transform parent, Transform staging,
            Action requireCurrent = null, Action<View> configureView = null)
        {
            UnityMainThread.Require();
            GameObject instance = null;
            View view = null;
            CleanupResponsibility rootCleanup = null;
            try
            {
                requireCurrent?.Invoke();
                if (prefab == null || parent == null || staging == null)
                {
                    throw new InvalidOperationException("Prefab creation requires live prefab and mounting roots.");
                }
                instance = UnityEngine.Object.Instantiate(prefab, staging, false);
                instance.name = prefab.name;
                instance.SetActive(false);
                view = instance.GetComponent<View>();
                if (view == null)
                {
                    throw new InvalidOperationException("Prefab has no root View.");
                }
                rootCleanup = view.CleanupResponsibility;
                if (configureView != null)
                {
                    view.ConfigureBeforeActivation(configureView);
                }
                requireCurrent?.Invoke();
                if (instance == null || view == null || !view.IsAlive || parent == null || staging == null)
                {
                    throw new InvalidOperationException("界面配置期间实例或挂载根已失效。");
                }
                if (instance.activeSelf || instance.transform.parent != staging)
                {
                    throw new InvalidOperationException("界面配置不能激活实例或改变临时挂载位置。");
                }
                view.Initialize();
                view.SetHostState(false, false);
                instance.transform.SetParent(parent, false);
                instance.SetActive(true);
                requireCurrent?.Invoke();
                if (instance == null || view == null || !view.IsAlive || parent == null ||
                    instance.transform.parent != parent || !instance.activeSelf)
                {
                    throw new InvalidOperationException("原生激活回调改变了界面实例的有效性或挂载状态。");
                }

                // 稳定根责任在交付前捕获；View 组件被外部销毁后仍负责整个原生实例。
                var cleanup = new InstanceCleanup(instance, rootCleanup);
                return new AcquiredView(view, _ => cleanup.DisposeAsync(),
                    Thread.CurrentThread.ManagedThreadId, supportsIdempotentRetry: true,
                    owner: "PrefabViewFactory.Instance", supportsCaching: true,
                    prepareForReuse: reused =>
                    {
                        requireCurrent?.Invoke();
                        var cached = (View)reused;
                        if (cached == null || !cached.IsAlive || parent == null || cached.transform.parent != parent)
                        {
                            throw new InvalidOperationException("Cached View no longer belongs to its mounting root.");
                        }
                        if (configureView != null)
                        {
                            cached.ConfigureBeforeActivation(configureView);
                        }
                        requireCurrent?.Invoke();
                    });
            }
            catch (Exception failure)
            {
                if (instance == null && rootCleanup == null)
                {
                    throw;
                }
                var cleanup = new InstanceCleanup(instance, rootCleanup);
                var rollback = new CleanupResponsibility(cleanup.DisposeAsync, "PrefabViewFactory.Rollback",
                    true, Thread.CurrentThread.ManagedThreadId);
                throw new ResourceLoadException(new CreationFailure(failure, rollback), rollback.DisposeAsync().AsTask());
            }
        }

        /// <summary>
        /// 分开记录视图归还、隐藏和销毁请求。恢复只检查视图责任，已尝试的未知原生步骤不重复。
        /// Destroy 成功请求后仍等待原生根实际消失，才能归还后端依赖。
        /// </summary>
        private sealed class InstanceCleanup
        {
            private readonly GameObject instance;
            private readonly CleanupResponsibility rootCleanup;
            private ViewHierarchyCleanup hierarchy;
            private bool captureAttempted;
            private Exception captureFailure;
            private bool hideAttempted;
            private Exception hideFailure;
            private bool destructionRequested;
            private Exception destructionFailure;

            internal InstanceCleanup(GameObject instance, CleanupResponsibility rootCleanup)
            {
                this.instance = instance;
                this.rootCleanup = rootCleanup;
            }

            internal async ValueTask DisposeAsync()
            {
                UnityMainThread.Require();
                if (!captureAttempted)
                {
                    captureAttempted = true;
                    try
                    {
                        hierarchy = new ViewHierarchyCleanup(instance, rootCleanup);
                    }
                    catch (Exception error)
                    {
                        captureFailure = error;
                    }
                }
                if (!hideAttempted)
                {
                    hideAttempted = true;
                    try
                    {
                        if (instance != null)
                        {
                            instance.SetActive(false);
                        }
                    }
                    catch (Exception error)
                    {
                        hideFailure = error;
                    }
                }

                try
                {
                    if (captureFailure != null)
                    {
                        ExceptionDispatchInfo.Capture(captureFailure).Throw();
                    }
                    if (!hierarchy.IsCleanupConfirmed)
                    {
                        await hierarchy.DisposeAsync();
                    }
                    if (hideFailure != null)
                    {
                        ExceptionDispatchInfo.Capture(hideFailure).Throw();
                    }
                    if (destructionFailure != null)
                    {
                        ExceptionDispatchInfo.Capture(destructionFailure).Throw();
                    }
                    if (!destructionRequested)
                    {
                        try
                        {
                            if (instance != null)
                            {
                                UnityEngine.Object.Destroy(instance);
                            }
                            destructionRequested = true;
                        }
                        catch (Exception error)
                        {
                            destructionFailure = error;
                            throw;
                        }
                    }
                    await WaitForReleasedInstanceAsync(instance, null);
                }
                catch (Exception error)
                {
                    throw new ReleaseFailure(error, destructionRequested);
                }
            }
        }

        private sealed class ReleaseFailure : Exception
        {
            internal ReleaseFailure(Exception cleanup, bool destructionRequested)
                : base("Prefab instance cleanup failed.", cleanup)
            {
                DestructionRequested = destructionRequested;
            }

            internal bool DestructionRequested
            {
                get;
            }
        }

        private sealed class CreationFailure : Exception
        {
            internal CreationFailure(Exception cause, CleanupResponsibility cleanup)
                : base("Prefab construction failed.", cause)
            {
                Cleanup = cleanup;
            }

            internal CleanupResponsibility Cleanup
            {
                get;
            }

            internal bool IsCleanupConfirmed => Cleanup.CaptureSnapshot().State == CleanupResponsibilityState.Completed;
        }
    }
}
