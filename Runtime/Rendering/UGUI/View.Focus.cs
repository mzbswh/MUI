using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MUI.UGUI
{
    public sealed partial class View
    {
        [SerializeField]
        private Selectable defaultSelection = null;
        private GameObject previousSelection;

        public void SetFocused(bool focused)
        {
            RequireAlive();
            var system = EventSystem.current;
            if (system == null || system.alreadySelecting)
            {
                return;
            }

            var selected = system.currentSelectedGameObject;
            if (!focused)
            {
                if (BelongsToView(selected))
                {
                    previousSelection = selected;
                    system.SetSelectedGameObject(null);
                }

                return;
            }

            ConstrainFocusCore(true);
        }

        public void ConstrainFocus() => ConstrainFocusCore(false);

        private void ConstrainFocusCore(bool acquiringFocus)
        {
            RequireAlive();
            var system = EventSystem.current;
            if (system == null || system.alreadySelecting)
            {
                return;
            }

            var selected = system.currentSelectedGameObject;
            if (!IsInputEnabled)
            {
                if (BelongsToView(selected))
                {
                    previousSelection = selected;
                    system.SetSelectedGameObject(null);
                }

                return;
            }

            if (CanSelect(selected))
            {
                previousSelection = selected;
                return;
            }

            if (!acquiringFocus && selected != null)
            {
                var host = GetComponentInParent<UIHost>();
                if (host != null && !selected.transform.IsChildOf(host.transform))
                {
                    return;
                }
            }

            GameObject candidate = CanSelect(previousSelection) ? previousSelection : null;
            if (candidate == null && defaultSelection != null && CanSelect(defaultSelection.gameObject))
            {
                candidate = defaultSelection.gameObject;
            }

            if (candidate == null)
            {
                foreach (var selectable in GetComponentsInChildren<Selectable>())
                {
                    if (selectable != null && CanSelect(selectable.gameObject))
                    {
                        candidate = selectable.gameObject;
                        break;
                    }
                }
            }

            if (selected != candidate)
            {
                system.SetSelectedGameObject(candidate);
            }
        }

        private bool BelongsToView(GameObject candidate) => candidate != null && candidate.transform.IsChildOf(transform);

        private bool CanSelect(GameObject candidate)
        {
            if (!BelongsToView(candidate) || !candidate.activeInHierarchy)
            {
                return false;
            }

            var selectable = candidate.GetComponent<Selectable>();
            return selectable != null && selectable.isActiveAndEnabled && selectable.IsInteractable();
        }
    }
}
