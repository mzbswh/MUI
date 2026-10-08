using System;

namespace MUI
{
    /// <summary>异常发生位置的值快照，不持有宿主、View、模型、参数或资源。</summary>
    public readonly struct UIErrorContext
    {
        public UIErrorContext(Guid hostId, long viewId, string routeKey, string operation, string phase)
        {
            HostId = hostId;
            ViewId = viewId;
            RouteKey = Limit(routeKey);
            Operation = Limit(operation);
            Phase = Limit(phase);
        }

        public Guid HostId
        {
            get;
        }

        /// <summary>宿主内的导航句柄编号；不属于页面实例时为零。</summary>
        public long ViewId
        {
            get;
        }

        public string RouteKey
        {
            get;
        }

        public string Operation
        {
            get;
        }

        public string Phase
        {
            get;
        }

        public bool IsEmpty => HostId == Guid.Empty && ViewId == 0 && RouteKey == null &&
            Operation == null && Phase == null;

        internal UIErrorContext WithFallback(UIErrorContext fallback)
        {
            var sameHost = HostId == Guid.Empty || fallback.HostId == Guid.Empty || HostId == fallback.HostId;
            return new UIErrorContext(
                HostId != Guid.Empty ? HostId : fallback.HostId,
                ViewId != 0 ? ViewId : sameHost ? fallback.ViewId : 0,
                RouteKey ?? (sameHost ? fallback.RouteKey : null),
                Operation ?? fallback.Operation,
                Phase ?? fallback.Phase);
        }

        private static string Limit(string value) => string.IsNullOrEmpty(value) ? null :
            value.Length <= 256 ? value : value.Substring(0, 256);

        public override string ToString() => $"host={HostId:N}; view={ViewId}; route={RouteKey}; operation={Operation}; phase={Phase}";
    }
}
