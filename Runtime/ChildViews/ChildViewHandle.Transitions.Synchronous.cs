using System;
using System.Collections.Generic;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewHandle<TViewModel, TArgs> where TViewModel : ViewModel
    {
        private bool synchronousTransition;

        private void BeginSynchronousTransition(ChildViewState state)
        {
            synchronousTransition = true;
            State = state;
        }

        internal override void DeactivateCore()
        {
            RequireSynchronousTransition();
            if ((State != ChildViewState.Active && State != ChildViewState.Retained) || !bindingsCommitted)
            {
                throw new InvalidOperationException("Only a committed child view can deactivate synchronously.");
            }

            BeginSynchronousTransition(ChildViewState.Deactivating);
            try
            {
                Owner.RefreshTicks();
                StopForParentClose();
                if (view is IVisualRetentionView retained)
                {
                    Invoke(retained.EndVisualRetention);
                }

                var errors = new List<Exception>();
                EndActivation(errors);
                if (errors.Count != 0)
                {
                    throw new AggregateException("Synchronous child deactivation cleanup failed.", errors);
                }

                Owner.RequireActive();
                if (State != ChildViewState.Deactivating || closeStarted)
                {
                    throw new OperationCanceledException("Child view closed during synchronous deactivation.");
                }

                State = ChildViewState.Inactive;
            }
            catch (Exception error)
            {
                RecordTransitionFailure(error);
                throw;
            }
            finally
            {
                synchronousTransition = false;
            }
        }

        internal override void PrepareReactivationCore()
        {
            RequireSynchronousTransition();
            if (State != ChildViewState.Retained && State != ChildViewState.Inactive)
            {
                throw new InvalidOperationException("Only a retained or inactive child view can reactivate synchronously.");
            }

            BeginSynchronousTransition(ChildViewState.Preparing);
            try
            {
                RequireContentCurrent();
                view.SetHostState(false, false);
                if (view is IVisualRetentionView retained)
                {
                    Invoke(retained.EndVisualRetention);
                }

                var errors = new List<Exception>();
                EndActivation(errors);
                if (errors.Count != 0)
                {
                    throw new AggregateException("Previous synchronous child activation cleanup failed.", errors);
                }

                RequireReactivationCurrent(Owner.Token);
                BeginFreshActivation();
                Prepare();
                RequireReactivationCurrent(activation.Token);
                FinishPreparation();
            }
            catch (Exception error)
            {
                RecordTransitionFailure(error);
                throw;
            }
            finally
            {
                synchronousTransition = false;
            }
        }

        private void RequireSynchronousTransition()
        {
            Owner.RequireActive();
            if (Owner.Mode != LifetimeMode.Synchronous || IsExecuting || committing
                || Owner.HasVisualRetention || !CanCloseSynchronously)
            {
                throw new InvalidOperationException("Synchronous child transitions require an idle synchronous lifecycle.");
            }
        }
    }
}
