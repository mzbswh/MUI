using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MUI.UGUI
{
    [DisallowMultipleComponent]
    public sealed partial class DropTargetElement : Element, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private DropBinding target;
        [SerializeField, Min(1)]
        private int maxPendingDrops = 16;
        private readonly HashSet<DragInteraction> pending = new HashSet<DragInteraction>();
        private bool accepting;

        public DropBinding Target
        {
            get
            {
                RequireAlive();
                return target;
            }

            set
            {
                Initialize();
                if (ReferenceEquals(target, value))
                {
                    return;
                }

                target = value;
                ClearHover();
                CancelPending();
                if (IsAlive)
                {
                    NotifyChanged();
                }
            }
        }

        protected override void OnInitialize()
        {
            if (maxPendingDrops < 1)
            {
                throw new InvalidOperationException("Drop target capacity must be positive.");
            }

            ValidateHighlight();
            OnDispose(() =>
            {
                target = null;
                ClearHover();
                CancelPending();
            });
            SetDropAllowed(false);
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (accepting || !CanDrop() || eventData == null || eventData.used || eventData.pointerDrag == null || pending.Count >= maxPendingDrops)
            {
                return;
            }

            var source = eventData.pointerDrag.GetComponent<DragSourceElement>();
            if (source == null)
            {
                return;
            }

            var expectedTarget = target;
            accepting = true;
            try
            {
                if (!source.TryDrop(eventData, expectedTarget, out var accepted))
                {
                    return;
                }

                eventData.Use();
                SetDropAllowed(false);
                if (!accepted.IsCompleted)
                {
                    pending.Add(accepted);
                    _ = ObserveAsync(accepted);
                    if (!CanDrop() || !ReferenceEquals(target, expectedTarget))
                    {
                        accepted.Cancel();
                    }
                }
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
            finally
            {
                accepting = false;
            }
        }

        private bool CanDrop()
        {
            if (!IsAlive || !isActiveAndEnabled || target == null)
            {
                return false;
            }

            var selectable = GetComponent<Selectable>();
            if (selectable != null && !CanReceiveInput(selectable))
            {
                return false;
            }

            var view = GetComponentInParent<View>(true);
            return view == null || view.IsInputEnabled;
        }

        private void OnDisable()
        {
            ClearHover();
            CancelPending();
        }

        private void Update()
        {
            if (pending.Count != 0 && !CanDrop())
            {
                CancelPending();
            }

            RefreshHover();
        }

        private void CancelPending()
        {
            var snapshot = new DragInteraction[pending.Count];
            pending.CopyTo(snapshot);
            foreach (var interaction in snapshot)
            {
                try
                {
                    interaction.Cancel();
                }
                catch (Exception error)
                {
                    UIErrors.Report(error);
                }
            }
        }

        private async Task ObserveAsync(DragInteraction interaction)
        {
            try
            {
                await interaction.Completion;
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
            finally
            {
                pending.Remove(interaction);
            }
        }
    }
}
