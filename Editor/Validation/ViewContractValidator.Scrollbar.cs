using System.Collections.Generic;
using MUI.UGUI;
using UnityEngine.UI;
using L = MUI.Editor.Localization.MUIEditorLocalization;

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
                errors.Add(L.Format("editor.ViewContractValidator.Scrollbar.ca55ec7917", path));
                return;
            }

            var handle = scrollbar.handleRect;
            if (handle == null)
            {
                errors.Add(L.Format("editor.ViewContractValidator.Scrollbar.9ade8392e6", path));
                return;
            }

            if (handle == scrollbar.transform || !handle.IsChildOf(scrollbar.transform))
            {
                errors.Add(L.Format("editor.ViewContractValidator.Scrollbar.7e7412f982", path));
            }

            if (handle.GetComponentInParent<View>(true) != view)
            {
                errors.Add(L.Format("editor.ViewContractValidator.Scrollbar.f866df34eb", path));
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

                errors.Add(L.Format("editor.ViewContractValidator.Scrollbar.a18e38d519", Path(element.transform, view.transform), entry.TargetProperty) +
                    L.Format("editor.ViewContractValidator.Scrollbar.7bb6aa750c", Path(owner.transform, view.transform), entry.Source) +
                    L.Get("editor.ViewContractValidator.Scrollbar.8a643a780c"));
            }
        }
    }
}
