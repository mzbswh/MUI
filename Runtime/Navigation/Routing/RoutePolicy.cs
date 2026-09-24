using System;

namespace MUI.Navigation
{
    /// <summary>对下层页面的影响，本身不创建模态射线阻挡层。</summary>
    public enum CoveragePolicy
    {
        None,
        BlockInput,
        Hide
    }

    [Flags]
    public enum TickPausePolicy
    {
        None = 0,
        Covered = 1,
        Hidden = 2
    }

    public enum OverflowPolicy
    {
        Reject,
        CloseOldest
    }

    public enum BackBehavior
    {
        Close,
        Ignore,
        HandleByPresenter,
        Block
    }

    /// <summary>关闭后不保留、受容量限制保留，或在指定停用时长内保留。</summary>
    public enum ViewCacheMode
    {
        None,
        KeepAlive,
        Timed
    }

    public sealed class RoutePolicy
    {
        public RoutePolicy(int layer = 0,
                    bool enterHistory = true,
                    bool allowMultiple = false,
                    int maxInstances = 1,
                    CoveragePolicy coverage = CoveragePolicy.None,
                    bool takesFocus = true,
                    bool modal = false,
                    BackBehavior backBehavior = BackBehavior.Close,
                    TickPausePolicy tickPause = TickPausePolicy.None,
                    int maxTickCatchUp = 4,
                    OverflowPolicy overflow = OverflowPolicy.Reject,
                    float enterTimeout = 5f,
                    float exitTimeout = 5f,
                    ViewCacheMode cacheMode = ViewCacheMode.None,
                    TimeSpan? cacheDuration = null,
                    TimeSpan? closeTimeout = null)
        {
            CloseTimeout = closeTimeout ?? TimeSpan.FromSeconds(30);
            if (CloseTimeout <= TimeSpan.Zero || CloseTimeout.TotalMilliseconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(closeTimeout));
            }

            if (!Enum.IsDefined(typeof(OverflowPolicy), overflow))
            {
                throw new ArgumentOutOfRangeException(nameof(overflow));
            }

            if (!Enum.IsDefined(typeof(ViewCacheMode), cacheMode))
            {
                throw new ArgumentOutOfRangeException(nameof(cacheMode));
            }

            if (cacheMode == ViewCacheMode.Timed)
            {
                if (!cacheDuration.HasValue || cacheDuration.Value <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(nameof(cacheDuration));
                }
            }
            else if (cacheDuration.HasValue)
            {
                throw new ArgumentException("Cache duration requires Timed mode.", nameof(cacheDuration));
            }

            CacheMode = cacheMode;
            CacheDuration = cacheDuration;
            Overflow = overflow;
            if (float.IsNaN(enterTimeout) || float.IsInfinity(enterTimeout) || enterTimeout <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(enterTimeout));
            }

            EnterTimeout = enterTimeout;
            if (float.IsNaN(exitTimeout) || float.IsInfinity(exitTimeout) || exitTimeout <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exitTimeout));
            }

            ExitTimeout = exitTimeout;
            if (maxInstances < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxInstances));
            }

            if (!Enum.IsDefined(typeof(CoveragePolicy), coverage))
            {
                throw new ArgumentOutOfRangeException(nameof(coverage));
            }

            if (!Enum.IsDefined(typeof(BackBehavior), backBehavior))
            {
                throw new ArgumentOutOfRangeException(nameof(backBehavior));
            }

            if ((tickPause & ~(TickPausePolicy.Covered | TickPausePolicy.Hidden)) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickPause));
            }

            if (maxTickCatchUp < 1 || maxTickCatchUp > 32)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTickCatchUp));
            }

            TickPause = tickPause;
            MaxTickCatchUp = maxTickCatchUp;
            BackBehavior = backBehavior;
            Layer = layer;
            EnterHistory = enterHistory;
            AllowMultiple = allowMultiple;
            MaxInstances = allowMultiple ? maxInstances : 1;
            Coverage = modal && coverage == CoveragePolicy.None ? CoveragePolicy.BlockInput : coverage;
            TakesFocus = takesFocus;
            Modal = modal;
        }

        public static RoutePolicy Default { get; } = new RoutePolicy();

        /// <summary>正常关闭后尝试缓存；仅接纳框架拥有模型且 Presenter 显式支持复用的实例。</summary>
        public ViewCacheMode CacheMode
        {
            get;
        }

        /// <summary>从关闭后成功入缓存起计时；Timed 模式必须提供正时长。</summary>
        public TimeSpan? CacheDuration
        {
            get;
        }

        /// <summary>视觉退出后的清理预算；超时只完成逻辑结果，未收敛资源继续隔离持有。</summary>
        public TimeSpan CloseTimeout
        {
            get;
        }

        /// <summary>退出效果的非缩放累计帧时长上限，不包含关闭回调及资源清理。</summary>
        public float ExitTimeout
        {
            get;
        }

        public float EnterTimeout
        {
            get;
        }

        public OverflowPolicy Overflow
        {
            get;
        }

        public TickPausePolicy TickPause
        {
            get;
        }

        public int MaxTickCatchUp
        {
            get;
        }

        public int Layer
        {
            get;
        }

        public bool EnterHistory
        {
            get;
        }

        public bool AllowMultiple
        {
            get;
        }

        public int MaxInstances
        {
            get;
        }

        public CoveragePolicy Coverage
        {
            get;
        }

        public bool TakesFocus
        {
            get;
        }

        public bool Modal
        {
            get;
        }

        public BackBehavior BackBehavior
        {
            get;
        }
    }
}
