using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private readonly Dictionary<ViewInstance, ExitFrame> exiting = new Dictionary<ViewInstance, ExitFrame>();
        private readonly List<ExitFrame> exitSnapshot = new List<ExitFrame>();

        private Task StartExit(ViewInstance instance, bool eligible, List<Exception> errors)
        {
            if (!eligible || IsShutdown || !(instance.View is IExitTransitionView transition) ||
                (userPreferences != null && userPreferences.ReducedMotion))
            {
                return Task.CompletedTask;
            }

            float duration;
            try
            {
                using (EnterCallback(instance))
                {
                    if (transition is IVisibilityView visibility && !visibility.IsVisible)
                    {
                        return Task.CompletedTask;
                    }

                    duration = transition.ExitDuration;
                }

                if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0)
                {
                    throw new InvalidOperationException("Exit duration must be finite and nonnegative.");
                }
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
                return Task.CompletedTask;
            }

            if (duration == 0 || IsShutdown)
            {
                return Task.CompletedTask;
            }

            var frame = new ExitFrame
            {
                Instance = instance,
                View = transition,
                Duration = duration,
                Errors = errors,
                InitialErrorCount = errors.Count,
                Trace = BeginOperationPhaseTrace(instance, NavigationOperationStage.ExitTransition, CurrentTraceOperation)
            };
            // 在调用渲染器前登记，宿主销毁重入时也能找到并结束此转场。
            exiting.Add(instance, frame);
            instance.ExitPending = true;
            try
            {
                using (EnterCallback(instance))
                {
                    transition.BeginVisualRetention();
                    if (instance.ExitPending && !IsShutdown)
                    {
                        transition.SampleExit(0);
                    }
                }

                if (IsShutdown)
                {
                    FinishExit(instance);
                }
            }
            catch (Exception error)
            {
                FinishExit(instance, error);
            }

            return frame.Completion.Task;
        }

        private void AdvanceExit(float delta)
        {
            exitSnapshot.Clear();
            exitSnapshot.AddRange(exiting.Values);
            try
            {
                foreach (var frame in exitSnapshot)
                {
                    var instance = frame.Instance;
                    if (!instance.ExitPending)
                    {
                        continue;
                    }

                    if (IsShutdown || (userPreferences != null && userPreferences.ReducedMotion))
                    {
                        FinishExit(instance);
                        continue;
                    }

                    frame.Elapsed += delta;
                    if (frame.Elapsed >= frame.Duration && frame.Duration <= instance.Route.Policy.ExitTimeout)
                    {
                        FinishExit(instance);
                        continue;
                    }

                    if (frame.Elapsed >= instance.Route.Policy.ExitTimeout)
                    {
                        FinishExit(instance, new TimeoutException("Exit transition exceeded its frame-time budget."));
                        continue;
                    }

                    try
                    {
                        using (EnterCallback(instance))
                        {
                            frame.View.SampleExit((float)(frame.Elapsed / frame.Duration));
                        }
                    }
                    catch (Exception error)
                    {
                        FinishExit(instance, error);
                    }
                }
            }
            finally
            {
                exitSnapshot.Clear();
            }
        }

        private void FinishExit(ViewInstance instance, Exception degradation = null)
        {
            if (!exiting.TryGetValue(instance, out var frame))
            {
                return;
            }

            instance.ExitPending = false;
            exiting.Remove(instance);
            try
            {
                try
                {
                    if (frame.View.IsAlive)
                    {
                        using (EnterCallback(instance))
                        {
                            frame.View.FinishExit();
                        }
                    }
                }
                catch (Exception error)
                {
                    frame.Errors.Add(error);
                    frame.Trace?.Fail(error);
                }

                CompleteVisualExit(instance, frame.Errors, frame.View);
                if (degradation == null && frame.Errors.Count == frame.InitialErrorCount)
                {
                    frame.Trace?.Complete();
                }
                else
                {
                    frame.Trace?.Fail(degradation ?? frame.Errors[frame.Errors.Count - 1]);
                }
            }
            catch (Exception error)
            {
                frame.Trace?.Fail(error);
                throw;
            }
            finally
            {
                frame.Trace?.Dispose();
            }

            frame.Completion.TrySetResult(true);
            // 诊断观察者可以再次导航，必须先撤销旧屏障并完成视觉收尾。
            if (degradation != null)
            {
                UIErrors.Report(degradation);
            }
        }

        private void CompleteVisualExit(ViewInstance instance, List<Exception> errors, IVisualRetentionView retained = null)
        {
            activeOrder.Remove(instance);
            instance.HiddenBy = default;
            instance.BlockedBy = default;
            instance.HostVisible = false;
            instance.HostInteractable = false;
            // 所有收尾步骤独立尝试。缓慢的 OnClose 或资源清理不再占用模态屏障。
            void Attempt(Action action)
            {
                try
                {
                    using (EnterCallback(instance))
                    {
                        action();
                    }
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            Attempt(() =>
            {
                if (instance.View != null && instance.View.IsAlive)
                {
                    instance.View.SetHostState(false, false);
                }
            });
            Attempt(() =>
            {
                if (retained != null && retained.IsAlive)
                {
                    retained.EndVisualRetention();
                }
            });
            Attempt(() =>
            {
                if (instance.View != null && instance.View.IsAlive && instance.View is IModalView modal)
                {
                    modal.SetModalBarrier(false);
                }
            });
            Attempt(RecomputePresentation);
        }

        private sealed class ExitFrame
        {
            internal ViewInstance Instance;
            internal IExitTransitionView View;
            internal float Duration;
            internal double Elapsed;
            internal List<Exception> Errors;
            internal int InitialErrorCount;
            internal OperationPhaseTraceScope Trace;
            internal readonly TaskCompletionSource<bool> Completion =
                            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
