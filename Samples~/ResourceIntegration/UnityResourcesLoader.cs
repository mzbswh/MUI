using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;
using UnityEngine;

namespace MUI.Samples.ResourceIntegration
{
    /// <summary>
    /// 在 Unity 主线程创建和调用，键为相对 Resources 的无扩展名路径。
    /// 释放凭证仅放弃托管持有权，不卸载全局共享的 Unity 资源。
    /// 项目在适合的加载边界安排 Resources.UnloadUnusedAssets。
    /// 原生请求无法中止，取消在请求完成后报告。
    /// </summary>
    public sealed class UnityResourcesLoader : IResourceLoader
    {
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly int requestCapacity;
        private int pendingRequests;

        public UnityResourcesLoader(int requestCapacity = 32)
        {
            if (requestCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(requestCapacity));
            }

            this.requestCapacity = requestCapacity;
        }

        public int PendingRequests
        {
            get
            {
                RequireThread();
                return pendingRequests;
            }
        }

        public ValueTask<IAcquiredResource<T>> LoadAsync<T>(string key, CancellationToken cancellationToken)
                    where T : class
        {
            Validate<T>(key);
            cancellationToken.ThrowIfCancellationRequested();
            RequireCapacity();
            ++pendingRequests;
            ResourceRequest request;
            try
            {
                request = UnityEngine.Resources.LoadAsync(key, typeof(T));
                if (request == null)
                {
                    throw new InvalidOperationException("Unity returned no resource request.");
                }
            }
            catch
            {
                --pendingRequests;
                throw;
            }

            var completion = new TaskCompletionSource<IAcquiredResource<T>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var finished = false;
            Action<AsyncOperation> handler = null;
            handler = operation =>
            {
                if (finished)
                {
                    return;
                }

                finished = true;
                request.completed -= handler;
                --pendingRequests;
                try
                {
                    RequireThread();
                    if (cancellationToken.IsCancellationRequested)
                    {
                        completion.TrySetCanceled(cancellationToken);
                    }
                    else
                    {
                        completion.TrySetResult(Claim<T>(key, request.asset));
                    }
                }
                catch (Exception failure)
                {
                    completion.TrySetException(failure);
                }
            };
            // 已完成请求可能在注册时立即回调；此保护同时覆盖
            // 该回调与显式完成检查，避免创建第二份持有权。
            request.completed += handler;
            if (request.isDone)
            {
                handler(request);
            }

            return new ValueTask<IAcquiredResource<T>>(completion.Task);
        }

        private static IAcquiredResource<T> Claim<T>(string key, UnityEngine.Object asset)
                    where T : class
        {
            if (asset == null || !(asset is T value))
            {
                throw new FileNotFoundException($"Resources asset '{key}' was not found as {typeof(T).FullName}.", key);
            }

            // AcquiredResource 释放时清空 Asset 和回调，不调用 Destroy/UnloadAsset：
            // Resources.Load 可能向无关联的所有者或加载器返回同一个原生对象。
            return new AcquiredResource<T>(value, released => default);
        }

        private void Validate<T>(string key)
                    where T : class
        {
            RequireThread();
            if (!typeof(UnityEngine.Object).IsAssignableFrom(typeof(T)))
            {
                throw new NotSupportedException("Unity Resources supports UnityEngine.Object asset types only.");
            }

            if (string.IsNullOrWhiteSpace(key) || key != key.Trim() || key.IndexOf('\\') >= 0)
            {
                throw new ArgumentException("Use a non-empty Resources-relative path with forward slashes.", nameof(key));
            }

            foreach (var part in key.Split('/'))
            {
                if (part.Length == 0 || part == "." || part == "..")
                {
                    throw new ArgumentException("Resource paths cannot contain empty or relative navigation segments.", nameof(key));
                }
            }
        }

        private void RequireCapacity()
        {
            if (pendingRequests >= requestCapacity)
            {
                throw new InvalidOperationException($"Unity Resources request capacity ({requestCapacity}) was reached.");
            }
        }

        private void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("Unity Resources loader must be used on the Unity thread where it was created.");
            }
        }
    }
}
