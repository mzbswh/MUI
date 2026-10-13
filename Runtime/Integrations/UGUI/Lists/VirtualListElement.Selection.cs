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
        private readonly VirtualListSelection selectionState = new VirtualListSelection();

        public object SelectedKey
        {
            get => selectionState.Key;
            set
            {
                RequireListAlive();
                if (preparedRebind != null)
                {
                    preparedRebind.WriteSelection(value);
                    return;
                }
                var activation = lifetime;
                var source = sourceRevision;
                var selection = selectionState.Revision;
                var exists = value == null || keyIndices.ContainsKey(value);
                if (!IsSelectionCurrent(activation, source, selection))
                {
                    return;
                }

                if (!exists)
                {
                    throw new ArgumentException("Selected key is not in the current list.", nameof(value));
                }

                SetSelection(value);
            }
        }

        public int SelectedIndex => selectionState.Index;

        private void SetSelection(object key)
        {
            var activation = lifetime;
            var source = sourceRevision;
            var previousSelection = selectionState.Revision;
            var keyChanged = !Equals(selectionState.Key, key);
            if (!IsSelectionCurrent(activation, source, previousSelection))
            {
                return;
            }

            var index = key != null && keyIndices.TryGetValue(key, out var found) ? found : -1;
            if (!IsSelectionCurrent(activation, source, previousSelection))
            {
                return;
            }

            var indexChanged = selectionState.Index != index;
            var revision = selectionState.Commit(key, index);
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

        private bool IsSelectionCurrent(LifetimeScope activation, long source, long selection) =>
            IsAlive && ReferenceEquals(lifetime, activation) && source == sourceRevision &&
            selection == selectionState.Revision && (activation == null || (!activation.IsEnded && scope != null && scope.IsActive));

        private void ReconcileSelection()
        {
            var activation = lifetime;
            var source = sourceRevision;
            var selection = selectionState.Revision;
            var key = selectionState.Key;
            var exists = key != null && keyIndices.ContainsKey(key);
            if (IsSelectionCurrent(activation, source, selection))
            {
                SetSelection(exists ? key : null);
            }
        }

        private void RefreshSelectionVisuals(long revision, long source)
        {
            // 选择标记 GameObject 的激活可能同步替换或清空列表。
            foreach (var cell in cells.ToArray())
            {
                if (!IsAlive || revision != selectionState.Revision || source != sourceRevision)
                {
                    return;
                }

                if (cell.Selection != null)
                {
                    var selected = cell.Key != null && Equals(cell.Key, selectionState.Key);
                    if (!IsAlive || revision != selectionState.Revision || source != sourceRevision)
                    {
                        return;
                    }

                    cell.Selection.SetSelected(selected);
                }
            }
        }

        private void AttachSelection(VirtualListCell cell, View view)
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

                var key = cell.Key;
                var activation = lifetime;
                var source = sourceRevision;
                var selection = selectionState.Revision;
                var found = keyIndices.TryGetValue(key, out var index);
                if (!IsSelectionCurrent(activation, source, selection) || !ReferenceEquals(cell.Key, key) || cell.Element == null)
                {
                    return;
                }

                if (!found || !ReferenceEquals(snapshot[index].ViewModel, cell.Element.DisplayedViewModel) ||
                    !string.Equals(snapshot[index].TemplateKey, cell.TemplateKey, StringComparison.Ordinal))
                {
                    return;
                }

                SetSelection(cell.Key);
            };
            cell.SelectionButton.onClick.AddListener(cell.SelectionHandler);
        }


    }
}
