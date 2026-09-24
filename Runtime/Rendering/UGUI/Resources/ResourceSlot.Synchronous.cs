using System;
using System.Threading;
using MUI.Resources;

namespace MUI.UGUI
{
    internal sealed partial class ResourceSlot<T> where T : class
    {
        private ISynchronousResourceLoader synchronousLoader;
        private ISynchronousResourceLease<T> synchronousCurrent;
        private ISynchronousResourceLease<T> synchronousUncertainCandidate;
        private bool synchronousOperating;

        private ResourceSlot(ISynchronousResourceLoader loader, Action<T> assign,
                    CancellationToken ownerToken, LifetimeMode mode)
        {
            synchronousLoader = loader ?? throw new ArgumentNullException(nameof(loader));
            this.assign = assign ?? throw new ArgumentNullException(nameof(assign));
            this.ownerToken = ownerToken;
            lifetime = new Lifetime(mode);
            lifetime.OnDispose(ReleaseSynchronousCurrent);
        }

        /// <summary>资源槽创建时确定执行模式，不随请求切换。</summary>
        public LifetimeMode Mode => lifetime.Mode;

        public bool CanDisposeSynchronously
        {
            get
            {
                RequireThread();
                return Mode == LifetimeMode.Synchronous && !assigning && !synchronousOperating &&
                    lifetime.CanDisposeSynchronously;
            }
        }

        /// <summary>创建只依赖同步加载器的资源槽；将返回槽登记到所属 Lifetime。</summary>
        public static ResourceSlot<T> CreateSynchronous(ISynchronousResourceLoader loader,
            Action<T> assign, CancellationToken ownerToken = default) =>
            new ResourceSlot<T>(loader, assign, ownerToken, LifetimeMode.Synchronous);

        /// <summary>
        /// 同步加载并赋值后归还旧资源。加载或赋值失败保留原显示；
        /// 旧资源归还失败不撤销已提交赋值，错误通知并保留到最终 Dispose。
        /// 赋值结果不确定时抛 ResourceAssignmentException，冻结并保留新旧凭证直到清理。
        /// </summary>
        public void Replace(string key)
        {
            RequireSynchronousMode();
            RequireMutable();
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Resource key is required.", nameof(key));
            }
            ownerToken.ThrowIfCancellationRequested();
            synchronousOperating = true;
            try
            {
                lifetime.Run(token =>
                {
                    ISynchronousResourceLease<T> candidate = null;
                    var assigningCandidate = false;
                    try
                    {
                        candidate = synchronousLoader.Load<T>(key);
                        if (candidate == null)
                        {
                            throw new InvalidOperationException("Resource loader returned a null synchronous lease.");
                        }
                        var asset = candidate.Asset;
                        token.ThrowIfCancellationRequested();
                        ownerToken.ThrowIfCancellationRequested();
                        if (asset == null)
                        {
                            throw new InvalidOperationException("Resource loader returned a null asset.");
                        }
                        assigningCandidate = true;
                        SetTarget(asset);
                        assigningCandidate = false;
                        var previous = synchronousCurrent;
                        synchronousCurrent = candidate;
                        candidate = null;
                        ReleaseSynchronous(previous);
                    }
                    catch (ResourceAssignmentException) when (assigningCandidate)
                    {
                        synchronousUncertainCandidate = candidate;
                        candidate = null;
                        throw;
                    }
                    catch (SynchronousResourceLoadException error)
                    {
                        // 后端没有交出凭证，但明确有回滚残留；保留到所属页面销毁时报告。
                        RecordReleaseError(error);
                        throw;
                    }
                    catch (ResourceLoadException error)
                    {
                        // 同步入口不能接管异步回滚；销毁时必须报告无法确认资源释放。
                        RecordReleaseError(error);
                        throw;
                    }
                    finally
                    {
                        ReleaseSynchronous(candidate);
                    }
                });
            }
            finally
            {
                synchronousOperating = false;
            }
        }

        /// <summary>先清空显示，再同步释放旧资源；冻结后只能通过销毁结束持有。</summary>
        public void Clear()
        {
            RequireSynchronousMode();
            RequireMutable();
            synchronousOperating = true;
            try
            {
                lifetime.Run(_ =>
                {
                    SetTarget(null);
                    var previous = synchronousCurrent;
                    synchronousCurrent = null;
                    ReleaseSynchronous(previous);
                });
            }
            finally
            {
                synchronousOperating = false;
            }
        }

        public void Dispose()
        {
            RequireSynchronousMode();
            if (assigning || synchronousOperating)
            {
                throw new InvalidOperationException("Cannot dispose a resource slot during its synchronous operation.");
            }
            lifetime.Dispose();
        }

        private void ReleaseSynchronous(ISynchronousResourceLease<T> lease)
        {
            if (lease == null)
            {
                return;
            }
            try
            {
                lease.Dispose();
            }
            catch (Exception error)
            {
                RecordReleaseError(error);
            }
        }

        private void ReleaseSynchronousCurrent()
        {
            // 清空失败继续持有原资源，避免释放仍被渲染器借用的资源。
            ClearTargetForRelease();
            var previous = synchronousCurrent;
            synchronousCurrent = null;
            var uncertain = synchronousUncertainCandidate;
            synchronousUncertainCandidate = null;
            ReleaseSynchronous(uncertain);
            ReleaseSynchronous(previous);
            ReleaseExternalReferences();
        }

        private void RequireSynchronousMode()
        {
            RequireThread();
            if (Mode != LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("This resource slot requires asynchronous operations.");
            }
        }

        private void RequireAsyncMode()
        {
            RequireThread();
            if (Mode == LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("A synchronous resource slot cannot start asynchronous operations.");
            }
        }
    }
}
