namespace MUI.UGUI
{
    internal sealed class VirtualListSelection
    {
        internal object Key
        {
            get; private set;
        }
        internal int Index { get; private set; } = -1;
        internal long Revision
        {
            get; private set;
        }
        internal long Commit(object key, int index)
        {
            Key = key;
            Index = index;
            return ++Revision;
        }
        internal void Invalidate() => ++Revision;
        internal void ClearKey() => Key = null;
        internal void ClearIndex() => Index = -1;
    }
}
