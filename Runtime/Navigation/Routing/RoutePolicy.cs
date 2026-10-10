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

    /// <summary>单实例页面再次打开时的行为；复用不会更新数据或调整显示顺序。</summary>
    public enum ExistingInstancePolicy
    {
        Reject,
        ReturnReady
    }

    public enum BackBehavior
    {
        Close,
        Ignore,
        HandleByPresenter,
        Block
    }

    /// <summary>仅缓存关闭并重置后的 View：不保留、无时间过期但受容量淘汰，或在指定停用时长内保留。</summary>
    public enum ViewCacheMode
    {
        None,
        KeepAlive,
        Timed
    }

    public sealed class RoutePolicy
    {
        public RoutePolicy(int layer = 0,
                    bool allowMultiple = false,
                    int maxInstances = 1,
                    CoveragePolicy coverage = CoveragePolicy.None,
                    bool takesFocus = true,
                    bool modal = false,
                    BackBehavior backBehavior = BackBehavior.Close,
                    TickPausePolicy tickPause = TickPausePolicy.Hidden,
                    OverflowPolicy overflow = OverflowPolicy.Reject,
                    float enterTimeout = 5f,
                    float exitTimeout = 5f,
                    ViewCacheMode cacheMode = ViewCacheMode.None,
                    TimeSpan? cacheDuration = null,
                    TimeSpan? closeTimeout = null,
                    TimeSpan? prepareTimeout = null,
                    TimeSpan? closeDecisionTimeout = null,
                    ExistingInstancePolicy existingInstance = ExistingInstancePolicy.Reject,
                    string layerName = null,
                    string presetName = null,
                    string configurationName = null,
                    int renderOrderSpan = 0, PageRole pageRole = PageRole.Main, OwnerDeparture ownerDeparture = OwnerDeparture.Close, bool receivesInput = true)
        {
            if (!Enum.IsDefined(typeof(ExistingInstancePolicy), existingInstance) ||
                (allowMultiple && existingInstance != ExistingInstancePolicy.Reject))
            {
                throw new ArgumentOutOfRangeException(nameof(existingInstance));
            }

            if (renderOrderSpan != 0 && renderOrderSpan < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(renderOrderSpan));
            }
            if (!Enum.IsDefined(typeof(PageRole), pageRole) || !Enum.IsDefined(typeof(OwnerDeparture), ownerDeparture))
            {
                throw new ArgumentOutOfRangeException(nameof(pageRole));
            }
            PageRole = pageRole;
            OwnerDeparture = ownerDeparture;
            RenderOrderSpan = renderOrderSpan;
            ExistingInstance = existingInstance;
            CloseDecisionTimeout = closeDecisionTimeout ?? TimeSpan.FromMinutes(2);
            if (CloseDecisionTimeout <= TimeSpan.Zero || CloseDecisionTimeout.TotalMilliseconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(closeDecisionTimeout));
            }

            PrepareTimeout = prepareTimeout ?? TimeSpan.FromSeconds(30);
            if (PrepareTimeout <= TimeSpan.Zero || PrepareTimeout.TotalMilliseconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(prepareTimeout));
            }

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

            TickPause = tickPause;
            BackBehavior = backBehavior;
            Layer = layer;
            AllowMultiple = allowMultiple;
            MaxInstances = allowMultiple ? maxInstances : 1;
            ReceivesInput = receivesInput;
            RequestedCoverage = coverage;
            Coverage = modal && coverage == CoveragePolicy.None ? CoveragePolicy.BlockInput : coverage;
            TakesFocus = takesFocus;
            Modal = modal;
            LayerName = layerName;
            PresetName = presetName;
            ConfigurationName = configurationName;
        }

        /// <summary>零继承宿主默认跨度；非零至少为 2，包含遮罩和根 Canvas。</summary>
        public int RenderOrderSpan
        {
            get;
        }

        public static RoutePolicy Default { get; } = new RoutePolicy();

        public string LayerName
        {
            get;
        }

        public string PresetName
        {
            get;
        }

        public string ConfigurationName
        {
            get;
        }

        /// <summary>默认拒绝重复打开；ReturnReady 仅返回参数和指定模型兼容的就绪实例。</summary>
        public ExistingInstancePolicy ExistingInstance
        {
            get;
        }

        /// <summary>正常关闭后尝试缓存已重置的 View；View 与取得凭证均须显式支持缓存，业务实例始终清理。</summary>
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

        /// <summary>异步候选及其依赖的总准备预算；超时后候选隔离持有，等待实际准备与清理结束。</summary>
        public TimeSpan PrepareTimeout
        {
            get;
        }

        /// <summary>关闭前事务排空和关闭决策各阶段的等待预算；超时不释放仍被项目回调持有的页面。</summary>
        public TimeSpan CloseDecisionTimeout
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

        /// <summary>自身业务 Tick 的暂停条件，默认仅隐藏时暂停；不暂停 Unity Update、协程或动画。</summary>
        public TickPausePolicy TickPause
        {
            get;
        }

        public PageRole PageRole
        {
            get;
        }

        public OwnerDeparture OwnerDeparture
        {
            get;
        }

        public int Layer
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

        internal CoveragePolicy RequestedCoverage { get; }

        /// <summary>是否接收页面输入；关闭时自身 UI 不阻挡射线，也不获取焦点。</summary>
        public bool ReceivesInput { get; }

        /// <summary>本页面对下层页面的显示和输入影响；下层业务 Tick 由下层自己的 TickPause 决定。</summary>
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
