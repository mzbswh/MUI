namespace MUI.Tabs
{
    /// <summary>目标内容失败后的表现策略，不改变失败目标本身的选择结果。</summary>
    public enum TabFailureDisplay
    {
        /// <summary>清除内容并显示错误与重试。</summary>
        ErrorPlaceholder,
        /// <summary>尝试重新激活唯一保留的旧内容，恢复失败后显示错误占位。</summary>
        RestorePrevious
    }
}
