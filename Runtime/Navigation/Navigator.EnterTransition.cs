using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private readonly List<ViewInstance> entering = new List<ViewInstance>();
        private readonly List<ViewInstance> enterSnapshot = new List<ViewInstance>();
        private readonly Dictionary<ViewInstance, OperationPhaseTraceScope> enteringTrace =
            new Dictionary<ViewInstance, OperationPhaseTraceScope>();

        private void StartEnter(ViewInstance instance)
        {
            if (!(instance.View is IEnterTransitionView transition))
            {
                instance.EnterTransition.TrySetResult(new ViewTransition(ViewTransitionStatus.Completed));
                return;
            }

            // 调用渲染器前先登记，确保同步关闭也能结束转场。
            instance.EnterPending = true;
            entering.Add(instance);
            try
            {
                var phase = BeginOperationPhaseTrace(instance, NavigationOperationStage.EnterTransition,
                    CurrentTraceOperation);
                if (phase != null)
                {
                    enteringTrace.Add(instance, phase);
                }

                float duration;
                using (EnterCallback(instance))
                {
                    duration = transition.EnterDuration;
                }

                if (!instance.IsActive || !instance.EnterPending)
                {
                    return;
                }

                if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0)
                {
                    throw new InvalidOperationException("Enter duration must be finite and nonnegative.");
                }

                instance.EnterDuration = duration;
                if (duration == 0 || (userPreferences != null && userPreferences.ReducedMotion))
                {
                    FinishEnter(instance);
                    return;
                }

                using (EnterCallback(instance))
                {
                    transition.SampleEnter(0);
                }
            }
            catch (Exception error)
            {
                FinishEnter(instance, error);
            }
        }

        private void AdvanceEnter(float delta)
        {
            enterSnapshot.Clear();
            enterSnapshot.AddRange(entering);
            try
            {
                foreach (var instance in enterSnapshot)
                {
                    if (!instance.IsActive || !instance.EnterPending)
                    {
                        continue;
                    }

                    if (userPreferences != null && userPreferences.ReducedMotion)
                    {
                        FinishEnter(instance);
                        continue;
                    }

                    instance.EnterElapsed += delta;
                    if (instance.EnterElapsed >= instance.EnterDuration && instance.EnterDuration <= instance.Route.Policy.EnterTimeout)
                    {
                        FinishEnter(instance);
                        continue;
                    }

                    if (instance.EnterElapsed >= instance.Route.Policy.EnterTimeout)
                    {
                        FinishEnter(instance, new TimeoutException("Enter transition exceeded its frame-time budget."));
                        continue;
                    }

                    try
                    {
                        using (EnterCallback(instance))
                        {
                            ((IEnterTransitionView)instance.View).SampleEnter((float)(instance.EnterElapsed / instance.EnterDuration));
                        }
                    }
                    catch (Exception error)
                    {
                        FinishEnter(instance, error);
                    }
                }
            }
            finally
            {
                enterSnapshot.Clear();
            }
        }

        private void FinishEnter(ViewInstance instance, Exception degradation = null)
        {
            if (!instance.EnterPending)
            {
                return;
            }

            instance.EnterFinishing = true;
            instance.EnterPending = false;
            entering.Remove(instance);
            enteringTrace.TryGetValue(instance, out var phase);
            enteringTrace.Remove(instance);
            instance.EnterDegradation = degradation;
            presentationDeferrals++;
            try
            {
                if (degradation != null)
                {
                    phase?.Fail(degradation);
                    UIErrors.Report(degradation);
                }

                if (instance.View != null && instance.View.IsAlive && instance.View is IEnterTransitionView transition)
                {
                    using (EnterCallback(instance))
                    {
                        transition.FinishEnter();
                    }
                }
            }
            catch (Exception error)
            {
                // 无法恢复稳定状态属于渲染失败，不能视为转场降级成功。
                phase?.Fail(error);
                instance.SetFailure(error);
                UIErrors.Report(error);
                CloseAfterFailure(instance, DismissReason.OpenFailed);
            }
            finally
            {
                try
                {
                    presentationDeferrals--;
                    RecomputePresentation();
                    if (degradation == null && instance.Failure == null)
                    {
                        phase?.Complete();
                    }
                    else
                    {
                        phase?.Fail(degradation ?? instance.Failure);
                    }
                }
                catch (Exception error)
                {
                    phase?.Fail(error);
                    instance.EnterTransition.TrySetResult(new ViewTransition(ViewTransitionStatus.Failed, error));
                    throw;
                }
                finally
                {
                    var status = instance.Failure != null ? ViewTransitionStatus.Failed :
                        instance.IsActive ? ViewTransitionStatus.Completed : ViewTransitionStatus.Interrupted;
                    instance.EnterTransition.TrySetResult(new ViewTransition(status, instance.Failure ?? degradation));
                    instance.EnterFinishing = false;
                    phase?.Dispose();
                }
            }
        }

        private async Task WaitForReadinessAsync(ViewInstance instance, CancellationToken token)
        {
            // 已发布的首次就绪不被后到的取消改写，等待也不改变进入转场。
            if (instance.TryGetReadiness(out _))
            {
                return;
            }

            await AsyncWait.WithCancellation(instance.ReadinessCompletion, token);
        }
    }
}
