using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.UGUI
{
    /// <summary>单次 View 激活借用的资源加载配置，不拥有项目加载器。</summary>
    internal sealed partial class ViewResourceContext
    {
        internal readonly LifetimeScope Scope;
        internal readonly IResourceLoader Loader;
        internal readonly UIErrorContext DiagnosticContext;
        private readonly bool waitForSources;
        private Dictionary<object, Preparation> preparations;
        private bool committed;
        private bool released;
        private List<TaskCompletionSource<bool>> preparationWaiters;

        internal ViewResourceContext(LifetimeScope lifetime, IResourceLoader loader,
                    bool waitForSources)
        {
            Scope = lifetime;
            Loader = loader;
            DiagnosticContext = UIErrors.CurrentContext;
            this.waitForSources = waitForSources;
        }

        /// <summary>只追踪本 View 默认异步加载器的资源键；直接配置的槽由调用者协调。</summary>
        internal Action<Task<bool>> CreatePreparationTracker(object owner, LifetimeScope lifetime, string label)
        {
            if (!waitForSources || committed || released || Scope.IsEnded ||
                !ReferenceEquals(Scope, lifetime))
            {
                return null;
            }

            var displayName = label.Length > 256
                ? label.Substring(0, char.IsHighSurrogate(label[255]) ? 255 : 256)
                : label;
            return operation =>
            {
                if (committed || Scope.IsEnded)
                {
                    return;
                }

                if (preparations == null)
                {
                    preparations = new Dictionary<object, Preparation>();
                }

                if (preparations.TryGetValue(owner, out var preparation))
                {
                    preparation.Operation = operation;
                }
                else
                {
                    preparations.Add(owner, new Preparation(displayName, operation));
                }
                SignalPreparationChanged();
            };
        }

        internal bool TryCompletePreparation()
        {
            Scope.Token.ThrowIfCancellationRequested();
            if (released)
            {
                throw new ObjectDisposedException(nameof(ViewResourceContext));
            }

            if (preparations == null)
            {
                return true;
            }

            var completed = true;
            foreach (var preparation in preparations.Values)
            {
                var operation = preparation.Operation;
                if (!operation.IsCompleted)
                {
                    completed = false;
                    continue;
                }

                if (!operation.GetAwaiter().GetResult())
                {
                    throw new InvalidOperationException("首帧资源请求未提交，不能完成界面准备。");
                }
            }

            return completed;
        }

        internal async ValueTask CompletePreparationAsync(CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (TryCompletePreparation())
                {
                    return;
                }

                var changed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                if (preparationWaiters == null)
                {
                    preparationWaiters = new List<TaskCompletionSource<bool>>();
                }

                preparationWaiters.Add(changed);
                try
                {
                    using (token.Register(() => changed.TrySetResult(true)))
                    using (Scope.Token.Register(() => changed.TrySetResult(true)))
                    {
                        var pending = new List<Task>();
                        foreach (var preparation in preparations.Values)
                        {
                            var operation = preparation.Operation;
                            if (!operation.IsCompleted)
                            {
                                pending.Add(operation);
                            }
                        }

                        // 后台任务可能在上次检查后、收集前失败，此时已完成任务不会进入 pending。
                        // 等待前再读一次终态，避免该失败被另一项长期加载挡住。
                        token.ThrowIfCancellationRequested();
                        if (TryCompletePreparation())
                        {
                            return;
                        }

                        // 完成、换键或取消都会唤醒复核，不能被已经淘汰的旧加载长期挡住。
                        // 若收集期间全部完成，直接复核结果，不只等待没有后续通知的变化信号。
                        if (pending.Count != 0)
                        {
                            pending.Add(changed.Task);
                            await Task.WhenAny(pending);
                        }
                    }
                }
                finally
                {
                    preparationWaiters.Remove(changed);
                    if (preparationWaiters.Count == 0)
                    {
                        preparationWaiters = null;
                    }
                }
            }
        }

        private void SignalPreparationChanged()
        {
            if (preparationWaiters == null)
            {
                return;
            }

            foreach (var waiter in preparationWaiters)
            {
                waiter.TrySetResult(true);
            }
        }

        internal void CommitPreparation()
        {
            if (!TryCompletePreparation())
            {
                throw new InvalidOperationException("首帧资源在提交期间发生变化。");
            }

            committed = true;
            preparations = null;
            SignalPreparationChanged();
        }

        internal void ReleasePreparation()
        {
            released = true;
            committed = true;
            preparations = null;
            SignalPreparationChanged();
        }

        private sealed class Preparation
        {
            internal readonly string Name;
            internal Task<bool> Operation;

            internal Preparation(string name, Task<bool> operation)
            {
                Name = name;
                Operation = operation;
            }
        }
    }
}
