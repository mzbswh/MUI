using System;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>虚拟列表项 View 根节点上的可选选择表现，独立于键盘焦点。</summary>
    [DisallowMultipleComponent]
    public sealed class VirtualListItemSelection : MonoBehaviour
    {
        [SerializeField]
        private GameObject selectedVisual = null;

        public bool Selected
        {
            get; private set;
        }

        public void Configure(GameObject visual)
        {
            selectedVisual = visual;
            Validate();
        }

        internal void Validate()
        {
            if (selectedVisual == null || selectedVisual.transform == transform || !selectedVisual.transform.IsChildOf(transform))
            {
                throw new InvalidOperationException("Selection visual must be a descendant of its item View.");
            }
        }

        internal void SetSelected(bool selected)
        {
            Selected = selected;
            if (selectedVisual != null && selectedVisual.activeSelf != selected)
            {
                selectedVisual.SetActive(selected);
            }
        }
    }
}
