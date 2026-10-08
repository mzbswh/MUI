using System.Collections.Generic;
using MUI.UGUI;
using UnityEngine.UI;

namespace MUI.Editor
{
    public static partial class ViewContractValidator
    {
        /// <summary>检查滚动条原生手柄及其 View 归属，不运行布局或创建控件。</summary>
        private static void ValidateScrollbar(Element element, View view, ICollection<string> errors)
        {
            var path = Path(element.transform, view.transform);
            var scrollbar = element.GetComponent<Scrollbar>();
            if (scrollbar == null)
            {
                errors.Add($"滚动条缺少同节点的 Scrollbar：{path}。");
                return;
            }

            var handle = scrollbar.handleRect;
            if (handle == null)
            {
                errors.Add($"滚动条缺少手柄引用：{path}。");
                return;
            }

            if (handle == scrollbar.transform || !handle.IsChildOf(scrollbar.transform))
            {
                errors.Add($"滚动条手柄必须位于其子节点，不能驱动滚动条根节点本身：{path}。");
            }

            if (handle.GetComponentInParent<View>(true) != view)
            {
                errors.Add($"滚动条手柄引用不能跨越 View 边界：{path}。");
            }
        }

        private static void ValidateScrollbarWriter(ScrollbarElement element, BindingEntry entry,
            ScrollRect[] owners, View view, ICollection<string> errors)
        {
            if (entry.Mode == BindingMode.OneWayToSource ||
                (entry.TargetProperty != nameof(ScrollbarElement.Value) &&
                 entry.TargetProperty != nameof(ScrollbarElement.Size)))
            {
                return;
            }

            var scrollbar = element.GetComponent<Scrollbar>();
            if (scrollbar == null)
            {
                return;
            }

            foreach (var owner in owners)
            {
                if (owner == null || (owner.horizontalScrollbar != scrollbar && owner.verticalScrollbar != scrollbar))
                {
                    continue;
                }

                errors.Add($"滚动条 {Path(element.transform, view.transform)}.{entry.TargetProperty} " +
                    $"同时由 ScrollRect {Path(owner.transform, view.transform)} 与绑定 {entry.Source} 写入。" +
                    "请绑定滚动容器位置，或改为仅反向观察滚动条。");
            }
        }
    }
}
