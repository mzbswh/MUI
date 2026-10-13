using System;

namespace MUI.Pages
{
    [Serializable]
    public sealed class PagePolicySettings
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

        internal PagePolicyOverrides CreateOverrides(PagePolicyFields fields, int layer)
        {
            bool Has(PagePolicyFields field) => (fields & field) != 0;
            return new PagePolicyOverrides
            {
                PageRole = Has(PagePolicyFields.PageScope) ? (PageRole?)PageRole : null,
                OwnerDeparture = Has(PagePolicyFields.PageScope) ? (OwnerDeparture?)OwnerDeparture : null,
                RenderOrderSpan = Has(PagePolicyFields.RenderOrder) ? (int?)RenderOrderSpan : null,
                Layer = Has(PagePolicyFields.Layer) ? (int?)layer : null,
                Coverage = Has(PagePolicyFields.Coverage) ? (CoveragePolicy?)Coverage : null,
                ReceivesInput = Has(PagePolicyFields.Input) ? (bool?)ReceivesInput : null,
                TakesFocus = Has(PagePolicyFields.Focus) ? (bool?)TakesFocus : null,
                Modal = Has(PagePolicyFields.Modal) ? (bool?)Modal : null,
                BackBehavior = Has(PagePolicyFields.Back) ? (BackBehavior?)BackBehavior : null,
                AllowMultiple = Has(PagePolicyFields.Instances) ? (bool?)AllowMultiple : null,
                MaxInstances = Has(PagePolicyFields.Instances) ? (int?)MaxInstances : null,
                Overflow = Has(PagePolicyFields.Instances) ? (OverflowPolicy?)Overflow : null,
                ExistingInstance = Has(PagePolicyFields.Instances) ? (ExistingInstancePolicy?)ExistingInstance : null,
                TickPause = Has(PagePolicyFields.Tick) ? (TickPausePolicy?)TickPause : null,
                CacheMode = Has(PagePolicyFields.Cache) ? (ViewCacheMode?)CacheMode : null,
                CacheDuration = Has(PagePolicyFields.Cache) && CacheMode == ViewCacheMode.Timed ? (TimeSpan?)TimeSpan.FromSeconds(CacheDurationSeconds) : null,
                EnterTimeout = Has(PagePolicyFields.Timeouts) ? (float?)EnterTimeout : null,
                ExitTimeout = Has(PagePolicyFields.Timeouts) ? (float?)ExitTimeout : null,
                PrepareTimeout = Has(PagePolicyFields.Timeouts) ? (TimeSpan?)TimeSpan.FromSeconds(PrepareTimeoutSeconds) : null,
                CloseTimeout = Has(PagePolicyFields.Timeouts) ? (TimeSpan?)TimeSpan.FromSeconds(CloseTimeoutSeconds) : null,
                CloseDecisionTimeout = Has(PagePolicyFields.Timeouts) ? (TimeSpan?)TimeSpan.FromSeconds(CloseDecisionTimeoutSeconds) : null
            };
        }
    }
}
