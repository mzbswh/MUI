using System;

namespace MUI
{
    public enum UpdateTimeMode
    {
        Unscaled, Scaled
    }

    /// <summary>Presenter 更新配置；默认禁用，间隔为零表示每帧。实例创建时读取。</summary>
    public readonly struct UpdateOptions
    {
        public bool Enabled
        {
            get;
        }
        public float Interval
        {
            get;
        }
        public UpdateTimeMode TimeMode
        {
            get;
        }

        public UpdateOptions(float interval, UpdateTimeMode timeMode = UpdateTimeMode.Unscaled)
        {
            if (interval < 0 || float.IsNaN(interval) || float.IsInfinity(interval))
                throw new ArgumentOutOfRangeException(nameof(interval));
            if (timeMode != UpdateTimeMode.Unscaled && timeMode != UpdateTimeMode.Scaled)
                throw new ArgumentOutOfRangeException(nameof(timeMode));
            Enabled = true;
            Interval = interval;
            TimeMode = timeMode;
        }

        public static UpdateOptions Disabled => default;
        public static UpdateOptions EveryFrame => new UpdateOptions(0);
        public static UpdateOptions Every(float seconds, UpdateTimeMode timeMode = UpdateTimeMode.Unscaled) =>
            new UpdateOptions(seconds, timeMode);
    }
}
