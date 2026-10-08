using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>
    /// 隔离的候选绑定快照及准备目标清理责任。提交前不订阅或写入当前 View；
    /// 清理按逆序排空目标，显式恢复只确认叶责任，不重复未知项目回调。
    /// </summary>
    internal sealed class BindingPreview : IPreparedBindingRebind, ICleanupResponsibilitySource
    {
        private readonly List<ReadCheck> checks = new List<ReadCheck>();
        private readonly List<TargetValues> targets = new List<TargetValues>();
        private readonly List<IPreparedBindingTarget> prepared = new List<IPreparedBindingTarget>();
        private readonly List<CleanupResponsibility> targetCleanup = new List<CleanupResponsibility>();
        private readonly List<CleanupResponsibility> unconfirmedCleanup = new List<CleanupResponsibility>();
        private bool committed;
        private bool cleanupStarted;

        internal BindingPreview(LifetimeScope owner)
        {
            var failureRecorded = false;
            CleanupResponsibility = new CleanupResponsibility(ReleasePreparedAsync, "BindingRebindPreparation", error =>
            {
                if (error != null && owner != null && !failureRecorded)
                {
                    failureRecorded = true;
                    owner.RecordCleanupFailure(error, CleanupResponsibility);
                }
            }, true, Thread.CurrentThread.ManagedThreadId);
        }

        public CleanupResponsibility CleanupResponsibility
        {
            get;
        }

        internal void Add(IElement element, string property, object value, Func<bool> stillCurrent)
        {
            AddCheck(property, stillCurrent);
            if (!(element is IBindingRebindTarget))
            {
                return;
            }

            TargetValues target = null;
            foreach (var entry in targets)
            {
                if (ReferenceEquals(entry.Element, element))
                {
                    target = entry;
                    break;
                }
            }

            if (target == null)
            {
                target = new TargetValues(element);
                targets.Add(target);
            }

            target.Values.Add(property, value);
        }

        internal void AddCheck(string property, Func<bool> stillCurrent) => checks.Add(new ReadCheck(property, stillCurrent));

        internal async ValueTask PrepareAsync(CancellationToken token)
        {
            foreach (var target in targets)
            {
                token.ThrowIfCancellationRequested();
                var candidate = await ((IBindingRebindTarget)target.Element).PrepareRebindAsync(target.Values, token);
                if (candidate == null)
                {
                    throw new InvalidOperationException("Binding rebind target returned no preparation.");
                }

                prepared.Add(candidate);
                // 取得稳定责任后再执行校验或提交；失败时保留原对象，不猜测其释放是否部分完成。
                targetCleanup.Add(CleanupRegistry.GetResponsibility(candidate, "BindingRebindTarget"));
            }
        }

        public void Validate()
        {
            foreach (var check in checks)
            {
                if (!check.StillCurrent())
                {
                    throw new OperationCanceledException($"Binding source for '{check.Property}' changed during rebind preparation.");
                }
            }

            foreach (var target in prepared)
            {
                target.Validate();
            }
        }

        public void BeginCommit()
        {
            foreach (var target in prepared)
            {
                target.BeginCommit();
            }
        }

        public void Commit()
        {
            foreach (var target in prepared)
            {
                target.Commit();
            }

            committed = true;
        }

        public ValueTask DisposeAsync() => CleanupResponsibility.DisposeAsync();

        private async ValueTask ReleasePreparedAsync()
        {
            if (cleanupStarted)
            {
                // 显式重试只确认已登记的叶责任，不重复调用未知项目清理回调。
                foreach (var responsibility in unconfirmedCleanup)
                {
                    if (responsibility.CaptureSnapshot().State != CleanupResponsibilityState.Completed)
                    {
                        throw new InvalidOperationException("Binding preparation cleanup dependencies are not confirmed.");
                    }
                }
                unconfirmedCleanup.Clear();
                return;
            }

            cleanupStarted = true;
            List<Exception> errors = null;
            for (var i = prepared.Count - 1; i >= 0; --i)
            {
                // 项目责任属性在登记时抛错也必须收尾该候选；此回退按未知且不可重试处理。
                var responsibility = i < targetCleanup.Count ? targetCleanup[i] :
                    new CleanupResponsibility(prepared[i].DisposeAsync, "BindingRebindTarget");
                try
                {
                    // 已公开责任的适配器仍从自身释放入口执行，保留其调用资格检查。
                    if (i < targetCleanup.Count)
                    {
                        await CleanupRegistry.ReleaseAsync(prepared[i], "BindingRebindTarget");
                    }
                    else
                    {
                        await responsibility.DisposeAsync();
                    }
                }
                catch (Exception error)
                {
                    unconfirmedCleanup.Add(responsibility);
                    if (errors == null)
                    {
                        errors = new List<Exception>();
                    }

                    errors.Add(error);
                }
            }

            prepared.Clear();
            targetCleanup.Clear();
            targets.Clear();
            checks.Clear();
            if (errors != null)
            {
                throw new AggregateException(committed ? "Committed binding cleanup failed." : "Binding preparation cleanup failed.", errors);
            }
        }

        private readonly struct ReadCheck
        {
            internal readonly string Property;
            internal readonly Func<bool> StillCurrent;

            internal ReadCheck(string property, Func<bool> stillCurrent)
            {
                Property = property;
                StillCurrent = stillCurrent;
            }
        }

        private sealed class TargetValues
        {
            internal readonly IElement Element;
            internal readonly Dictionary<string, object> Values = new Dictionary<string, object>(StringComparer.Ordinal);

            internal TargetValues(IElement element)
            {
                Element = element;
            }
        }
    }
}
