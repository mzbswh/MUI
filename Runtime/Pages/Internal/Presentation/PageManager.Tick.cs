using MUI.Pages.Diagnostics;
using System;
using System.Collections.Generic;

namespace MUI.Pages
{
    public sealed partial class PageManager
    {
        private readonly List<PageInstance> tickInstances = new List<PageInstance>();
        private readonly List<PageInstance> tickSnapshot = new List<PageInstance>();
        private bool ticking;

        /// <summary>UIHost 提供缩放与非缩放帧时长；其他宿主必须确保只有一个 Tick 驱动器。</summary>
        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            AssertThread();
            PresenterUpdateClock.Validate(deltaTime, unscaledDeltaTime);

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
                            InvokeTick(instance, deltaTime, unscaledDeltaTime);
                        }

                        if (CanTick(instance) && instance.View is IWidgetTickHost children && children.HasWidgetTicks)
                        {
                            try
                            {
                                using (EnterCallback(instance))
                                {
                                    children.TickChildren(deltaTime, unscaledDeltaTime);
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

        internal void RefreshTickRegistration(PageInstance instance)
        {
            AssertThread();
            var required = instance.IsActive && instance.ActivationCommitted && (instance.HasTick || (instance.View is IWidgetTickHost children && children.HasWidgetTicks));
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

        private bool CanTick(PageInstance instance)
        {
            instance.LastTickStatus = EvaluateTick(instance);
            return instance.LastTickStatus == PageTickStatus.Eligible;
        }

        private PageTickStatus EvaluateTick(PageInstance instance)
        {
            if (IsShutdown)
            {
                return PageTickStatus.HostShutdown;
            }
            if (!instance.IsActive || !instance.ActivationCommitted ||
                !entries.TryGetValue(instance.Handle, out var current) || !ReferenceEquals(current, instance))
            {
                return PageTickStatus.Inactive;
            }
            if (instance.IsRebinding)
            {
                return PageTickStatus.Rebinding;
            }
            if (instance.EnterPending)
            {
                return PageTickStatus.Entering;
            }
            var pause = instance.Definition.Policy.TickPause;
            if ((pause & TickPausePolicy.Covered) != 0 && instance.Covered)
            {
                return PageTickStatus.Covered;
            }
            if ((pause & TickPausePolicy.Hidden) != 0 &&
                (!instance.HostVisible || (instance.View is IVisibilityView visibility && !visibility.IsVisible)))
            {
                return PageTickStatus.Hidden;
            }
            return PageTickStatus.Eligible;
        }

        private void InvokeTick(PageInstance instance, float deltaTime, float unscaledDeltaTime)
        {
            try
            {
                using (EnterCallback(instance))
                {
                    instance.InvokeTick(deltaTime, unscaledDeltaTime);
                }
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }
    }
}
