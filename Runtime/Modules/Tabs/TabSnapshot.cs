using System;

namespace MUI.Tabs
{
    /// <summary>内容区域的阶段；Inactive 表示父生命周期已结束。</summary>
    public enum TabPhase
    {
        Empty,
        Loading,
        Ready,
        Error,
        Inactive
    }

    /// <summary>加载期间的表现策略，只影响内容区域，不隐藏 TabBar。</summary>
    public enum TabPendingDisplay
    {
        LoadingPlaceholder,
        Blank,
        /// <summary>保留旧画面，但停止旧激活的业务与输入；没有旧内容时显示空白。</summary>
        KeepPrevious
    }

    /// <summary>一次性发布的不可变内容状态，避免视图读取到不同请求混合的字段。</summary>
    public sealed class TabSnapshot
    {
        internal TabSnapshot(string selected,
            string displayed,
            TabPhase phase,
            Exception error,
            long version,
            bool loadingVisible = false)
        {
            SelectedTab = selected;
            DisplayedTab = displayed;
            Phase = phase;
            Error = error;
            RequestVersion = version;
            LoadingIndicatorVisible = loadingVisible;
        }

        /// <summary>当前选择意图的键；无选择时为 null。</summary>
        public string SelectedTab
        {
            get;
        }

        /// <summary>当前显示的子界面键；KeepPrevious 加载期间为冻结旧内容的键，无内容时为 null。</summary>
        public string DisplayedTab
        {
            get;
        }

        /// <summary>当前内容阶段。</summary>
        public TabPhase Phase
        {
            get;
        }

        /// <summary>失败信息；非失败状态通常为 null，面向用户的文案由表现层提供。</summary>
        public Exception Error
        {
            get;
        }

        /// <summary>选择代际，用于区分先后请求；不代表资源版本。</summary>
        public long RequestVersion
        {
            get;
        }

        /// <summary>是否显示加载提示，已包含延迟显示策略。</summary>
        public bool LoadingIndicatorVisible
        {
            get;
        }
    }
}
