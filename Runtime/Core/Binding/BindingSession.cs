using System;
using System.Collections.Generic;

namespace MUI
{
    internal sealed partial class BindingSession
    {
        private readonly List<Action> detach = new List<Action>();
        private readonly List<Action> finalizers = new List<Action>();
        private readonly List<Exception> detachErrors = new List<Exception>();
        private bool subscriptionsDetached;
        private bool finalized;
        private readonly List<Action> readyActions = new List<Action>();
        private readonly List<Action> initialWrites = new List<Action>();

        public BindingSession(LifetimeMode mode)
        {
            Commands = new Lifetime(mode);
        }

        public Lifetime Commands
        {
            get;
        }

        public ICommandTarget CommandTarget
        {
            get; set;
        }

        public bool HasAsynchronousCommands
        {
            get; set;
        }

        public bool IsActive { get; private set; } = true;

        public bool SourcesCommitted
        {
            get; private set;
        }

        public bool IsReady
        {
            get; private set;
        }

        public void AddDetach(Action action) => detach.Add(action);

        public void AddFinalizer(Action action) => finalizers.Add(action);

        public void StageSourceWrite(Action action) => initialWrites.Add(action);

        public void OnReady(Action action) => readyActions.Add(action);

        public void CommitSources()
        {
            if (!IsActive)
            {
                throw new InvalidOperationException("Binding session has ended.");
            }

            if (SourcesCommitted)
            {
                return;
            }

            // 先设置标记，设置器可能同步发出变化通知。
            SourcesCommitted = true;
            try
            {
                // 来源设置器或就绪回调内部的解绑可能清空这些列表。
                for (var i = 0; IsActive && i < initialWrites.Count; ++i)
                {
                    initialWrites[i]();
                }

                if (!IsActive)
                {
                    return;
                }

                IsReady = true;
                for (var i = 0; IsActive && i < readyActions.Count; ++i)
                {
                    readyActions[i]();
                }
            }
            finally
            {
                initialWrites.Clear();
                readyActions.Clear();
            }
        }

        public void Freeze()
        {
            DetachSubscriptions();
            if (detachErrors.Count > 0)
            {
                throw new AggregateException("Binding freeze could not detach every subscription.", detachErrors);
            }
        }

        private void DetachSubscriptions()
        {
            if (subscriptionsDetached)
            {
                return;
            }

            subscriptionsDetached = true;
            IsActive = false;
            IsReady = false;
            readyActions.Clear();
            initialWrites.Clear();
            targetWriters.Clear();
            sourceWriters.Clear();
            for (var i = detach.Count - 1; i >= 0; --i)
            {
                try
                {
                    detach[i]();
                }
                catch (Exception error)
                {
                    detachErrors.Add(error);
                }
            }

            detach.Clear();
            Commands.Cancel();
        }

        public void Detach()
        {
            if (finalized)
            {
                return;
            }

            DetachSubscriptions();
            finalized = true;
            var errors = new List<Exception>(detachErrors);
            detachErrors.Clear();
            for (var i = finalizers.Count - 1; i >= 0; --i)
            {
                try
                {
                    finalizers[i]();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            finalizers.Clear();
            if (errors.Count > 0)
            {
                throw new AggregateException("Binding detachment failed.", errors);
            }
        }
    }
}
