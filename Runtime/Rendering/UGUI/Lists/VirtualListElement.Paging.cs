using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class VirtualListElement
    {
        [SerializeField] private bool autoLoadPages;
        [SerializeField, Min(0)] private int pagePrefetchItems = 5;
        private IPagedListSource pageSource;
        private Action pageSourceChanged;
        private Exception pageRequestError;
        private Exception pageSourceError;
        private Task<PageLoadResult> automaticPage;
        private long pageSourceVersion;
        private bool publishingPageState;
        private bool pageStatePending;

        public bool AutoLoadPages
        {
            get => autoLoadPages;
            set
            {
                RequireListAlive();
                if (value)
                {
                    RequireAsyncListAllowed();
                }

                autoLoadPages = value;
                NotifyChanged();
            }
        }

        public bool IsLoadingPage => ReadPageFlag(loading: true);

        public bool HasMorePages => ReadPageFlag(loading: false);

        public Exception PageError
        {
            get
            {
                RequirePageStateThread();
                if (pageSourceError != null || pageRequestError != null)
                {
                    return pageSourceError ?? pageRequestError;
                }

                var source = pageSource;
                var version = pageSourceVersion;
                var activation = lifetime;
                if (source == null || activation == null || activation.IsEnded)
                {
                    return null;
                }

                try
                {
                    var error = source.Error;
                    return IsCurrentPageLoad(source, version, activation) ? error : null;
                }
                catch (Exception error)
                {
                    RecordPageSourceError(source, version, activation, error);
                    return IsCurrentPageLoad(source, version, activation) ? error : null;
                }
            }
        }

        /// <summary>初始化前设置自动页尾加载；预取阈值按方向计算当前物化范围之前或之后的条目数。</summary>
        public void ConfigurePaging(bool automatic, int prefetchItems = 5)
        {
            RequireListAlive();
            if (initialized)
            {
                throw new InvalidOperationException("Configure pagination before initialization.");
            }

            if (prefetchItems < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(prefetchItems));
            }

            autoLoadPages = automatic;
            pagePrefetchItems = prefetchItems;
        }

        private void BindPageSource(IPagedListSource source)
        {
            var previous = pageSource;
            var previousHandler = pageSourceChanged;
            var version = ++pageSourceVersion;
            pageSource = null;
            pageSourceChanged = null;
            pageRequestError = null;
            pageSourceError = null;
            automaticPage = null;
            if (previous != null && previousHandler != null)
            {
                previous.StateChanged -= previousHandler;
            }

            if (!IsAlive || version != pageSourceVersion)
            {
                return;
            }

            pageSource = source;
            if (source != null)
            {
                object queryVersion = null;
                var subscriptionThread = Thread.CurrentThread.ManagedThreadId;
                Action handler = () =>
                {
                    if (version != pageSourceVersion || !ReferenceEquals(source, pageSource))
                    {
                        return;
                    }

                    if (Thread.CurrentThread.ManagedThreadId != subscriptionThread)
                    {
                        throw new InvalidOperationException("分页来源必须在所属 UI 线程通知状态。");
                    }

                    if (IsAlive)
                    {
                        object currentQuery;
                        try
                        {
                            currentQuery = GetPageQueryVersion(source);
                        }
                        catch (Exception error)
                        {
                            if (IsAlive && version == pageSourceVersion && ReferenceEquals(source, pageSource))
                            {
                                pageSourceError = error;
                                PublishPageState();
                            }

                            return;
                        }

                        if (!IsAlive || version != pageSourceVersion || !ReferenceEquals(source, pageSource))
                        {
                            return;
                        }

                        pageSourceError = null;
                        if (!ReferenceEquals(queryVersion, currentQuery))
                        {
                            queryVersion = currentQuery;
                            pageRequestError = null;
                        }

                        PublishPageState();
                    }
                };
                pageSourceChanged = handler;
                try
                {
                    source.StateChanged += handler;
                }
                catch (Exception subscriptionError)
                {
                    if (version == pageSourceVersion && ReferenceEquals(source, pageSource) &&
                        ReferenceEquals(handler, pageSourceChanged))
                    {
                        pageSource = null;
                        pageSourceChanged = null;
                    }

                    try
                    {
                        source.StateChanged -= handler;
                    }
                    catch (Exception removalError)
                    {
                        throw new AggregateException("分页来源订阅与回滚均失败。", subscriptionError, removalError);
                    }

                    throw;
                }

                if (!IsAlive || version != pageSourceVersion || !ReferenceEquals(source, pageSource))
                {
                    // 自定义事件 add 可同步换源，旧监听需在 add 真正返回后撤销。
                    source.StateChanged -= handler;
                }
            }
        }

        /// <summary>显式加载下一页或重试失败页，失败不隐藏已有内容；取消只影响本次等待。</summary>
        public ValueTask<PageLoadResult> LoadNextPageAsync(CancellationToken cancellationToken = default)
        {
            RequireListAlive();
            RequireAsyncListAllowed();
            if (initialized && Thread.CurrentThread.ManagedThreadId != uiThread)
            {
                throw new InvalidOperationException("Page loading must start on the list's UI thread.");
            }

            if (publishingPageState)
            {
                throw new InvalidOperationException("Cannot load a page from its state notification.");
            }

            var source = pageSource;
            var version = pageSourceVersion;
            var activation = lifetime;
            if (source == null || scope == null || !scope.IsActive || activation == null || activation.IsEnded)
            {
                return new ValueTask<PageLoadResult>(new PageLoadResult(PageLoadStatus.Inactive));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return new ValueTask<PageLoadResult>(new PageLoadResult(PageLoadStatus.WaitCancelled));
            }

            bool active;
            try
            {
                active = source.IsActive;
            }
            catch (Exception error)
            {
                if (!IsCurrentPageLoad(source, version, activation))
                {
                    return new ValueTask<PageLoadResult>(new PageLoadResult(
                        activation.IsEnded ? PageLoadStatus.Inactive : PageLoadStatus.Superseded));
                }

                pageSourceError = error;
                PublishPageState();
                return new ValueTask<PageLoadResult>(new PageLoadResult(PageLoadStatus.Failed, error: error));
            }

            if (!IsCurrentPageLoad(source, version, activation))
            {
                return new ValueTask<PageLoadResult>(new PageLoadResult(
                    activation.IsEnded ? PageLoadStatus.Inactive : PageLoadStatus.Superseded));
            }

            if (!active || IsVisualRetentionActive)
            {
                return new ValueTask<PageLoadResult>(new PageLoadResult(PageLoadStatus.Inactive));
            }

            return activation.RunAsync(_ => new ValueTask<PageLoadResult>(
                LoadPageAsync(source, version, activation, cancellationToken)));
        }

        private async Task<PageLoadResult> LoadPageAsync(IPagedListSource source, long version,
                    Lifetime activation, CancellationToken token)
        {
            object queryVersion;
            try
            {
                queryVersion = GetPageQueryVersion(source);
            }
            catch (Exception error)
            {
                if (!IsCurrentPageLoad(source, version, activation))
                {
                    return new PageLoadResult(activation.IsEnded ? PageLoadStatus.Inactive : PageLoadStatus.Superseded);
                }

                pageSourceError = error;
                PublishPageState();
                return new PageLoadResult(PageLoadStatus.Failed, error: error);
            }

            if (!IsCurrentPageLoad(source, version, activation))
            {
                return new PageLoadResult(activation.IsEnded ? PageLoadStatus.Inactive : PageLoadStatus.Superseded);
            }

            pageSourceError = null;
            PageLoadResult result;
            try
            {
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, activation.Token))
                {
                    pageRequestError = null;
                    result = await source.LoadNextAsync(linked.Token);
                }
            }
            catch (Exception error)
            {
                result = new PageLoadResult(PageLoadStatus.Failed, error: error);
            }

            if (Thread.CurrentThread.ManagedThreadId != uiThread)
            {
                var threadingError = new InvalidOperationException("分页加载必须在所属 UI 线程恢复。");
                Exception failure = threadingError;
                if (result.Status == PageLoadStatus.Failed && result.Error != null)
                {
                    failure = new AggregateException("分页加载与线程恢复均失败。", result.Error, threadingError);
                }

                return new PageLoadResult(PageLoadStatus.Failed,
                    error: failure);
            }

            // 已换来源或重新激活的旧等待只返回结果，不写回当前分页表现。
            if (IsCurrentPageLoad(source, version, activation))
            {
                object currentQuery;
                try
                {
                    currentQuery = GetPageQueryVersion(source);
                }
                catch (Exception error)
                {
                    if (!IsCurrentPageLoad(source, version, activation))
                    {
                        return result;
                    }

                    var failure = result.Status == PageLoadStatus.Failed && result.Error != null
                        ? new AggregateException("分页加载与查询版本读取均失败。", result.Error, error)
                        : error;
                    pageSourceError = failure;
                    PublishPageState();
                    return new PageLoadResult(PageLoadStatus.Failed, error: failure);
                }

                if (IsCurrentPageLoad(source, version, activation) && ReferenceEquals(queryVersion, currentQuery))
                {
                    pageSourceError = null;
                    if (result.Status == PageLoadStatus.Failed)
                    {
                        pageRequestError = result.Error ?? new InvalidOperationException("Page load failed without an error.");
                    }

                    PublishPageState();
                }
            }

            return result;
        }

        private static object GetPageQueryVersion(IPagedListSource source)
        {
            if (!(source is IVersionedPagedListSource versioned))
            {
                return null;
            }

            return versioned.QueryVersion ??
                throw new InvalidOperationException("分页查询版本不能为空。");
        }

        private bool IsCurrentPageLoad(IPagedListSource source, long version, Lifetime activation) =>
            IsAlive && version == pageSourceVersion && ReferenceEquals(source, pageSource) &&
            ReferenceEquals(activation, lifetime) && !activation.IsEnded;

        private bool ReadPageFlag(bool loading)
        {
            RequirePageStateThread();
            var source = pageSource;
            var version = pageSourceVersion;
            var activation = lifetime;
            if (source == null || activation == null || activation.IsEnded || pageSourceError != null)
            {
                return false;
            }

            try
            {
                var active = source.IsActive;
                if (!active || !IsCurrentPageLoad(source, version, activation))
                {
                    return false;
                }

                var value = loading ? source.IsLoading : source.HasMore;
                return IsCurrentPageLoad(source, version, activation) && value;
            }
            catch (Exception error)
            {
                RecordPageSourceError(source, version, activation, error);
                return false;
            }
        }

        private void RecordPageSourceError(IPagedListSource source, long version, Lifetime activation, Exception error)
        {
            if (!IsCurrentPageLoad(source, version, activation))
            {
                return;
            }

            pageSourceError = error;
            PublishPageState();
        }

        private void RequirePageStateThread()
        {
            if (initialized && Thread.CurrentThread.ManagedThreadId != uiThread)
            {
                throw new InvalidOperationException("分页状态必须在所属 UI 线程读取。");
            }
        }

        private void TryLoadMorePages()
        {
            if (!autoLoadPages || running || IsVisualRetentionActive || pageSourceError != null ||
                pageRequestError != null || (automaticPage != null && !automaticPage.IsCompleted))
            {
                return;
            }

            var source = pageSource;
            var version = pageSourceVersion;
            var activation = lifetime;
            if (source == null || activation == null || activation.IsEnded)
            {
                return;
            }

            PageInsertion insertion;
            try
            {
                var active = source.IsActive;
                if (!active || !IsCurrentPageLoad(source, version, activation))
                {
                    return;
                }

                var hasMore = source.HasMore;
                if (!hasMore || !IsCurrentPageLoad(source, version, activation))
                {
                    return;
                }

                var loading = source.IsLoading;
                if (loading || !IsCurrentPageLoad(source, version, activation))
                {
                    return;
                }

                var sourceError = source.Error;
                if (sourceError != null || !IsCurrentPageLoad(source, version, activation))
                {
                    return;
                }

                insertion = source.Insertion;
            }
            catch (Exception error)
            {
                if (IsCurrentPageLoad(source, version, activation))
                {
                    pageSourceError = error;
                    PublishPageState();
                }

                return;
            }

            if (!IsCurrentPageLoad(source, version, activation))
            {
                return;
            }

            Layout.GetRange(snapshot.Count, scrollRect.content.anchoredPosition.y,
                scrollRect.viewport.rect.height, out var start, out var end);
            var remaining = insertion == PageInsertion.Prepend ? start : snapshot.Count - end;
            if (remaining <= pagePrefetchItems)
            {
                // 每帧最多发起一次；同步返回的短页也不会在同一帧递归拉取后续页。
                var request = LoadNextPageAsync(activation.Token).AsTask();
                if (IsCurrentPageLoad(source, version, activation))
                {
                    automaticPage = request;
                }
            }
        }

        private void PublishPageState()
        {
            pageStatePending = true;
            if (publishingPageState)
            {
                return;
            }

            publishingPageState = true;
            try
            {
                var passes = 0;
                while (pageStatePending && IsAlive)
                {
                    if (++passes > 32)
                    {
                        UIErrors.Report(new InvalidOperationException("分页状态在 32 次通知后仍未稳定。"));
                        break;
                    }

                    pageStatePending = false;
                    var version = pageSourceVersion;
                    NotifyPageProperty(nameof(IsLoadingPage));
                    if (!IsAlive || version != pageSourceVersion)
                    {
                        pageStatePending = true;
                        continue;
                    }

                    NotifyPageProperty(nameof(HasMorePages));
                    if (!IsAlive || version != pageSourceVersion)
                    {
                        pageStatePending = true;
                        continue;
                    }

                    NotifyPageProperty(nameof(PageError));
                }
            }
            finally
            {
                publishingPageState = false;
                pageStatePending = false;
            }
        }

        private void NotifyPageProperty(string property)
        {
            try
            {
                NotifyChanged(property);
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }
    }
}
