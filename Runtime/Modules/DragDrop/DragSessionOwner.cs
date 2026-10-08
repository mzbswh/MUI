using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.DragDrop
{
    /// <summary>每个来源及载荷类型只登记一次清理，只持有尚未完成的会话，避免历史拖放累积。</summary>
    internal sealed class DragSessionOwner<TPayload> : IAsyncDisposable
    {
        private static readonly ConditionalWeakTable<LifetimeScope, DragSessionOwner<TPayload>> owners =
                    new ConditionalWeakTable<LifetimeScope, DragSessionOwner<TPayload>>();
        private static readonly object ownersGate = new object();
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly List<DragSession<TPayload>> sessions = new List<DragSession<TPayload>>();
        private bool disposing;
        private bool disposed;
        private Exception failure;
        private Task disposal;

        private DragSessionOwner(LifetimeScope source)
        {
            source.Own(this);
        }

        internal static DragSessionOwner<TPayload> Attach(LifetimeScope source, DragSession<TPayload> session)
        {
            DragSessionOwner<TPayload> owner;
            // 弱表工厂可能重复执行；显式串行化登记，不能给同一来源创建多份清理所有权。
            lock (ownersGate)
            {
                if (!owners.TryGetValue(source, out owner))
                {
                    owner = new DragSessionOwner<TPayload>(source);
                    owners.Add(source, owner);
                }
            }
            owner.RequireThread();
            if (source.IsEnded || owner.disposing || owner.disposed)
            {
                throw new ObjectDisposedException(nameof(source));
            }
            if (owner.sessions.Count >= DragSession<TPayload>.MaxConcurrentSessionsPerSource)
            {
                throw new InvalidOperationException("来源生命周期的同类型在途拖放会话已达到容量上限。");
            }
            owner.sessions.Add(session);
            return owner;
        }

        internal void Remove(DragSession<TPayload> session)
        {
            RequireThread();
            sessions.Remove(session);
        }

        public ValueTask DisposeAsync()
        {
            RequireThread();
            if (disposal != null)
            {
                return new ValueTask(disposal);
            }
            if (disposing)
            {
                throw new InvalidOperationException("不能在拖放管理器的清理回调中重复等待自身清理。");
            }

            var snapshot = sessions.ToArray();
            disposing = true;
            disposal = DisposeCoreAsync(snapshot);
            return new ValueTask(disposal);
        }

        private async Task DisposeCoreAsync(DragSession<TPayload>[] snapshot)
        {
            var errors = new List<Exception>();
            for (var i = snapshot.Length - 1; i >= 0; --i)
            {
                try
                {
                    await snapshot[i].DisposeAsync();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }
            Complete(errors);
        }

        private void Complete(List<Exception> errors)
        {
            // 正常收尾的会话已经自行移除；失败且尚未收尾的会话继续保留，不能伪装成释放成功。
            disposing = false;
            disposed = true;
            failure = errors.Count == 0 ? null : new AggregateException("拖放会话清理失败。", errors);
            RethrowFailure();
        }

        private void RethrowFailure()
        {
            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }

        private void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("拖放会话管理器必须在所属 UI 线程使用。");
            }
        }
    }
}
