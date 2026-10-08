using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Resources
{
    /// <summary>
    /// 对父 Prefab 持有的现有 View 提供独占子视图访问。
    /// 释放凭证会隐藏 View，但不销毁包装器或原生节点。
    /// 子视图清理完成后，提供方可再次使用。
    /// </summary>
    public sealed class BorrowedViewProvider : IViewProvider
    {
        private readonly ViewResource resource;
        private readonly IView view;
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;
        private bool isAcquired;

        public BorrowedViewProvider(ViewResource resource, IView view)
        {
            this.resource = resource ?? throw new ArgumentNullException(nameof(resource));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
        }

        private IAcquiredView CreateCore(ViewResource requested)
        {
            RequireThread();
            if (!resource.Equals(requested))
            {
                throw new ArgumentException("Borrowed View resource mismatch.", nameof(requested));
            }

            if (!view.IsAlive)
            {
                throw new ObjectDisposedException("Borrowed View");
            }

            if (isAcquired)
            {
                throw new InvalidOperationException("Borrowed View already has a childView owner.");
            }

            // 门控可能触发项目回调；先占用，阻止重入时交出第二份独占持有权。
            isAcquired = true;
            try
            {
                view.SetHostState(false, false);
                return new AcquiredView(view, ReleaseAsync, threadId);
            }
            catch
            {
                isAcquired = false;
                throw;
            }
        }

        public Task<IAcquiredView> AcquireAsync(ViewResource requested, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IAcquiredView>(CreateCore(requested));
        }

        private ValueTask ReleaseAsync(IView value)
        {
            RequireThread();
            try
            {
                if (value.IsAlive)
                {
                    value.SetHostState(false, false);
                }
            }
            finally
            {
                isAcquired = false;
            }
            return default;
        }

        private void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != threadId)
            {
                throw new InvalidOperationException("Borrowed View access requires its owning UI thread.");
            }
        }
    }
}
