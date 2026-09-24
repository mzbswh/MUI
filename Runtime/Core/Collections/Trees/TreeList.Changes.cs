using System.Collections.Generic;

namespace MUI
{
    public sealed partial class TreeList<T>
    {
        /// <summary>
        /// 展开操作沿用同一份先序节点表，可见节点的相对顺序不变。
        /// 合并遍历新旧可见序列，连续隐藏或显示的区间合并为一次移除或插入。
        /// </summary>
        private static void ApplyExpansionChanges(Snapshot previous, Snapshot candidate,
            List<T> rows, ObservableListEditor<T> editor)
        {
            var oldNode = 0;
            var newNode = 0;
            var row = 0;
            var end = candidate.Entries.Length;
            while (oldNode < end || newNode < end)
            {
                if (oldNode == newNode)
                {
                    // 相同条目仍可能需要刷新折叠图标；不让其他未变化条目重新测量。
                    if (previous.Expanded[oldNode] != candidate.Expanded[newNode])
                    {
                        editor.NotifyUpdated(row);
                    }

                    oldNode = NextVisibleNode(previous, oldNode);
                    newNode = NextVisibleNode(candidate, newNode);
                    ++row;
                }
                else if (oldNode < newNode)
                {
                    var removed = 0;
                    do
                    {
                        ++removed;
                        oldNode = NextVisibleNode(previous, oldNode);
                    }
                    while (oldNode < newNode);

                    // 删除后下一条仍位于同一个中间行索引。
                    editor.RemoveRange(row, removed);
                }
                else
                {
                    var added = 0;
                    do
                    {
                        ++added;
                        newNode = NextVisibleNode(candidate, newNode);
                    }
                    while (newNode < oldNode);

                    editor.InsertRange(row, rows.GetRange(row, added));
                    row += added;
                }
            }
        }

        private static int NextVisibleNode(Snapshot source, int index) =>
            source.Expanded[index] ? index + 1 : source.Entries[index].End;
    }
}
