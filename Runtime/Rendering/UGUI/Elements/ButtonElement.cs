using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MUI.UGUI
{
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class ButtonElement : Element, IPointerDownHandler
    {
        private Button target;
        private PointerEventData pressedPointer;

        public event Action Clicked;

        private Button Target
        {
            get
            {
                RequireAlive();
                if (target == null)
                {
                    target = RequireComponent<Button>();
                }

                return target;
            }
        }

        public bool Interactable
        {
            get => Target.interactable;
            set
            {
                if (Target.interactable == value)
                {
                    return;
                }

                Target.interactable = value;
                NotifyChanged();
            }
        }

        protected override AccessibilityRole DefaultAccessibilityRole => AccessibilityRole.Button;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Left && CanReceiveInput(target))
            {
                pressedPointer = eventData;
            }
        }

        protected override void OnInitialize()
        {
            target = RequireComponent<Button>();
            UnityAction handler = () =>
            {
                if (CanReceiveInput(target))
                {
                    if (pressedPointer != null && pressedPointer.eligibleForClick && pressedPointer.pointerPress == gameObject)
                    {
                        var owner = GetComponentInParent<View>(true);
                        if (owner != null)
                        {
                            owner.CaptureModalPointer(pressedPointer);
                        }
                    }

                    Clicked?.Invoke();
                }
            };
            OnDispose(() =>
            {
                if (target != null)
                {
                    target.onClick.RemoveListener(handler);
                }

                Clicked = null;
                pressedPointer = null;
            });
            target.onClick.AddListener(handler);
        }
    }
}
