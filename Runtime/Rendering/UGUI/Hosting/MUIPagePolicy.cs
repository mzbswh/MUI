using System;
using MUI.Navigation;

namespace MUI.UGUI
{
    [Flags]
    public enum MUIPagePolicyFields
    {
        None = 0,
        Layer = 1,
        // 位 2 原为 History，保留为空，避免旧资产其它配置位移位。
        Coverage = 4,
        Focus = 8,
        Modal = 16,
        Back = 32,
        Instances = 64,
        Tick = 128,
        Cache = 256,
        Timeouts = 512,
        RenderOrder = 1024,
        PageScope = 2048,
        Input = 4096,
        All = 8189
    }

    [Serializable]
    public sealed class MUILayer
    {
        public string Name;
        public int Order;
        [UnityEngine.Serialization.FormerlySerializedAs("OrderGap")]
        public int LayerGapAfter = -1;

        public MUILayer(string name, int order)
        {
            Name = name;
            Order = order;
        }
    }

    [Serializable]
    public sealed class MUIPagePolicy
    {
        public string Name;
        public MUIPagePolicyFields Overrides;
        public MUIPagePolicyValues Values = new MUIPagePolicyValues();

        public MUIPagePolicy(string name, MUIPagePolicyFields overrides)
        {
            Name = name;
            Overrides = overrides;
        }
    }

    [Serializable]
    public sealed class MUIPagePolicyValues
    {
        public string Layer = "Default";
        public PageRole PageRole = PageRole.Main;
        public OwnerDeparture OwnerDeparture;
        public CoveragePolicy Coverage;
        public bool ReceivesInput = true;
        public bool TakesFocus = true;
        public bool Modal;
        public BackBehavior BackBehavior;
        public bool AllowMultiple;
        public int MaxInstances = 1;
        public OverflowPolicy Overflow;
        public ExistingInstancePolicy ExistingInstance;
        public TickPausePolicy TickPause = TickPausePolicy.Hidden;
        public int RenderOrderSpan;
        public ViewCacheMode CacheMode;
        public float CacheDurationSeconds = 60;
        public float EnterTimeout = 5;
        public float ExitTimeout = 5;
        public float PrepareTimeoutSeconds = 30;
        public float CloseTimeoutSeconds = 30;
        public float CloseDecisionTimeoutSeconds = 120;

        internal RoutePolicyOverrides CreateOverrides(MUIPagePolicyFields fields, int layer)
        {
            bool Has(MUIPagePolicyFields field) => (fields & field) != 0;
            return new RoutePolicyOverrides
            {
                PageRole = Has(MUIPagePolicyFields.PageScope) ? (PageRole?)PageRole : null,
                OwnerDeparture = Has(MUIPagePolicyFields.PageScope) ? (OwnerDeparture?)OwnerDeparture : null,
                RenderOrderSpan = Has(MUIPagePolicyFields.RenderOrder) ? (int?)RenderOrderSpan : null,
                Layer = Has(MUIPagePolicyFields.Layer) ? (int?)layer : null,
                Coverage = Has(MUIPagePolicyFields.Coverage) ? (CoveragePolicy?)Coverage : null,
                ReceivesInput = Has(MUIPagePolicyFields.Input) ? (bool?)ReceivesInput : null,
                TakesFocus = Has(MUIPagePolicyFields.Focus) ? (bool?)TakesFocus : null,
                Modal = Has(MUIPagePolicyFields.Modal) ? (bool?)Modal : null,
                BackBehavior = Has(MUIPagePolicyFields.Back) ? (BackBehavior?)BackBehavior : null,
                AllowMultiple = Has(MUIPagePolicyFields.Instances) ? (bool?)AllowMultiple : null,
                MaxInstances = Has(MUIPagePolicyFields.Instances) ? (int?)MaxInstances : null,
                Overflow = Has(MUIPagePolicyFields.Instances) ? (OverflowPolicy?)Overflow : null,
                ExistingInstance = Has(MUIPagePolicyFields.Instances) ? (ExistingInstancePolicy?)ExistingInstance : null,
                TickPause = Has(MUIPagePolicyFields.Tick) ? (TickPausePolicy?)TickPause : null,
                CacheMode = Has(MUIPagePolicyFields.Cache) ? (ViewCacheMode?)CacheMode : null,
                CacheDuration = Has(MUIPagePolicyFields.Cache) && CacheMode == ViewCacheMode.Timed ? (TimeSpan?)TimeSpan.FromSeconds(CacheDurationSeconds) : null,
                EnterTimeout = Has(MUIPagePolicyFields.Timeouts) ? (float?)EnterTimeout : null,
                ExitTimeout = Has(MUIPagePolicyFields.Timeouts) ? (float?)ExitTimeout : null,
                PrepareTimeout = Has(MUIPagePolicyFields.Timeouts) ? (TimeSpan?)TimeSpan.FromSeconds(PrepareTimeoutSeconds) : null,
                CloseTimeout = Has(MUIPagePolicyFields.Timeouts) ? (TimeSpan?)TimeSpan.FromSeconds(CloseTimeoutSeconds) : null,
                CloseDecisionTimeout = Has(MUIPagePolicyFields.Timeouts) ? (TimeSpan?)TimeSpan.FromSeconds(CloseDecisionTimeoutSeconds) : null
            };
        }
    }
}
