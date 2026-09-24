using System;

namespace MUI.Loading
{
    /// <summary>
    /// 不可变的加载表现状态。仅描述显示，不拥有任务或输入阻挡；第三方来源可以独立控制可见性。
    /// </summary>
    public readonly struct LoadingSnapshot
    {
        /// <summary>校验数量和进度后构造状态；允许可见性独立于操作数量，空文案转为空字符串。</summary>
        public LoadingSnapshot(int count, int blockingCount, bool visible, double? progress, string message)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (blockingCount < 0 || blockingCount > count)
            {
                throw new ArgumentOutOfRangeException(nameof(blockingCount));
            }

            if (progress.HasValue && (double.IsNaN(progress.Value) || double.IsInfinity(progress.Value) ||
                progress.Value < 0 || progress.Value > 1))
            {
                throw new ArgumentOutOfRangeException(nameof(progress));
            }

            OperationCount = count;
            BlockingCount = blockingCount;
            IsVisible = visible;
            Progress = progress;
            Message = message ?? string.Empty;
        }

        /// <summary>活跃操作数。</summary>
        public int OperationCount
        {
            get;
        }

        /// <summary>声明阻挡输入的活跃操作数，不超过总操作数。</summary>
        public int BlockingCount
        {
            get;
        }

        /// <summary>是否展示加载提示，不等同于是否阻挡输入。</summary>
        public bool IsVisible
        {
            get;
        }

        /// <summary>0 到 1 的有限进度；null 表示不确定进度。</summary>
        public double? Progress
        {
            get;
        }

        /// <summary>当前提示文案，永不为 null。</summary>
        public string Message
        {
            get;
        }
    }
}
