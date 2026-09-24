using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MUI.Tabs
{
    /// <summary>
    /// 向 TabBar 和内容区域提供只读表现状态，由 TabContentController 在所属 UI 线程更新。
    /// 选择、重试和目录变更必须通过控制器请求，视图不直接修改状态。
    /// </summary>
    public sealed class TabViewModel : ViewModel
    {
        internal TabViewModel(IList<TabItemState> items)
        {
            ReplaceItems(items);
        }

        /// <summary>
        /// 按展示顺序排列的 Tab 项。集合结构只读；项的 Enabled 由控制器刷新。
        /// 可用性变化也通过 Items 属性通知，调用方不应把项视为不可变历史快照。
        /// </summary>
        public IReadOnlyList<TabItemState> Items
        {
            get; private set;
        }

        /// <summary>一次性发布的内容状态；加载期间选中的 Tab 不等于已显示的 Tab。</summary>
        public TabSnapshot Snapshot
        {
            get; private set;
        } =
            new TabSnapshot(null, null, TabPhase.Empty, null, 0);

        /// <summary>
        /// 复制集合结构但保留项对象。此处不发送通知，让控制器先完成目录与选择状态的协调。
        /// </summary>
        internal void ReplaceItems(IList<TabItemState> items)
        {
            Items = new ReadOnlyCollection<TabItemState>(new List<TabItemState>(items));
        }

        /// <summary>目录提交后统一通知；隔离订阅者异常，避免打断控制器收尾。</summary>
        internal void NotifyItemsChanged()
        {
            OnPropertyChangedSafely(nameof(Items), UIErrors.Report);
        }

        /// <summary>先替换完整快照，再通知视图，避免视图读到部分更新的状态。</summary>
        internal void Publish(TabSnapshot snapshot)
        {
            Snapshot = snapshot;
            OnPropertyChangedSafely(nameof(Snapshot), UIErrors.Report);
        }

        /// <summary>只在可用性确实变化时通知；不负责切换或关闭当前内容。</summary>
        internal void SetEnabled(string key, bool enabled)
        {
            foreach (var item in Items)
            {
                if (item.Key == key && item.Enabled != enabled)
                {
                    item.Enabled = enabled;
                    OnPropertyChangedSafely(nameof(Items), UIErrors.Report);
                }
            }
        }
    }
}
