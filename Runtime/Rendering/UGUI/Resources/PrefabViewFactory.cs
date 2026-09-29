using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using MUI.Resources;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>仅创建和清理已提供的 Prefab 界面实例；不加载或缓存资源。</summary>
    public static class PrefabViewFactory
    {
        public static async ValueTask WaitForFailedInstanceAsync(Exception failure)
        {
            UnityMainThread.Require();
            while (failure is SynchronousResourceLoadException rollback)
            {
                failure = rollback.LoadError;
            }

            if (failure is CreationFailure creation)
            {
                if (creation.Instance != null && !creation.DestructionRequested)
                {
                    // 销毁请求未成功时不能无限等待，也不能归还仍被原生实例使用的依赖。
                    throw new InvalidOperationException("Failed prefab instance was not scheduled for destruction.", creation);
                }

                while (creation.Instance != null)
                {
                    await Task.Yield();
                    UnityMainThread.Require();
                }
            }
        }

        public static async ValueTask WaitForReleasedInstanceAsync(GameObject instance, Exception failure)
        {
            UnityMainThread.Require();
            if (instance != null && failure != null &&
                (!(failure is ReleaseFailure release) || !release.DestructionRequested))
            {
                // 已知销毁失败或释放未进入工厂时保留依赖，不能等待一个尚未发出的销毁请求。
                ExceptionDispatchInfo.Capture(failure).Throw();
            }

            while (instance != null)
            {
                await Task.Yield();
                UnityMainThread.Require();
            }
        }

        public static SynchronousViewLease Create(GameObject prefab, Transform parent, Transform staging,
                    Action requireCurrent = null, Action<View> configureView = null)
        {
            UnityMainThread.Require();
            GameObject instance = null;
            View view = null;
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

                // 配置属于创建事务，在原生激活和绑定前完成；异常沿用统一回滚。
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
                // 初始化和原生回调可能使资源代际失效；交出凭证前再次校验。
                requireCurrent?.Invoke();
                if (instance == null || view == null || !view.IsAlive || parent == null ||
                    instance.transform.parent != parent || !instance.activeSelf)
                {
                    throw new InvalidOperationException("原生激活回调改变了界面实例的有效性或挂载状态。");
                }
                // 独立保留原生根对象；View 组件提前销毁后仍要负责释放整个预制体实例。
                return new SynchronousViewLease(view, released => Release(released, instance),
                    System.Threading.Thread.CurrentThread.ManagedThreadId);
            }
            catch (Exception failure)
            {
                var cleanup = ReleaseNativeInstance(view, instance, out var destructionRequested);
                if (cleanup != null)
                {
                    throw new SynchronousResourceLoadException(
                        new CreationFailure(failure, instance, destructionRequested), cleanup);
                }

                if (instance != null)
                {
                    throw new CreationFailure(failure, instance, destructionRequested);
                }

                throw;
            }
        }

        private static void Release(IView released, GameObject instance)
        {
            var view = (View)released;
            var cleanup = ReleaseNativeInstance(view, instance, out var destructionRequested);
            if (cleanup != null)
            {
                throw new ReleaseFailure(cleanup, destructionRequested);
            }
        }

        /// <summary>逐项尝试逻辑清理、隐藏与原生销毁，前一项失败不能跳过后一项。</summary>
        private static Exception ReleaseNativeInstance(View view, GameObject instance, out bool destructionRequested)
        {
            var errors = new List<Exception>();
            if (view != null)
            {
                try
                {
                    view.Dispose();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            if (instance != null)
            {
                try
                {
                    instance.SetActive(false);
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            destructionRequested = instance == null;
            if (instance != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(instance);
                    destructionRequested = true;
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            return errors.Count == 0 ? null : errors.Count == 1 ? errors[0]
                : new AggregateException("Prefab instance cleanup failed.", errors);
        }

        private sealed class ReleaseFailure : Exception
        {
            public ReleaseFailure(Exception cleanup, bool destructionRequested)
                            : base("Prefab instance cleanup failed.", cleanup)
            {
                DestructionRequested = destructionRequested;
            }

            public bool DestructionRequested
            {
                get;
            }
        }

        private sealed class CreationFailure : Exception
        {
            public CreationFailure(Exception cause, GameObject instance, bool destructionRequested) : base("Prefab construction failed.", cause)
            {
                Instance = instance;
                DestructionRequested = destructionRequested;
            }

            public GameObject Instance
            {
                get;
            }

            public bool DestructionRequested
            {
                get;
            }
        }
    }
}
