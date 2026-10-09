using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.ChildViews;

namespace MUI.UGUI
{
    public sealed partial class NestedViewElement
    {
        async ValueTask<IPreparedBindingTarget> IBindingRebindTarget.PrepareRebindAsync(
            IReadOnlyDictionary<string, object> values, CancellationToken cancellationToken)
        {
            RequireAlive();
            RequireAvailableNode();
            var bindsModel = values.TryGetValue(nameof(ViewModel), out var value);
            if (bindsModel && value != null && !(value is ViewModel))
            {
                throw new InvalidOperationException("Nested ViewModel binding must produce a ViewModel or null.");
            }

            var next = bindsModel ? (ViewModel)value : requestedModel;
            var parentScope = scope;
            var lifetime = parentLifetime;
            var request = version;
            var priorChange = pendingChange;
            if (parentScope == null || lifetime == null || !parentScope.IsActive)
            {
                throw new InvalidOperationException("Nested child has no active parent scope.");
            }

            if (priorChange != null)
            {
                await WaitForPreviousChangeAsync(priorChange, cancellationToken, lifetime.Token);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!IsAlive || request != version || !ReferenceEquals(scope, parentScope) ||
                !ReferenceEquals(parentLifetime, lifetime) || !parentScope.IsActive)
            {
                throw new OperationCanceledException("Nested child changed during rebind preparation.", cancellationToken);
            }

            var previous = childHandle;
            var unchanged = !bindsModel || (ReferenceEquals(DisplayedViewModel, next) &&
                (next != null || previous == null || !previous.IsActive));
            PreparedViewRebind staged = null;
            ChildViewHandle<ViewModel, Unit> candidate = null;
            try
            {
                if (!unchanged && next != null)
                {
                    BindingRegistry.GetManifest(next.GetType());
                    if (previous != null && previous.IsActive)
                    {
                        staged = await previous.PrepareStagedRebindAsync(next, cancellationToken);
                    }
                    else
                    {
                        candidate = await parentScope.PrepareAsync(template, provider, Unit.Value, next, cancellationToken);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                return new PreparedRebind(this, parentScope, lifetime, previous, priorChange,
                    request, next, bindsModel, unchanged, staged, candidate);
            }
            catch (Exception failure)
            {
                try
                {
                    if (staged != null)
                    {
                        await staged.DisposeAsync();
                    }

                    if (candidate != null)
                    {
                        await candidate.DisposeAsync();
                    }
                    else if (failure is ChildViewPreparationException preparation)
                    {
                        await preparation.CleanupCompletion;
                    }
                }
                catch (Exception cleanup)
                {
                    throw new AggregateException("Nested rebind preparation and cleanup failed.", failure, cleanup);
                }

                throw;
            }
        }

        private static async Task WaitForPreviousChangeAsync(Task change, CancellationToken callerToken,
            CancellationToken activationToken)
        {
            callerToken.ThrowIfCancellationRequested();
            activationToken.ThrowIfCancellationRequested();
            if (!change.IsCompleted)
            {
                var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (callerToken.Register(() => cancelled.TrySetResult(true)))
                using (activationToken.Register(() => cancelled.TrySetResult(true)))
                {
                    await Task.WhenAny(change, cancelled.Task);
                    callerToken.ThrowIfCancellationRequested();
                    activationToken.ThrowIfCancellationRequested();
                }
            }

            try
            {
                await change;
            }
            catch (Exception)
            {
                // 前次请求的失败属于其等待者；已排空的失败不阻止本次准备。
            }

            callerToken.ThrowIfCancellationRequested();
            activationToken.ThrowIfCancellationRequested();
        }
    }
}
