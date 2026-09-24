using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MUI.UGUI
{
    public sealed partial class VirtualListElement
    {
        [SerializeField]
        private bool selectOnItemClick = true;
        private object selectedKey;
        private int selectedIndex = -1;
        private long selectionRevision;

        public object SelectedKey
        {
            get => selectedKey;
            set
            {
                RequireListAlive();
                if (value != null && !keyIndices.ContainsKey(value))
                {
                    throw new ArgumentException("Selected key is not in the current list.", nameof(value));
                }

                SetSelection(value);
            }
        }

        public int SelectedIndex => selectedIndex;

        private void SetSelection(object key)
        {
            var keyChanged = !Equals(selectedKey, key);
            var index = key != null && keyIndices.TryGetValue(key, out var found) ? found : -1;
            var indexChanged = selectedIndex != index;
            selectedKey = key;
            selectedIndex = index;
            var revision = ++selectionRevision;
            var source = sourceRevision;
            RefreshSelectionVisuals(revision, source);
            // PropertyChanged 读取者使用当前状态。即使视觉回调
            // 替换为同键来源，也应保留本次变更通知。
            if (!IsAlive)
            {
                return;
            }

            if (keyChanged)
            {
                NotifyChanged(nameof(SelectedKey));
            }

            if (!IsAlive)
            {
                return;
            }

            if (indexChanged)
            {
                NotifyChanged(nameof(SelectedIndex));
            }
        }

        private void ReconcileSelection() => SetSelection(selectedKey != null && keyIndices.ContainsKey(selectedKey) ? selectedKey : null);

        private void RefreshSelectionVisuals(long revision, long source)
        {
            // 选择标记 GameObject 的激活可能同步替换或清空列表。
            foreach (var cell in cells.ToArray())
            {
                if (!IsAlive || revision != selectionRevision || source != sourceRevision)
                {
                    return;
                }

                if (cell.Selection != null)
                {
                    cell.Selection.SetSelected(cell.Key != null && Equals(cell.Key, selectedKey));
                }
            }
        }

        private void AttachSelection(Cell cell, View view)
        {
            cell.Selection = view.GetComponent<VirtualListItemSelection>();
            if (cell.Selection != null)
            {
                cell.Selection.Validate();
                cell.Selection.SetSelected(false);
            }

            cell.SelectionButton = selectOnItemClick ? view.GetComponent<Button>() : null;
            if (cell.SelectionButton == null)
            {
                return;
            }

            cell.SelectionHandler = () =>
            {
                if (!IsAlive || scope == null || !scope.IsActive || cell.Key == null || view == null || !view.IsInputEnabled || cell.SelectionButton == null || !cell.SelectionButton.isActiveAndEnabled || !cell.SelectionButton.IsInteractable())
                {
                    return;
                }

                if (!keyIndices.TryGetValue(cell.Key, out var index) || !ReferenceEquals(snapshot[index].ViewModel, cell.Element.DisplayedViewModel) ||
                    !string.Equals(snapshot[index].TemplateKey, cell.TemplateKey, StringComparison.Ordinal))
                {
                    return;
                }

                SetSelection(cell.Key);
            };
            cell.SelectionButton.onClick.AddListener(cell.SelectionHandler);
        }

        private static void DetachSelection(Cell cell)
        {
            if (cell.SelectionButton != null && cell.SelectionHandler != null)
            {
                cell.SelectionButton.onClick.RemoveListener(cell.SelectionHandler);
            }

            cell.SelectionHandler = null;
            if (cell.Selection != null)
            {
                cell.Selection.SetSelected(false);
            }
        }
    }
}
