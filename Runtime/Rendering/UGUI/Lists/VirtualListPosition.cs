namespace MUI.UGUI
{
    /// <summary>
    /// 显式捕获的阅读位置。仅保存来源令牌和稳定键，不持有来源集合、条目模型或视图。
    /// 键必须使用独立、不可变的业务标识；原顺序用于锚点删除后的邻项回退。
    /// </summary>
    public sealed class VirtualListPosition
    {
        private readonly object[] orderedKeys;

        internal VirtualListPosition(object sourceIdentity, object[] keys, int anchorIndex, float offset)
        {
            SourceIdentity = sourceIdentity;
            orderedKeys = keys;
            AnchorIndex = anchorIndex;
            Offset = offset;
        }

        /// <summary>不引用来源集合的不透明身份；同一集合在不同列表中具有相同身份。</summary>
        public object SourceIdentity
        {
            get;
        }

        public object AnchorKey => AnchorIndex < 0 ? null : orderedKeys[AnchorIndex];

        /// <summary>锚点起始处到视口起始处的距离；视口位于条目前的间距或内边距时可为负。单位为 Content 局部 Canvas 单位。</summary>
        public float Offset
        {
            get;
        }

        internal int AnchorIndex
        {
            get;
        }

        internal int Count => orderedKeys.Length;

        internal object KeyAt(int index) => orderedKeys[index];
    }
}
