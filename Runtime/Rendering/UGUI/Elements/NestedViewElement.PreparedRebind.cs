using System;
using System.Threading.Tasks;
using MUI.ChildViews;

namespace MUI.UGUI
{
    public sealed partial class NestedViewElement
    {
        private sealed class PreparedRebind : IPreparedBindingTarget
        {
            private readonly NestedViewElement element;
            private readonly ChildViewScope scope;
            private readonly LifetimeScope lifetime;
            private readonly ChildViewHandle<ViewModel, Unit> previous;
            private readonly Task priorChange;
            private readonly long version;
            private readonly ViewModel model;
            private readonly bool bindsModel;
            private readonly bool unchanged;
            private readonly PreparedViewRebind staged;
            private ChildViewHandle<ViewModel, Unit> candidate;
            private Task<bool> retirement;
            private ViewModel originalModel;
            private bool started;
            private bool adopted;
            private bool committing;
            private int writes;

            internal PreparedRebind(NestedViewElement element, ChildViewScope scope, LifetimeScope lifetime,
                ChildViewHandle<ViewModel, Unit> previous, Task priorChange, long version,
                ViewModel model, bool bindsModel, bool unchanged, PreparedViewRebind staged,
                ChildViewHandle<ViewModel, Unit> candidate)
            {
                this.element = element;
                this.scope = scope;
                this.lifetime = lifetime;
                this.previous = previous;
                this.priorChange = priorChange;
                this.version = version;
                this.model = model;
                this.bindsModel = bindsModel;
                this.unchanged = unchanged;
                this.staged = staged;
                this.candidate = candidate;
            }

            public void Validate()
            {
                if (!element.IsAlive || !ReferenceEquals(element.scope, scope) ||
                    !ReferenceEquals(element.parentLifetime, lifetime) || version != element.version ||
                    !ReferenceEquals(element.childHandle, previous) ||
                    !ReferenceEquals(element.pendingChange, priorChange) || !scope.IsActive ||
                    (priorChange != null && !priorChange.IsCompleted))
                {
                    throw new OperationCanceledException("Nested content changed during rebind preparation.");
                }

                staged?.Validate();
                if (candidate != null && (!candidate.BelongsTo(scope) || candidate.State != ChildViewState.Prepared))
                {
                    throw new InvalidOperationException("Nested candidate is no longer prepared in its parent scope.");
                }
            }

            public void BeginCommit()
            {
                Validate();
                if (element.preparedRebind != null)
                {
                    throw new InvalidOperationException("Nested content already has a rebind commit.");
                }

                originalModel = element.requestedModel;
                element.preparedRebind = this;
                started = true;
            }

            internal void Write(ViewModel value)
            {
                if (!started || committing || !bindsModel || ++writes != 1)
                {
                    throw new InvalidOperationException("Nested ViewModel was changed during its rebind commit.");
                }

                if (value != null)
                {
                    BindingRegistry.GetManifest(value.GetType());
                }

                element.requestedModel = value;
            }

            public void Commit()
            {
                if (!started || !ReferenceEquals(element.preparedRebind, this) ||
                    writes != (bindsModel ? 1 : 0) || !ReferenceEquals(element.requestedModel, model))
                {
                    throw new InvalidOperationException("Nested binding writes differ from the prepared model.");
                }

                Validate();
                committing = true;
                // 前次失败归原调用者；本次成功保留同一模型也应发布独立的完成边界。
                var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                element.pendingChange = completion.Task;
                _ = ObserveAsync(completion.Task);
                if (!unchanged)
                {
                    var committedVersion = ++element.version;
                    // 发布信号后才调用子激活或关闭；项目回调必须观察到本轮任务。
                    if (staged != null)
                    {
                        try
                        {
                            staged.Commit();
                        }
                        finally
                        {
                            // 立即完成时直接读取已完成结果，避免预先挂起的观察者强制让出一帧。
                            _ = CompleteChangeAsync(CompleteStagedAsync(staged.Completion), completion);
                        }
                    }
                    else if (candidate != null)
                    {
                        try
                        {
                            candidate.Commit();
                            element.childHandle = candidate;
                            adopted = true;
                            completion.TrySetResult(true);
                        }
                        catch (Exception error)
                        {
                            completion.TrySetException(error);
                            throw;
                        }
                    }
                    else
                    {
                        try
                        {
                            if (previous != null && previous.IsActive)
                            {
                                previous.RequestClose();
                            }

                            retirement = CompleteRemovalAsync(element, previous, committedVersion);
                            _ = CompleteChangeAsync(retirement, completion);
                        }
                        catch (Exception error)
                        {
                            completion.TrySetException(error);
                            throw;
                        }
                    }
                }
                else
                {
                    completion.TrySetResult(true);
                }

                element.preparedRebind = null;
                if (!ReferenceEquals(originalModel, element.requestedModel))
                {
                    element.NotifyChanged(nameof(ViewModel));
                }
            }

            public async ValueTask DisposeAsync()
            {
                if (ReferenceEquals(element.preparedRebind, this))
                {
                    element.preparedRebind = null;
                }

                if (staged != null)
                {
                    await staged.DisposeAsync();
                }

                if (!adopted && candidate != null)
                {
                    await candidate.DisposeAsync();
                }

                if (retirement != null)
                {
                    await retirement;
                }

                candidate = null;
            }

            private static async Task<bool> CompleteStagedAsync(Task<RebindOutcome> completion)
            {
                var outcome = await completion;
                if (!outcome.IsApplied)
                {
                    if (outcome.Error != null)
                    {
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(outcome.Error).Throw();
                    }

                    throw new OperationCanceledException("Nested staged rebind did not apply.");
                }

                if (outcome.Error != null)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(outcome.Error).Throw();
                }

                return true;
            }

            private static async Task<bool> CompleteRemovalAsync(NestedViewElement element,
                ChildViewHandle<ViewModel, Unit> previous, long committedVersion)
            {
                try
                {
                    if (previous != null)
                    {
                        await previous.CleanupCompletion;
                    }

                    return true;
                }
                finally
                {
                    if (element.IsAlive && element.version == committedVersion &&
                        ReferenceEquals(element.childHandle, previous))
                    {
                        element.childHandle = null;
                    }
                }
            }
        }
    }
}
