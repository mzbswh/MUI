using System;
using System.Collections.Generic;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewScope
    {
        private readonly List<ChildViewHandle> tickHandles = new List<ChildViewHandle>();
        private readonly List<ChildViewHandle> tickSnapshot = new List<ChildViewHandle>();
        private readonly List<ChildViewHandle> refreshSnapshot = new List<ChildViewHandle>();
        private bool ticking;
        private bool refreshingTicks;
        private bool refreshTicksPending;
        private bool publishedTickActivity;

        public event Action TickActivityChanged;

        public bool HasTicks => IsActive && (tickHandles.Count != 0 || synchronousCloseRequests.Count != 0);

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
                        UIErrors.Report(new InvalidOperationException("ChildView tick registration did not stabilize."));
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

        public void Tick(float unscaledDeltaTime)
        {
            RequireThread();
            if (unscaledDeltaTime < 0 || float.IsNaN(unscaledDeltaTime) || float.IsInfinity(unscaledDeltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
            }

            if (!HasTicks || ticking)
            {
                return;
            }

            ticking = true;
            try
            {
                Pump();
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

                        handle.Tick(unscaledDeltaTime);
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
