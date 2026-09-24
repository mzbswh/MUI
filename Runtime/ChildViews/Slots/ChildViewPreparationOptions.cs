using System;

namespace MUI.ChildViews
{
    /// <summary>子视图准备的等待期限与隔离数量；超时不代表底层工作已被强制终止。</summary>
    public sealed class ChildViewPreparationOptions
    {
        internal static readonly ChildViewPreparationOptions Default = new ChildViewPreparationOptions();

        public ChildViewPreparationOptions(TimeSpan? timeout = null, int maxQuarantinedPreparations = 2)
        {
            if (timeout.HasValue && (timeout.Value <= TimeSpan.Zero || timeout.Value.TotalMilliseconds > int.MaxValue))
            {
                throw new ArgumentOutOfRangeException(nameof(timeout));
            }

            if (maxQuarantinedPreparations < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxQuarantinedPreparations));
            }

            Timeout = timeout;
            MaxQuarantinedPreparations = maxQuarantinedPreparations;
        }

        /// <summary>准备超时；null 保持原有等待直至完成的行为。</summary>
        public TimeSpan? Timeout
        {
            get;
        }

        /// <summary>尚未完成准备或迟到清理的隔离操作上限；达到上限后拒绝启动新准备。</summary>
        public int MaxQuarantinedPreparations
        {
            get;
        }
    }
}
