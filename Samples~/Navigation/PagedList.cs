using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Samples.Navigation
{
    /// <summary>
    /// 有界、单请求的分页来源，可直接作为虚拟列表输入。
    /// 失败保留已加载数据与游标；重新调用加载即重试。条目模型只借用，不在此销毁。
    /// 此来源属于异步扩展；纯同步列表应使用 ObservableList，不创建此分页来源。
    /// </summary>
    public sealed partial class PagedList<T, TCursor> : IReadOnlyObservableList<T>, IPagedListSource, IVersionedPagedListSource, IAsyncDisposable
    {
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly ObservableList<T> items = new ObservableList<T>();
        private HashSet<object> keys = new HashSet<object>();
        private List<object> keyOrder = new List<object>();
        private readonly Func<T, object> itemKey;
        private Func<TCursor, int, CancellationToken, ValueTask<ListPage<T, TCursor>>> loader;
        private readonly Lifetime work;
        private readonly CancellationTokenSource cancellation;
        private readonly int pageSize;
        private readonly int maxItems;
        private readonly AsyncLocal<LoadingFrame> loadingFrame = new AsyncLocal<LoadingFrame>();
        private TCursor cursor;
        private TaskCompletionSource<PageLoadResult> loading;
        private CancellationTokenSource loadCancellation;
        private TaskCompletionSource<bool> disposal;
        private bool notifying;
        private bool loadingState;
        private Exception loadError;

        public PagedList(Lifetime owner,
                    Func<TCursor, int, CancellationToken, ValueTask<ListPage<T, TCursor>>> loader,
                    Func<T, object> itemKey, TCursor initialCursor = default, int pageSize = 50, int maxItems = 10000,
                    PageInsertion insertion = PageInsertion.Append,
                    PageOverflowPolicy overflow = PageOverflowPolicy.Reject)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            // 在创建异步生命周期、完成信号和取消关联前拒绝，避免同步宿主隐式分配异步状态。
            if (owner.Mode == LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("异步分页来源不能属于同步生命周期，请使用 ObservableList 提供同步数据。");
            }

            if (owner.IsEnded)
            {
                throw new ObjectDisposedException(nameof(owner));
            }

            this.loader = loader ?? throw new ArgumentNullException(nameof(loader));
            this.itemKey = itemKey ?? throw new ArgumentNullException(nameof(itemKey));
            if (pageSize < 1 || maxItems < pageSize)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must fit the positive item capacity.");
            }

            if (!Enum.IsDefined(typeof(PageInsertion), insertion))
            {
                throw new ArgumentOutOfRangeException(nameof(insertion));
            }

            Insertion = insertion;
            if (!Enum.IsDefined(typeof(PageOverflowPolicy), overflow))
            {
                throw new ArgumentOutOfRangeException(nameof(overflow));
            }

            OverflowPolicy = overflow;
            this.pageSize = pageSize;
            this.maxItems = maxItems;
            if (context == null || context.GetType() == typeof(SynchronizationContext))
            {
                throw new InvalidOperationException("Paged list requires an owning UI synchronization context.");
            }

            cursor = initialCursor;
            work = new Lifetime();
            try
            {
                cancellation = CancellationTokenSource.CreateLinkedTokenSource(owner.Token, work.Token);
                owner.Own(this);
                items.Changed += PublishChanges;
            }
            catch
            {
                if (cancellation != null)
                {
                    cancellation.Dispose();
                }

                // 尚未启动加载或登记资源，构造失败可直接归还内部生命周期。
                work.Dispose();
                throw;
            }
        }

        public event Action<ListChangeSet<T>> Changed;

        public event Action StateChanged;

        public PageInsertion Insertion
        {
            get;
        }

        /// <summary>容量不足时拒绝加载，或淘汰与加载方向相反一端的条目。</summary>
        public PageOverflowPolicy OverflowPolicy
        {
            get;
        }

        /// <summary>最多保留的条目数；不代表业务模型、界面单元或资源的总内存上限。</summary>
        public int Capacity => maxItems;

        public bool IsActive => disposal == null && dispatchFailure == null && !cancellation.IsCancellationRequested;

        public bool IsLoading
        {
            get => dispatchFailure == null && (loadingState || IsResetting);
            private set => loadingState = value;
        }

        public bool HasMore { get; private set; } = true;

        public Exception Error
        {
            get => dispatchFailure ?? loadError;
            private set => loadError = value;
        }

        public int Count => items.Count;

        public long Version => items.Version;

        public T this[int index] => items[index];

        public IEnumerator<T> GetEnumerator() => items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>重复调用共享一次加载；令牌只取消调用者等待，父生命周期取消底层操作。</summary>
        public ValueTask<PageLoadResult> LoadNextAsync(CancellationToken cancellationToken = default)
        {
            RequireThread();
            RejectCallback();
            if (dispatchFailure != null)
            {
                return new ValueTask<PageLoadResult>(new PageLoadResult(PageLoadStatus.Failed, error: dispatchFailure));
            }

            if (disposal != null || cancellation.IsCancellationRequested)
            {
                return Result(PageLoadStatus.Inactive);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Result(PageLoadStatus.WaitCancelled);
            }

            if (IsResetting)
            {
                return Result(PageLoadStatus.Resetting);
            }

            if (loading != null && !loading.Task.IsCompleted)
            {
                return new ValueTask<PageLoadResult>(WaitAsync(loading.Task, cancellationToken));
            }

            if (!HasMore)
            {
                return Result(PageLoadStatus.EndReached);
            }

            loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
            loading = new TaskCompletionSource<PageLoadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            IsLoading = true;
            Error = null;
            PublishState();
            _ = CompleteLoadAsync(loading, loadCancellation);
            return new ValueTask<PageLoadResult>(WaitAsync(loading.Task, cancellationToken));
        }

        private async Task CompleteLoadAsync(TaskCompletionSource<PageLoadResult> completion, CancellationTokenSource request)
        {
            PageLoadResult result;
            try
            {
                result = await work.RunAsync(_ => LoadCoreAsync(request.Token));
            }
            catch (OperationCanceledException) when (request.IsCancellationRequested)
            {
                result = new PageLoadResult(cancellation.IsCancellationRequested
                    ? PageLoadStatus.Inactive : PageLoadStatus.Superseded);
            }
            catch (Exception error)
            {
                result = new PageLoadResult(PageLoadStatus.Failed, error: error);
            }

            try
            {
                await OnOwnerAsync(() =>
                {
                    if (!IsResetting)
                    {
                        Error = result.Error;
                    }

                    IsLoading = false;
                    loadCancellation = null;
                    request.Dispose();
                    PublishState();
                    completion.TrySetResult(result);
                    return true;
                });
            }
            catch (Exception error)
            {
                // 调度失败不能改在线程池发布状态；以终态错误结束等待，保留错误供所属线程读取。
                Interlocked.CompareExchange(ref dispatchFailure, error, null);
                request.Dispose();
                completion.TrySetResult(new PageLoadResult(PageLoadStatus.Failed, error: error));
            }
        }

        private async ValueTask<PageLoadResult> LoadCoreAsync(CancellationToken token)
        {
            var previous = loadingFrame.Value;
            var frame = new LoadingFrame();
            loadingFrame.Value = frame;
            try
            {
                token.ThrowIfCancellationRequested();
                var available = OverflowPolicy == PageOverflowPolicy.EvictOppositeEnd ? pageSize : maxItems - items.Count;
                if (available == 0)
                {
                    throw new InvalidOperationException("Paged list item capacity is exhausted.");
                }

                var requested = Math.Min(pageSize, available);
                var page = await loader(cursor, requested, token);
                return await OnOwnerAsync(() => CommitPage(page, requested, token));
            }
            finally
            {
                frame.Active = false;
                loadingFrame.Value = previous;
            }
        }

        public ValueTask DisposeAsync()
        {
            RequireThread();
            RejectCallback();
            if (disposal != null)
            {
                return new ValueTask(disposal.Task);
            }

            disposal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = DisposeCoreAsync();
            return new ValueTask(disposal.Task);
        }

        private async Task DisposeCoreAsync()
        {
            var errors = new List<Exception>();
            try
            {
                await work.DisposeAsync();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            if (loading != null)
            {
                await loading.Task;
            }

            if (resetDrain != null)
            {
                await resetDrain.Task;
            }

            try
            {
                await OnOwnerAsync(() =>
                {
                    keys.Clear();
                    keyOrder.Clear();
                    HasMore = false;
                    IsLoading = false;
                    items.Clear();
                    PublishState();
                    items.Changed -= PublishChanges;
                    Changed = null;
                    StateChanged = null;
                    return true;
                });
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
            finally
            {
                cancellation.Dispose();
            }

            if (errors.Count == 0)
            {
                disposal.TrySetResult(true);
            }
            else
            {
                disposal.TrySetException(new AggregateException("Paged list cleanup failed.", errors));
            }
        }

        private void PublishChanges(ListChangeSet<T> change)
        {
            notifying = true;
            try
            {
                var handlers = Changed;
                if (handlers == null)
                {
                    return;
                }

                foreach (Action<ListChangeSet<T>> handler in handlers.GetInvocationList())
                {
                    try
                    {
                        handler(change);
                    }
                    catch (Exception error)
                    {
                        UIErrors.Report(error);
                    }
                }
            }
            finally
            {
                notifying = false;
            }
        }

        private void PublishState()
        {
            notifying = true;
            try
            {
                var handlers = StateChanged;
                if (handlers == null)
                {
                    return;
                }

                foreach (Action handler in handlers.GetInvocationList())
                {
                    try
                    {
                        handler();
                    }
                    catch (Exception error)
                    {
                        UIErrors.Report(error);
                    }
                }
            }
            finally
            {
                notifying = false;
            }
        }

        private void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("Paged list operations must run on their owning UI thread.");
            }
        }

        private void RejectCallback()
        {
            if (ownerCallback || notifying || (loadingFrame.Value != null && loadingFrame.Value.Active))
            {
                throw new InvalidOperationException("Cannot load, reset or await disposal from this source's loader or notification callback.");
            }
        }

        private static ValueTask<PageLoadResult> Result(PageLoadStatus status) =>
                    new ValueTask<PageLoadResult>(new PageLoadResult(status));

        private static async Task<PageLoadResult> WaitAsync(Task<PageLoadResult> shared, CancellationToken token)
        {
            if (!token.CanBeCanceled || shared.IsCompleted)
            {
                return await shared;
            }

            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(shared, cancelled.Task) != shared)
                {
                    return new PageLoadResult(PageLoadStatus.WaitCancelled);
                }

                return await shared;
            }
        }

        private sealed class LoadingFrame
        {
            public bool Active = true;
        }
    }
}
