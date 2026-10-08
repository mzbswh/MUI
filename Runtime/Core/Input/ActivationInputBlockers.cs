using System;
using System.Collections.Generic;
using System.Threading;

namespace MUI
{
    /// <summary>每个生命周期只注册一次，仅保留尚未释放的阻挡器。</summary>
    internal sealed class ActivationInputBlockers : IDisposable
    {
        private readonly LifetimeScope lifetime;
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly LinkedList<OwnedBlocker> active = new LinkedList<OwnedBlocker>();
        private bool disposed;

        internal ActivationInputBlockers(LifetimeScope lifetime)
        {
            this.lifetime = lifetime;
            lifetime.OwnDisposable(this);
        }

        internal int Count
        {
            get
            {
                AssertThread();
                return active.Count;
            }
        }

        internal IDisposable Block(InputGate gate, string reason)
        {
            RequireActive();
            var token = gate.Block(reason);
            try
            {
                // Block 期间门控观察者可能同步结束本次激活。
                RequireActive();
                var blocker = new OwnedBlocker(this, token);
                blocker.Node = active.AddLast(blocker);
                return blocker;
            }
            catch
            {
                token.Dispose();
                throw;
            }
        }

        private void RequireActive()
        {
            AssertThread();
            if (disposed || lifetime.IsEnded)
            {
                throw new OperationCanceledException(lifetime.Token);
            }
        }

        private void AssertThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("Activation input blockers must be accessed on their UI thread.");
            }
        }

        public void Dispose()
        {
            AssertThread();
            if (disposed)
            {
                return;
            }

            disposed = true;
            List<Exception> failures = null;
            while (active.Last != null)
            {
                try
                {
                    active.Last.Value.Dispose();
                }
                catch (Exception error)
                {
                    if (failures == null)
                    {
                        failures = new List<Exception>();
                    }

                    failures.Add(error);
                }
            }

            if (failures != null)
            {
                throw new AggregateException("Input blocker cleanup failed.", failures);
            }
        }

        private sealed class OwnedBlocker : IDisposable
        {
            private ActivationInputBlockers owner;
            private IDisposable token;
            internal LinkedListNode<OwnedBlocker> Node;

            internal OwnedBlocker(ActivationInputBlockers owner, IDisposable token)
            {
                this.owner = owner;
                this.token = token;
            }

            public void Dispose()
            {
                var current = owner;
                if (current == null)
                {
                    return;
                }

                current.AssertThread();
                var releasing = token;
                current.active.Remove(Node);
                owner = null;
                token = null;
                Node = null;
                // 调用门控观察者前先解绑，防止重入清理重复释放。
                releasing.Dispose();
            }
        }
    }
}
