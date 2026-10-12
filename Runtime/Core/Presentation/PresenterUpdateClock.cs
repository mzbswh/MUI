using System;

namespace MUI
{
    internal sealed class PresenterUpdateClock
    {
        private readonly UpdateOptions options;
        private double elapsed, unscaledElapsed, pending, unscaledPending, remainder;
        internal PresenterUpdateClock(UpdateOptions options)
        {
            this.options = options;
        }
        internal void Reset()
        {
            elapsed = unscaledElapsed = pending = unscaledPending = remainder = 0;
        }

        internal bool Advance(float deltaTime, float unscaledDeltaTime, out UpdateTime time)
        {
            elapsed += deltaTime;
            unscaledElapsed += unscaledDeltaTime;
            pending += deltaTime;
            unscaledPending += unscaledDeltaTime;
            var delta = options.TimeMode == UpdateTimeMode.Scaled ? deltaTime : unscaledDeltaTime;
            remainder += delta;
            time = default;
            if (delta <= 0 || (options.Interval > 0 && remainder < options.Interval))
                return false;
            if (options.Interval > 0)
                remainder %= options.Interval;
            else
                remainder = 0;
            time = new UpdateTime(pending, unscaledPending, elapsed, unscaledElapsed);
            pending = unscaledPending = 0;
            return true;
        }

        internal static void Validate(float deltaTime, float unscaledDeltaTime)
        {
            if (deltaTime < 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (unscaledDeltaTime < 0 || float.IsNaN(unscaledDeltaTime) || float.IsInfinity(unscaledDeltaTime))
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
        }
    }
}
