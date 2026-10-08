using System;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.UGUI
{
    /// <summary>
    /// 所属 UI 线程上的单个可替换资源槽。新请求使旧结果
    /// 失效，旧显示资源保持持有，直到替换资源已赋值。
    /// 将槽托管给目标生命周期，并传入该生命周期令牌，
    /// 使目标停用后立即停止待处理赋值。
    /// </summary>
    internal sealed partial class ResourceSlot<T> : IAsyncDisposable where T : class
    {
        private IResourceLoader loader;
        private Action<T> assign;
        private CancellationToken ownerToken;
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;
        private readonly LifetimeScope lifetime;
        private IAcquiredResource<T> current;
        private T borrowed;
        private IAcquiredResource<T> uncertainCandidate;
        private long generation;
        private bool assigning;
        private bool frozen;
        private bool assignmentUncertain;
        private LoadRequest latestLoad;
        private readonly UIErrorContext diagnosticContext;
        private bool targetReleased;

        /// <param name="assign">
        /// 同步赋值借用资源，null 表示清空目标。操作必须原子化：
        /// 抛出异常时保持原目标不变，清空操作不能抛出异常。
        /// 如果原生 setter 可能已经修改引用后才抛错，须包装为
        /// ResourceAssignmentException，槽会冻结并保留可能被借用的候选。
        /// 不能在此重入资源槽，
        /// 也不能执行异步工作。已销毁目标可忽略 null，
        /// Unity 适配器必须用 == null 检查原生对象有效性。
        /// </param>
        public ResourceSlot(IResourceLoader loader, Action<T> assign, CancellationToken ownerToken = default)
        {
            diagnosticContext = UIErrors.CurrentContext;
            lifetime = new LifetimeScope();
            this.loader = loader ?? throw new ArgumentNullException(nameof(loader));
            this.assign = assign ?? throw new ArgumentNullException(nameof(assign));
            this.ownerToken = ownerToken;
            lifetime.OnDisposeAsync(ReleaseCurrentAsync, true, "ResourceSlot<" + typeof(T).Name + ">.Clear", threadId);
        }

        /// <summary>借用资源，仅在替换、清空或销毁前有效。</summary>
        public T Asset
        {
            get
            {
                RequireThread();
                return current == null ? borrowed : current.Asset;
            }
        }

        public bool IsFrozen
        {
            get
            {
                RequireThread();
                return frozen;
            }
        }

        /// <summary>
        /// 永久停止替换和清空请求，但继续持有当前已赋值凭证。
        /// 待处理请求被取代；即使加载器忽略取消，也不能赋值迟到结果。
        /// 仍须调用 DisposeAsync，先清空目标再释放保留的凭证。
        /// </summary>
        public void Freeze()
        {
            RequireThread();
            if (assigning)
            {
                throw new InvalidOperationException("Cannot freeze a resource slot from its assignment callback.");
            }

            if (lifetime.IsEnded)
            {
                throw new ObjectDisposedException(nameof(ResourceSlot<T>));
            }

            if (frozen)
            {
                return;
            }

            frozen = true;
            ++generation;
            var previous = latestLoad;
            latestLoad = null;
            previous?.Supersede();
        }

        /// <summary>
        /// 已赋值返回 true，被取代返回 false。加载或赋值失败保留
        /// 当前资源并抛出异常，调用者或所有者取消也抛出异常。
        /// ResourceAssignmentException 表示显示结果不确定，槽冻结并保留新旧凭证直到清理。
        /// 即使加载器忽略取消，迟到结果仍会释放。取代请求会
        /// 合作式取消旧提供方操作，并返回 false。
        /// </summary>
        public ValueTask<bool> ReplaceAsync(string key, CancellationToken cancellationToken = default)
        {
            RequireThread();
            RequireMutable();
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Resource key is required.", nameof(key));
            }

            ownerToken.ThrowIfCancellationRequested();
            cancellationToken.ThrowIfCancellationRequested();
            var request = ++generation;
            var previous = latestLoad;
            var load = latestLoad = new LoadRequest();
            return lifetime.RunAsync(async token =>
            {
                try
                {
                    // 调用任意取消回调前先登记本操作。
                    previous?.Supersede();
                    return await ReplaceCoreAsync(key, request, token, cancellationToken, load);
                }
                finally
                {
                    if (ReferenceEquals(latestLoad, load))
                    {
                        latestLoad = null;
                    }

                    load.Dispose();
                }
            });
        }

        /// <summary>使待处理结果失效，清空目标，再释放其凭证。</summary>
        public ValueTask ClearAsync()
        {
            return new ValueTask(SetBorrowedAsync(null).AsTask());
        }

        /// <summary>
        /// 同步替换目标为借用对象，使在途加载失效，再异步归还旧凭证。
        /// 调用方必须持有该对象至显示结束；槽不取得或释放借用对象的所有权。
        /// </summary>
        public ValueTask<bool> SetBorrowedAsync(T asset)
        {
            RequireThread();
            RequireMutable();
            ownerToken.ThrowIfCancellationRequested();
            var request = ++generation;
            var previousLoad = latestLoad;
            latestLoad = null;
            return lifetime.RunAsync(async token =>
            {
                previousLoad?.Supersede();
                token.ThrowIfCancellationRequested();
                if (request != generation)
                {
                    return false;
                }

                SetTarget(asset);
                var previous = current;
                current = null;
                borrowed = asset;
                await ReleaseAsync(previous);
                return true;
            });
        }

        public ValueTask DisposeAsync()
        {
            RequireThread();
            if (assigning)
            {
                throw new InvalidOperationException("Cannot dispose a resource slot from its assignment callback.");
            }

            using (BeginResourcePhase("Release"))
            {
                return lifetime.DisposeAsync();
            }
        }

        internal IDisposable BeginResourcePhase(string phase) =>
            UIErrors.BeginOwnedPhase(diagnosticContext, "Resource", phase);

        private async ValueTask<bool> ReplaceCoreAsync(string key, long request, CancellationToken lifetimeToken, CancellationToken callerToken, LoadRequest load)
        {
            using (BeginResourcePhase("Load"))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken, ownerToken, callerToken, load.Cancellation.Token))
            {
                IAcquiredResource<T> candidate = null;
                var assigningCandidate = false;
                try
                {
                    linked.Token.ThrowIfCancellationRequested();
                    candidate = await loader.LoadAsync<T>(key, linked.Token);
                    if (candidate == null)
                    {
                        throw new InvalidOperationException("Resource loader returned a null resource acquisition.");
                    }

                    RequireThread();
                    linked.Token.ThrowIfCancellationRequested();
                    if (request != generation)
                    {
                        return false;
                    }

                    var asset = candidate.Asset;
                    // 凭证属性访问器属于外部代码，可能冻结、销毁或取代此槽。
                    linked.Token.ThrowIfCancellationRequested();
                    if (request != generation)
                    {
                        return false;
                    }

                    if (asset == null)
                    {
                        throw new InvalidOperationException("Resource loader returned a null asset.");
                    }

                    assigningCandidate = true;
                    SetTarget(asset);
                    assigningCandidate = false;
                    var previous = current;
                    current = candidate;
                    borrowed = null;
                    candidate = null;
                    await ReleaseAsync(previous);
                    return true;
                }
                catch (ResourceAssignmentException error) when (assigningCandidate)
                {
                    UIErrors.AttachContext(error, UIErrors.CurrentContext);
                    // SetTarget 已冻结槽，后续请求不能再产生第二份不确定候选。
                    uncertainCandidate = candidate;
                    candidate = null;
                    throw;
                }
                catch (OperationCanceledException) when (load.Superseded && !lifetimeToken.IsCancellationRequested && !ownerToken.IsCancellationRequested && !callerToken.IsCancellationRequested)
                {
                    return false;
                }
                catch (ResourceLoadException failure) when (candidate == null)
                {
                    UIErrors.AttachContext(failure, UIErrors.CurrentContext);
                    using (BeginResourcePhase("Rollback"))
                    {
                        try
                        {
                            await failure.CleanupCompletion;
                        }
                        catch (Exception cleanup)
                        {
                            UIErrors.AttachContext(cleanup, UIErrors.CurrentContext);
                            RecordReleaseError(cleanup);
                            throw new AggregateException("资源槽加载与后端回滚均失败。", failure, cleanup);
                        }
                    }

                    Exception cause = failure;
                    while (cause is ResourceLoadException && cause.InnerException != null)
                    {
                        cause = cause.InnerException;
                    }
                    if (cause is OperationCanceledException cancelled)
                    {
                        if (load.Superseded && !lifetimeToken.IsCancellationRequested &&
                            !ownerToken.IsCancellationRequested && !callerToken.IsCancellationRequested)
                        {
                            return false;
                        }
                        throw new OperationCanceledException("资源槽加载已取消，回滚已结束。", failure, cancelled.CancellationToken);
                    }
                    throw;
                }
                catch (Exception error)
                {
                    UIErrors.AttachContext(error, UIErrors.CurrentContext);
                    throw;
                }
                finally
                {
                    // 赋值流程未接管已被取代或失败结果的所有权。
                    await ReleaseAsync(candidate);
                }
            }
        }

        private void SetTarget(T asset)
        {
            using (BeginResourcePhase("Assignment"))
            {
                RequireThread();
                assigning = true;
                try
                {
                    assign(asset);
                }
                catch (Exception error)
                {
                    UIErrors.AttachContext(error, UIErrors.CurrentContext);
                    // 外部原生回调可能在修改引用后抛错；保留当前显示状态和凭证，禁止继续写入。
                    if (error is ResourceAssignmentException)
                    {
                        frozen = true;
                        assignmentUncertain = true;
                        ++generation;
                    }
                    throw;
                }
                finally
                {
                    assigning = false;
                }
            }
        }

        private async ValueTask ReleaseAsync(IAcquiredResource<T> ownedResource)
        {
            if (ownedResource == null)
            {
                return;
            }

            using (BeginResourcePhase("Release"))
            {
                try
                {
                    await CleanupRegistry.ReleaseAsync(ownedResource, "ResourceSlot<" + typeof(T).Name + ">");
                }
                catch (Exception error)
                {
                    UIErrors.AttachContext(error, UIErrors.CurrentContext);
                    // 释放失败不能撤销已提交赋值，也不能掩盖原加载错误，
                    // 该错误同时保留到最终清理时报告。
                    RecordReleaseError(error, responsibility: CleanupRegistry.GetResponsibility(ownedResource,
                        "ResourceSlot<" + typeof(T).Name + ">"));
                }
            }
        }

        private void ClearTargetForRelease()
        {
            try
            {
                SetTarget(null);
                assignmentUncertain = false;
            }
            catch (Exception error)
            {
                // 清理回调抛出的异常会由 LifetimeScope 自身汇总，不再次登记造成重复错误。
                RecordReleaseError(error, false);
                throw;
            }
        }

        private async ValueTask ReleaseCurrentAsync()
        {
            // 销毁在执行此回调前等待所有在途请求。
            // 清空失败时保留所有权，避免卸载仍被显示的资源。
            if (!targetReleased)
            {
                ClearTargetForRelease();
                targetReleased = true;
            }
            var previous = current;
            current = null;
            borrowed = null;
            var uncertain = uncertainCandidate;
            uncertainCandidate = null;
            await ReleaseAsync(uncertain);
            await ReleaseAsync(previous);
            ReleaseExternalReferences();
        }

        /// <summary>在途操作已排空且目标已清空后，解除外部引用；保留清理诊断供调用者查询。</summary>
        private void ReleaseExternalReferences()
        {
            loader = null;
            assign = null;
            ownerToken = default;
            latestLoad = null;
        }

        internal void RequireMutable()
        {
            RequireThread();
            if (assigning)
            {
                throw new InvalidOperationException("Cannot reenter a resource slot from its assignment callback.");
            }

            if (lifetime.IsEnded)
            {
                throw new ObjectDisposedException(nameof(ResourceSlot<T>));
            }

            if (frozen)
            {
                throw new InvalidOperationException("A frozen resource slot cannot be changed. Dispose it after its retained visual is no longer needed.");
            }
        }

        private void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != threadId)
            {
                throw new InvalidOperationException("Resource slots must run on their owning UI thread with its synchronization context.");
            }
        }

        private sealed class LoadRequest : IDisposable
        {
            public readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
            private bool disposed;

            public bool Superseded
            {
                get; private set;
            }

            public void Supersede()
            {
                if (disposed || Superseded)
                {
                    return;
                }

                Superseded = true;
                try
                {
                    Cancellation.Cancel(throwOnFirstException: false);
                }
                catch (Exception error)
                {
                    UIErrors.Report(error);
                }
            }

            public void Dispose()
            {
                disposed = true;
                Cancellation.Dispose();
            }
        }
    }
}
