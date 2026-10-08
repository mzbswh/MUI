using System;
using System.Collections.Generic;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private readonly List<ViewInstance> tickInstances = new List<ViewInstance>();
        private readonly List<ViewInstance> tickSnapshot = new List<ViewInstance>();
        private bool ticking;

        /// <summary>UIHost 提供非缩放帧时长；其他宿主必须确保只有一个 Tick 驱动器。</summary>
        public void Tick(float unscaledDeltaTime)
        {
            AssertThread();
            if (unscaledDeltaTime < 0 || float.IsNaN(unscaledDeltaTime) || float.IsInfinity(unscaledDeltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
            }

            if (IsShutdown || IsReentrant || ticking)
            {
                return;
            }

            ticking = true;
            tickSnapshot.Clear();
            tickSnapshot.AddRange(tickInstances);
            try
            {
                TickCache();
                AdvanceEnter(unscaledDeltaTime);
                AdvanceExit(unscaledDeltaTime);
                foreach (var instance in tickSnapshot)
                {
                    try
                    {
                        if (!CanTick(instance))
                        {
                            continue;
                        }

                        if (instance.HasTick)
                        {
                            var count = ViewTickTiming.Due(ref instance.TickRemainder, unscaledDeltaTime, instance.TickInterval, instance.Route.Policy.MaxTickCatchUp);
                            for (var i = 0; i < count && CanTick(instance); i++)
                            {
                                InvokeTick(instance, instance.TickInterval == 0 ? unscaledDeltaTime : instance.TickInterval);
                            }
                        }

                        if (CanTick(instance) && instance.View is IChildTickHost children && children.HasChildTicks)
                        {
                            try
                            {
                                using (EnterCallback(instance))
                                {
                                    children.TickChildren(unscaledDeltaTime);
                                }
                            }
                            catch (Exception error)
                            {
                                UIErrors.Report(error);
                            }
                        }
                    }
                    catch (Exception error)
                    {
                        UIErrors.Report(error);
                    }
                }
            }
            finally
            {
                tickSnapshot.Clear();
                ticking = false;
            }
        }

        internal void RefreshTickRegistration(ViewInstance instance)
        {
            AssertThread();
            var required = instance.IsActive && instance.ActivationCommitted && (instance.HasTick || (instance.View is IChildTickHost children && children.HasChildTicks));
            if (required)
            {
                if (!tickInstances.Contains(instance))
                {
                    tickInstances.Add(instance);
                }
            }
            else
            {
                tickInstances.Remove(instance);
            }
        }

        private bool CanTick(ViewInstance instance)
        {
            if (IsShutdown || instance.IsRebinding || !instance.IsActive || !instance.ActivationCommitted || instance.EnterPending || !entries.TryGetValue(instance.Handle, out var current) || !ReferenceEquals(current, instance))
            {
                return false;
            }

            var pause = instance.Route.Policy.TickPause;
            if ((pause & TickPausePolicy.Covered) != 0 && instance.Covered)
            {
                return false;
            }

            if ((pause & TickPausePolicy.Hidden) == 0)
            {
                return true;
            }

            return instance.HostVisible && (!(instance.View is IVisibilityView visibility) || visibility.IsVisible);
        }

        private void InvokeTick(ViewInstance instance, float delta)
        {
            try
            {
                using (EnterCallback(instance))
                {
                    instance.InvokeTick(delta);
                }
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }
    }
}
