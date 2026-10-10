using System;

namespace MUI
{
    internal static class ViewTickTiming
    {
        internal static float Interval(object presenter)
        {
            if (presenter is IViewTick && presenter is ILowFrequencyViewTick)
            {
                throw new InvalidOperationException("A Presenter must choose one tick frequency interface.");
            }

            if (!(presenter is ILowFrequencyViewTick low))
            {
                return 0;
            }

            var interval = low.TickInterval;
            if (interval <= 0 || float.IsNaN(interval) || float.IsInfinity(interval))
            {
                throw new InvalidOperationException("Low-frequency tick interval must be finite and positive.");
            }

            return interval;
        }

        internal static int CatchUpLimit(object presenter)
        {
            var maximum = presenter is ILowFrequencyViewTickCatchUp catchUp ? catchUp.MaxTickCatchUp : 1;
            if (maximum < 1 || maximum > 32)
            {
                throw new InvalidOperationException("Low-frequency tick catch-up limit must be between 1 and 32.");
            }
            return maximum;
        }

        internal static int Due(ref double remainder, float delta, float interval, int maximum)
        {
            if (interval == 0)
            {
                return 1;
            }

            remainder += delta;
            var whole = Math.Floor(remainder / interval);
            if (whole < 1)
            {
                return 0;
            }

            var due = (int)Math.Min(maximum, whole);
            remainder %= interval;
            return due;
        }
    }
}
