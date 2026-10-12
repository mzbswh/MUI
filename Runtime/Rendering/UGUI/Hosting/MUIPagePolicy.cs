using System;
using MUI.Pages;

namespace MUI.UGUI
{
    [Flags]
    public enum MUIPagePolicyFields
    {
        None = 0,
        Layer = 1,
        Coverage = 2,
        Focus = 4,
        Modal = 8,
        Back = 16,
        Instances = 32,
        Tick = 64,
        Cache = 128,
        Timeouts = 256,
        RenderOrder = 512,
        PageScope = 1024,
        Input = 2048,
        All = 4095
    }

    [Serializable]
    public sealed class MUILayer
    {
        public string Name;
        public int Order;

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

        internal PagePolicyOverrides CreateOverrides(MUIPagePolicyFields fields, int layer)
        {
            bool Has(MUIPagePolicyFields field) => (fields & field) != 0;
            return new PagePolicyOverrides
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
