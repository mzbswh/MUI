using System;
using System.Collections.Generic;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private readonly Queue<(NavigationEvent snapshot, ViewInstance source)> lifecycleEvents = new Queue<(NavigationEvent, ViewInstance)>();
        private const int LifecycleEventCapacity = 256;
        private bool dispatchingEvents;
        private bool reportedEventOverflow;
        private long eventSequence;
        private long commitVersion;

        public event Action<NavigationEvent> LifecycleChanged;

        public long DroppedLifecycleEventCount
        {
            get; private set;
        }

        private void QueueLifecycleEvent(ViewInstance instance,
                    NavigationEventKind kind,
                    DismissReason? reason = null,
                    CloseOutcome? outcome = null,
                    ArgsUpdateOutcome? argsUpdate = null,
                    RebindOutcome? rebind = null,
                    DependencyFailure? dependencyFailure = null)
        {
            var sequence = ++eventSequence;
            var snapshot = new NavigationEvent(kind, instance.Route, instance.Handle, sequence,
                instance.CommitVersion, reason, outcome, argsUpdate, rebind, dependencyFailure);
            // 追踪记录实际事件发生点，不受外部订阅或通知队列溢出影响。
            RecordLifecycleTrace(snapshot);
            if (LifecycleChanged == null)
            {
                return;
            }

            if (lifecycleEvents.Count >= LifecycleEventCapacity)
            {
                ++DroppedLifecycleEventCount;
                reportedEventOverflow = true;
                return;
            }

            lifecycleEvents.Enqueue((snapshot, instance));
        }

        private void DispatchLifecycleEvents()
        {
            if (dispatchingEvents)
            {
                return;
            }

            dispatchingEvents = true;
            try
            {
                while (lifecycleEvents.Count != 0 || reportedEventOverflow)
                {
                    if (lifecycleEvents.Count == 0)
                    {
                        reportedEventOverflow = false;
                        UIErrors.Report(new InvalidOperationException("Navigation lifecycle event buffer overflowed; inspect DroppedLifecycleEventCount."));
                        continue;
                    }

                    var entry = lifecycleEvents.Dequeue();
                    var handlers = LifecycleChanged;
                    if (handlers == null)
                    {
                        continue;
                    }

                    using (EnterCallback(entry.source))
                    {
                        foreach (Action<NavigationEvent> handler in handlers.GetInvocationList())
                        {
                            try
                            {
                                handler(entry.snapshot);
                            }
                            catch (Exception error)
                            {
                                UIErrors.Report(error);
                            }
                        }
                    }
                }
            }
            finally
            {
                dispatchingEvents = false;
            }
        }
    }
}
