using System;
using MUI.ChildViews;

namespace MUI.Tabs
{
    public sealed partial class TabContentDefinition
    {
        private TabContentDefinition(string key, string label, Func<ChildViewScope, ChildViewHandle> prepare,
                    Func<bool> isEnabled, Func<TabLeaveContext, bool> canLeave, long? estimatedRetainedBytes)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Stable TabKey is required.", nameof(key));
            }

            if (estimatedRetainedBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(estimatedRetainedBytes));
            }

            Key = key;
            Label = label ?? throw new ArgumentNullException(nameof(label));
            PrepareSynchronous = prepare ?? throw new ArgumentNullException(nameof(prepare));
            IsEnabled = isEnabled ?? (() => true);
            CanLeaveSynchronous = canLeave;
            EstimatedRetainedBytes = estimatedRetainedBytes;
        }

        /// <summary>此定义是否使用独立同步工厂和同步守卫。</summary>
        public bool SupportsSynchronousLifecycle => PrepareSynchronous != null;

        internal Func<ChildViewScope, ChildViewHandle> PrepareSynchronous
        {
            get;
        }

        internal Func<TabLeaveContext, bool> CanLeaveSynchronous
        {
            get;
        }

        /// <summary>创建纯同步定义；工厂和离开守卫均直接执行，不要求实现异步委托。</summary>
        public static TabContentDefinition CreateSynchronous(string key, string label,
            Func<ChildViewScope, ChildViewHandle> prepare, Func<bool> isEnabled = null,
            Func<TabLeaveContext, bool> canLeave = null, long? estimatedRetainedBytes = null)
        {
            return new TabContentDefinition(key, label, prepare, isEnabled, canLeave, estimatedRetainedBytes);
        }
    }
}
