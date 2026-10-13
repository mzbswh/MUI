namespace MUI.UGUI
{
    /// <summary>条目沿滚动轴相对于视口的对齐方式，最终位置受内容边界约束。</summary>
    public enum VirtualListAlignment
    {
        /// <summary>已完整可见则不移动，否则以最小位移进入视口；超大条目对齐起点。</summary>
        Nearest,
        /// <summary>条目起点对齐视口起点。</summary>
        Start,
        /// <summary>条目中心对齐视口中心。</summary>
        Center,
        /// <summary>条目末尾对齐视口末尾。</summary>
        End
    }
}
