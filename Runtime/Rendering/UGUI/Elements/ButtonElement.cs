using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MUI.UGUI
{
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class ButtonElement : Element
    {
        private Button target;

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

        protected override void OnInitialize()
        {
            target = RequireComponent<Button>();
            UnityAction handler = () =>
            {
                if (CanReceiveInput(target))
                {
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
            });
            target.onClick.AddListener(handler);
        }
    }
}
