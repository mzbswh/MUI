using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MUI.UGUI
{
    public sealed partial class DropTargetElement
    {
        [SerializeField]
        private GameObject allowedHighlight = null;
        private readonly HashSet<PointerIdentity> hovered = new HashSet<PointerIdentity>();
        private readonly List<PointerIdentity> hoverSnapshot = new List<PointerIdentity>(16);
        private bool refreshingHover;
        private bool dropAllowed;

        public bool IsDropAllowed
        {
            get
            {
                RequireAlive();
                return dropAllowed;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsAlive || !isActiveAndEnabled || eventData == null)
            {
                return;
            }

            hovered.RemoveWhere(entry => !entry.IsCurrentModule);
            var identity = HoverIdentity(eventData);
            if (hovered.Count >= 16 && !hovered.Contains(identity))
            {
                return;
            }

            hovered.Add(identity);
            RefreshHover();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (eventData == null)
            {
                return;
            }

            hovered.Remove(HoverIdentity(eventData));
            RefreshHover();
        }

        private static PointerIdentity HoverIdentity(PointerEventData data)
        {
            // 悬停不区分按键；真实拖放身份由来源注册表提供。
            return new PointerIdentity(data.currentInputModule, data.pointerId, PointerEventData.InputButton.Left);
        }

        private void RefreshHover()
        {
            if (!IsAlive || refreshingHover)
            {
                return;
            }

            refreshingHover = true;
            try
            {
                hovered.RemoveWhere(entry => !entry.IsCurrentModule);
                var allowed = false;
                var expected = target;
                if (CanDrop() && !accepting && pending.Count < maxPendingDrops)
                {
                    // 业务谓词可能触发 UI 回调，不能枚举可被回调修改的实时集合。
                    hoverSnapshot.Clear();
                    foreach (var data in hovered)
                    {
                        hoverSnapshot.Add(data);
                    }

                    foreach (var data in hoverSnapshot)
                    {
                        if (DragSourceElement.CanDropAtPointer(data, expected) && hovered.Contains(data))
                        {
                            allowed = true;
                            break;
                        }
                    }
                }

                SetDropAllowed(allowed && CanDrop() && ReferenceEquals(target, expected) && hovered.Count != 0);
            }
            catch (Exception error)
            {
                // 谓词出错后，同一次悬停不能每帧重复抛出异常。
                ClearHover();
                UIErrors.Report(error);
            }
            finally
            {
                hoverSnapshot.Clear();
                refreshingHover = false;
            }
        }

        private void ClearHover()
        {
            hovered.Clear();
            SetDropAllowed(false);
        }

        private void SetDropAllowed(bool value)
        {
            var changed = dropAllowed != value;
            dropAllowed = value;
            if (allowedHighlight != null && allowedHighlight.activeSelf != value)
            {
                allowedHighlight.SetActive(value);
            }

            if (changed && IsAlive)
            {
                NotifyChanged(nameof(IsDropAllowed));
            }
        }

        private void ValidateHighlight()
        {
            if (allowedHighlight != null && (allowedHighlight.transform == transform || !allowedHighlight.transform.IsChildOf(transform)))
            {
                throw new InvalidOperationException("Drop highlight must be a strict child of its target.");
            }
        }
    }
}
