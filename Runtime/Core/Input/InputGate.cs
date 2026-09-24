using System;
using System.Collections.Generic;
using System.Threading;

namespace MUI
{
    /// <summary>仅限 UI 线程的输入阻挡器，每个令牌只释放自己预留的阻挡。</summary>
    public sealed class InputGate : IDisposable
    {
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly Dictionary<object, string> blockers = new Dictionary<object, string>();
        private bool disposed;
        private bool publishing;
        private bool publishPending;
        private long version;

        /// <summary>阻挡状态变化通知；重入时发布最新状态，观察者异常隔离报告。</summary>
        public event Action Changed;

        /// <summary>未销毁且没有任何阻挡时为 true。</summary>
        public bool IsOpen
        {
            get
            {
                AssertThread();
                return !disposed && blockers.Count == 0;
            }
        }

        /// <summary>当前尚未释放的阻挡数量。</summary>
        public int BlockerCount
        {
            get
            {
                AssertThread();
                return blockers.Count;
            }
        }

        /// <summary>当前阻挡原因的只读副本，供诊断使用。</summary>
        public IReadOnlyList<string> BlockerReasons
        {
            get
            {
                AssertThread();
                return new List<string>(blockers.Values).AsReadOnly();
            }
        }

        /// <summary>复制最多指定数量的阻挡原因；释放后的门控仍可读取，且不会发出 Changed 通知。</summary>
        public InputGateSnapshot CaptureSnapshot(int maxReasons = 128)
        {
            AssertThread();
            if (maxReasons < 1 || maxReasons > 4096)
            {
                throw new ArgumentOutOfRangeException(nameof(maxReasons), "输入原因采集上限必须在 1 至 4096 之间。");
            }
            var reasons = new string[Math.Min(blockers.Count, maxReasons)];
            var index = 0;
            foreach (var reason in blockers.Values)
            {
                if (index == reasons.Length)
                {
                    break;
                }
                reasons[index++] = reason;
            }
            return new InputGateSnapshot(disposed, blockers.Count, reasons);
        }

        /// <summary>登记独立阻挡并通知；释放返回令牌只移除此阻挡，必须在所属 UI 线程操作。</summary>
        public IDisposable Block(string reason)
        {
            AssertThread();
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(InputGate));
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("A blocker reason is required.", nameof(reason));
            }

            var token = new Blocker(this);
            blockers.Add(token, reason);
            Notify();
            if (disposed)
            {
                token.Dispose();
                throw new ObjectDisposedException(nameof(InputGate), "输入门在阻挡登记期间已被释放。");
            }

            return token;
        }

        private void Release(object token)
        {
            AssertThread();
            if (blockers.Remove(token))
            {
                Notify();
            }
        }

        private void Notify()
        {
            ++version;
            publishPending = true;
            if (publishing)
            {
                return;
            }

            publishing = true;
            try
            {
                var passes = 0;
                while (publishPending)
                {
                    if (++passes > 32)
                    {
                        UIErrors.Report(new InvalidOperationException("输入门状态在 32 次通知后仍未稳定。"));
                        break;
                    }

                    publishPending = false;
                    var currentVersion = version;
                    var handlers = Changed;
                    if (handlers == null)
                    {
                        continue;
                    }

                    foreach (Action handler in handlers.GetInvocationList())
                    {
                        if (version != currentVersion)
                        {
                            break;
                        }

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
            }
            finally
            {
                publishing = false;
                publishPending = false;
                if (disposed)
                {
                    Changed = null;
                }
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
            blockers.Clear();
            Notify();
        }

        private void AssertThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("InputGate must be accessed on its owning UI thread.");
            }
        }

        private sealed class Blocker : IDisposable
        {
            private InputGate owner;

            public Blocker(InputGate owner)
            {
                this.owner = owner;
            }

            public void Dispose()
            {
                var current = owner;
                if (current == null)
                {
                    return;
                }

                current.AssertThread();
                owner = null;
                current.Release(this);
            }
        }
    }
}
