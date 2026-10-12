using System;
using System.Collections.Generic;

namespace MUI.Widgets
{
    public sealed partial class WidgetScope
    {
        private readonly List<WidgetHandle> tickHandles = new List<WidgetHandle>();
        private readonly List<WidgetHandle> tickSnapshot = new List<WidgetHandle>();
        private readonly List<WidgetHandle> refreshSnapshot = new List<WidgetHandle>();
        private bool ticking;
        private bool refreshingTicks;
        private bool refreshTicksPending;
        private bool publishedTickActivity;

        public event Action TickActivityChanged;

        public bool HasTicks => IsActive && tickHandles.Count != 0;

        internal void RefreshTicks()
        {
            RequireThread();
            refreshTicksPending = true;
            if (refreshingTicks)
            {
                return;
            }

            refreshingTicks = true;
            try
            {
                var passes = 0;
                while (refreshTicksPending)
                {
                    refreshTicksPending = false;
                    if (++passes > 32)
                    {
                        UIErrors.Report(new InvalidOperationException("Widget tick registration did not stabilize."));
                        break;
                    }

                    tickHandles.Clear();
                    if (IsActive)
                    {
                        refreshSnapshot.Clear();
                        refreshSnapshot.AddRange(handles);
                        foreach (var handle in refreshSnapshot)
                        {
                            try
                            {
                                if (handle.WantsTick && IsActive && handle.BelongsTo(this))
                                {
                                    tickHandles.Add(handle);
                                }
                            }
                            catch (Exception error)
                            {
                                UIErrors.Report(error);
                            }
                        }
                    }

                    var hasTicks = HasTicks;
                    if (publishedTickActivity == hasTicks)
                    {
                        continue;
                    }

                    publishedTickActivity = hasTicks;

                    var handlers = TickActivityChanged;
                    if (handlers == null)
                    {
                        continue;
                    }

                    foreach (Action handler in handlers.GetInvocationList())
                    {
                        try
                        {
                            handler();
                        }
                        catch (Exception error)
                        {
                            UIErrors.Report(error);
                        }
                    }
                }
            }
            finally
            {
                refreshSnapshot.Clear();
                refreshingTicks = false;
            }
        }

        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            RequireThread();
            PresenterUpdateClock.Validate(deltaTime, unscaledDeltaTime);

            if (!HasTicks || ticking)
            {
                return;
            }

            ticking = true;
            try
            {
                tickSnapshot.Clear();
                tickSnapshot.AddRange(tickHandles);
                foreach (var handle in tickSnapshot)
                {
                    try
                    {
                        if (!handle.WantsTick || !handle.BelongsTo(this))
                        {
                            continue;
                        }

                        handle.Tick(deltaTime, unscaledDeltaTime);
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
    }
}
