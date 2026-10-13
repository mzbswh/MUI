using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MUI.UGUI
{
    public sealed partial class DragSourceElement
    {
        private static readonly Dictionary<PointerIdentity, DragSourceElement> activePointers =
            new Dictionary<PointerIdentity, DragSourceElement>();
        private PointerIdentity pointer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPointers()
        {
            activePointers.Clear();
        }

        private bool ReservePointer(PointerIdentity identity)
        {
            if (!identity.IsCurrentModule)
            {
                return false;
            }

            if (activePointers.TryGetValue(identity, out var previous) && previous != null && previous.IsAlive)
            {
                return false;
            }

            pointer = identity;
            activePointers[identity] = this;
            return true;
        }

        private void ReleasePointer()
        {
            if (activePointers.TryGetValue(pointer, out var current) && ReferenceEquals(current, this))
            {
                activePointers.Remove(pointer);
            }
        }

        private bool MatchesPointer(PointerEventData data)
        {
            return data != null && pointer.IsCurrentModule && pointer.Equals(PointerIdentity.From(data));
        }

        /// <summary>悬停属于指针位置，查询该输入模块下实际活动的按钮流，不读取缓存事件的 pointerDrag。</summary>
        internal static bool CanDropAtPointer(PointerIdentity hovered, DropBinding target)
        {
            if (!hovered.IsCurrentModule)
            {
                return false;
            }

            for (var button = 0; button < 3; ++button)
            {
                var identity = hovered.ForButton((PointerEventData.InputButton)button);
                if (!activePointers.TryGetValue(identity, out var source) || source == null || !source.IsAlive)
                {
                    continue;
                }

                var candidate = source.interaction;
                var generation = source.generation;
                if (candidate == null || !candidate.IsDragging || !source.CanDrag())
                {
                    continue;
                }

                if (candidate.CanDrop(target) && source != null && source.IsAlive &&
                    source.generation == generation && ReferenceEquals(source.interaction, candidate) &&
                    candidate.IsDragging && source.CanDrag() &&
                    activePointers.TryGetValue(identity, out var current) && ReferenceEquals(current, source))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
