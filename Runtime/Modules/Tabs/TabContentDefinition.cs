using System;
using System.Threading;
using System.Threading.Tasks;
using MUI.ChildViews;

namespace MUI.Tabs
{
    /// <summary>一个 Tab 的稳定身份、内容准备工厂和可选离开守卫；不持有已创建的子界面。</summary>
    public sealed class TabContentDefinition
    {
        /// <summary>创建定义。业务回调在控制器所属 UI 线程发起，应遵守取消与父级所有权约定。</summary>
        /// <param name="key">目录内唯一、非空的稳定键。</param>
        /// <param name="label">展示标题，允许空字符串。</param>
        /// <param name="prepare">在给定父 Scope 中准备子视图；显示提交由控制器协调。</param>
        /// <param name="isEnabled">同步检查业务可用性；会在选择、准备与提交阶段重复求值，须无业务副作用；省略时始终可选。</param>
        /// <param name="canLeaveAsync">离开当前内容前的异步许可；省略时直接离开。</param>
        /// <param name="estimatedRetainedBytes">停用后仍持有的实例资源估算字节；未知时为 null，不代表实际进程内存。</param>
        public TabContentDefinition(string key,
            string label,
            Func<ChildViewScope, CancellationToken, ValueTask<ChildViewHandle>> prepare,
            Func<bool> isEnabled = null,
            Func<TabLeaveContext, CancellationToken, ValueTask<bool>> canLeaveAsync = null,
            long? estimatedRetainedBytes = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Stable TabKey is required.", nameof(key));
            }

            if (estimatedRetainedBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(estimatedRetainedBytes));
            }

            EstimatedRetainedBytes = estimatedRetainedBytes;
            Key = key;
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Prepare = prepare ?? throw new ArgumentNullException(nameof(prepare));
            IsEnabled = isEnabled ?? (() => true);
            CanLeaveAsync = canLeaveAsync;
        }

        /// <summary>目录内唯一的稳定键。</summary>
        public string Key
        {
            get;
        }

        /// <summary>TabBar 展示标题。</summary>
        public string Label
        {
            get;
        }

        /// <summary>停用实例的资源估算字节；未知时为 null，共享资源是否重复计量由项目估算约定决定。</summary>
        public long? EstimatedRetainedBytes
        {
            get;
        }

        // 工厂和策略只交给控制器执行，表现层只观察 ViewModel。
        internal Func<ChildViewScope, CancellationToken, ValueTask<ChildViewHandle>> Prepare
        {
            get;
        }

        internal Func<bool> IsEnabled
        {
            get;
        }

        internal Func<TabLeaveContext, CancellationToken, ValueTask<bool>> CanLeaveAsync
        {
            get;
        }
    }
}
