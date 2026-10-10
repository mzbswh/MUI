using System;

namespace MUI.Navigation
{
    /// <summary>显式覆盖策略；null 表示继承，零和 false 仍是有效设置。</summary>
    public sealed class RoutePolicyOverrides
    {
        public int? RenderOrderSpan
        {
            get; set;
        }

        public PageRole? PageRole
        {
            get; set;
        }

        public OwnerDeparture? OwnerDeparture
        {
            get; set;
        }

        public int? Layer
        {
            get; set;
        }

        public bool? AllowMultiple
        {
            get; set;
        }

        public int? MaxInstances
        {
            get; set;
        }

        public CoveragePolicy? Coverage
        {
            get; set;
        }

        public bool? ReceivesInput { get; set; }

        public bool? TakesFocus
        {
            get; set;
        }

        public bool? Modal
        {
            get; set;
        }

        public BackBehavior? BackBehavior
        {
            get; set;
        }

        public TickPausePolicy? TickPause
        {
            get; set;
        }

        public OverflowPolicy? Overflow
        {
            get; set;
        }

        public float? EnterTimeout
        {
            get; set;
        }

        public float? ExitTimeout
        {
            get; set;
        }

        public ViewCacheMode? CacheMode
        {
            get; set;
        }

        public TimeSpan? CacheDuration
        {
            get; set;
        }

        public TimeSpan? CloseTimeout
        {
            get; set;
        }

        public TimeSpan? PrepareTimeout
        {
            get; set;
        }

        public TimeSpan? CloseDecisionTimeout
        {
            get; set;
        }

        public ExistingInstancePolicy? ExistingInstance
        {
            get; set;
        }

        public RoutePolicy Apply(RoutePolicy source, string layerName = null,
            string presetName = null, string configurationName = null)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var cacheMode = CacheMode ?? source.CacheMode;
            if (CacheDuration.HasValue && cacheMode != ViewCacheMode.Timed)
            {
                throw new ArgumentException("Cache duration requires Timed mode.", nameof(CacheDuration));
            }
            return new RoutePolicy(
                layer: Layer ?? source.Layer,
                allowMultiple: AllowMultiple ?? source.AllowMultiple,
                maxInstances: MaxInstances ?? source.MaxInstances,
                coverage: Coverage ?? source.RequestedCoverage,
                takesFocus: TakesFocus ?? source.TakesFocus,
                modal: Modal ?? source.Modal,
                backBehavior: BackBehavior ?? source.BackBehavior,
                tickPause: TickPause ?? source.TickPause,
                overflow: Overflow ?? source.Overflow,
                enterTimeout: EnterTimeout ?? source.EnterTimeout,
                exitTimeout: ExitTimeout ?? source.ExitTimeout,
                cacheMode: cacheMode,
                cacheDuration: cacheMode == ViewCacheMode.Timed ? CacheDuration ?? source.CacheDuration : null,
                closeTimeout: CloseTimeout ?? source.CloseTimeout,
                prepareTimeout: PrepareTimeout ?? source.PrepareTimeout,
                closeDecisionTimeout: CloseDecisionTimeout ?? source.CloseDecisionTimeout,
                existingInstance: ExistingInstance ?? source.ExistingInstance,
                layerName: Layer.HasValue ? layerName : layerName ?? source.LayerName,
                presetName: presetName ?? source.PresetName,
                configurationName: configurationName ?? source.ConfigurationName,
                renderOrderSpan: RenderOrderSpan ?? source.RenderOrderSpan,
                pageRole: PageRole ?? source.PageRole, ownerDeparture: OwnerDeparture ?? source.OwnerDeparture,
                receivesInput: ReceivesInput ?? source.ReceivesInput);
        }
    }
}
