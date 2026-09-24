using System;
using MUI.DragDrop;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MUI.UGUI
{
    [DisallowMultipleComponent]
    public sealed partial class DragSourceElement : Element, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private DragBinding source;
        private DragInteraction interaction;
        private long generation;
        private bool starting;

        public event Action<Vector2> Dragged;

        public event Action<DropResult> Finished;

        public DragBinding Source
        {
            get
            {
                RequireAlive();
                return source;
            }

            set
            {
                Initialize();
                if (ReferenceEquals(source, value))
                {
                    return;
                }

                var previous = interaction;
                source = value;
                EndGhost();
                if (previous != null)
                {
                    previous.Cancel();
                }

                if (IsAlive)
                {
                    NotifyChanged();
                }
            }
        }

        protected override void OnInitialize()
        {
            ValidateGhost();
            OnDispose(() =>
            {
                EndGhost();
                var previous = interaction;
                interaction = null;
                ReleasePointer();
                ++generation;
                source = null;
                Dragged = null;
                Finished = null;
                if (previous != null)
                {
                    previous.Cancel();
                }
            });
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData == null || eventData.used || source == null || !CanDrag() || interaction != null || starting)
            {
                return;
            }

            Initialize();
            if (!ReservePointer(PointerIdentity.From(eventData)))
            {
                return;
            }

            starting = true;
            var expected = ++generation;
            PointerDragCapture capture = null;
            try
            {
                capture = new PointerDragCapture(eventData, gameObject, () => EndGhost(expected));
                BeginGhost(eventData, expected);
                var binding = source;
                var created = binding.Begin(capture, result => Settle(expected, binding, result));
                if (!IsAlive || !isActiveAndEnabled || generation != expected || !ReferenceEquals(source, binding))
                {
                    created.Cancel();
                }
                else if (!created.IsCompleted)
                {
                    interaction = created;
                }
            }
            catch (Exception error)
            {
                if (capture != null)
                {
                    capture.Dispose();
                }

                UIErrors.Report(error);
            }
            finally
            {
                starting = false;
                if (interaction == null)
                {
                    ReleasePointer();
                }
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData == null || interaction == null || !interaction.IsDragging || !MatchesPointer(eventData))
            {
                return;
            }

            if (!CanDrag())
            {
                interaction.Cancel();
                return;
            }

            var expected = generation;
            var active = interaction;
            var position = eventData.position;
            var ghostPositioned = MoveGhost(position);
            if (generation != expected || !ReferenceEquals(interaction, active) || !active.IsDragging)
            {
                return;
            }

            if (ghost != null)
            {
                ghost.gameObject.SetActive(ghostPositioned);
            }

            var handlers = Dragged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<Vector2> handler in handlers.GetInvocationList())
            {
                if (!IsAlive || generation != expected || !ReferenceEquals(interaction, active) || !active.IsDragging)
                {
                    break;
                }

                try
                {
                    handler(position);
                }
                catch (Exception error)
                {
                    UIErrors.Report(error);
                }
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData != null && interaction != null && MatchesPointer(eventData) && interaction.IsDragging)
            {
                interaction.Cancel();
            }
        }

        internal bool TryDrop(PointerEventData data, DropBinding target, out DragInteraction accepted)
        {
            accepted = null;
            if (data == null || target == null || interaction == null || !interaction.IsDragging || !MatchesPointer(data) || !CanDrag())
            {
                return false;
            }

            var candidate = interaction;
            var expected = generation;
            if (!candidate.TryDrop(target))
            {
                return false;
            }

            // 投放后的异步业务不再占用指针按钮流，下一次手势可以落到其他来源。
            if (generation == expected)
            {
                ReleasePointer();
            }

            accepted = candidate;
            return true;
        }

        private void OnDisable()
        {
            EndGhost();
            if (interaction != null)
            {
                interaction.Cancel();
            }
        }

        private void Update()
        {
            if (interaction != null)
            {
                // Pump 可能同步取消并通过 Settle 清空 interaction，之后重新检查引用。
                interaction.Pump();
            }
            if (interaction != null && (!pointer.IsCurrentModule || !CanDrag()))
            {
                interaction.Cancel();
            }
        }

        private bool CanDrag()
        {
            if (!IsAlive || !isActiveAndEnabled)
            {
                return false;
            }

            var selectable = GetComponent<Selectable>();
            if (selectable != null)
            {
                return CanReceiveInput(selectable);
            }

            var view = GetComponentInParent<View>(true);
            return view == null || view.IsInputEnabled;
        }

        private void Settle(long expected, DragBinding binding, DropResult result)
        {
            EndGhost(expected);
            if (expected != generation || !IsAlive)
            {
                return;
            }

            interaction = null;
            ReleasePointer();
            if (!ReferenceEquals(source, binding))
            {
                return;
            }

            var handlers = Finished;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<DropResult> handler in handlers.GetInvocationList())
            {
                if (!IsAlive || expected != generation)
                {
                    break;
                }

                try
                {
                    handler(result);
                }
                catch (Exception error)
                {
                    UIErrors.Report(error);
                }
            }
        }
    }
}
