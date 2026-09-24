using System;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    public abstract partial class Element
    {
        [SerializeField]
        private string accessibilityLabel = string.Empty;
        [SerializeField]
        private string accessibilityDescription = string.Empty;
        [SerializeField]
        private string accessibilityValue = string.Empty;
        [SerializeField]
        private AccessibilityRole semanticRole = AccessibilityRole.Unspecified;
        [SerializeField]
        private AccessibilityState semanticState;
        [SerializeField]
        private int accessibilityOrder;
        [SerializeField]
        private bool accessibilityHidden;

        protected virtual AccessibilityRole DefaultAccessibilityRole => AccessibilityRole.Generic;

        protected virtual string DefaultAccessibilityLabel => string.Empty;

        protected virtual AccessibilityState NativeAccessibilityState => AccessibilityState.None;

        public string AccessibilityLabel
        {
            get
            {
                RequireAlive();
                return string.IsNullOrEmpty(accessibilityLabel) ? DefaultAccessibilityLabel : accessibilityLabel;
            }

            set
            {
                RequireAlive();
                value = value ?? string.Empty;
                if (accessibilityLabel == value)
                {
                    return;
                }

                accessibilityLabel = value;
                NotifyChanged();
            }
        }

        public string AccessibilityDescription
        {
            get
            {
                RequireAlive();
                return accessibilityDescription;
            }

            set
            {
                RequireAlive();
                value = value ?? string.Empty;
                if (accessibilityDescription == value)
                {
                    return;
                }

                accessibilityDescription = value;
                NotifyChanged();
            }
        }

        /// <summary>显式朗读值，不自动暴露 InputField 文本或密码内容。</summary>
        public string AccessibilityValue
        {
            get
            {
                RequireAlive();
                return accessibilityValue;
            }

            set
            {
                RequireAlive();
                value = value ?? string.Empty;
                if (accessibilityValue == value)
                {
                    return;
                }

                accessibilityValue = value;
                NotifyChanged();
            }
        }

        public AccessibilityRole SemanticRole
        {
            get
            {
                RequireAlive();
                return semanticRole == AccessibilityRole.Unspecified ? DefaultAccessibilityRole : semanticRole;
            }

            set
            {
                RequireAlive();
                if (!Enum.IsDefined(typeof(AccessibilityRole), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                if (semanticRole == value)
                {
                    return;
                }

                semanticRole = value;
                NotifyChanged();
            }
        }

        public AccessibilityState SemanticState
        {
            get
            {
                RequireAlive();
                var value = semanticState | NativeAccessibilityState;
                var selectable = GetComponent<Selectable>();
                if (selectable != null && !CanReceiveInput(selectable))
                {
                    value |= AccessibilityState.Disabled;
                }

                return value;
            }

            set
            {
                RequireAlive();
                const AccessibilityState known = AccessibilityState.Disabled | AccessibilityState.Selected | AccessibilityState.Checked | AccessibilityState.Expanded | AccessibilityState.ReadOnly | AccessibilityState.Busy | AccessibilityState.Invalid;
                if ((value & ~known) != 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                if (semanticState == value)
                {
                    return;
                }

                semanticState = value;
                NotifyChanged();
            }
        }

        public int AccessibilityOrder
        {
            get
            {
                RequireAlive();
                return accessibilityOrder;
            }

            set
            {
                RequireAlive();
                if (accessibilityOrder == value)
                {
                    return;
                }

                accessibilityOrder = value;
                NotifyChanged();
            }
        }

        public bool AccessibilityHidden
        {
            get
            {
                RequireAlive();
                return accessibilityHidden;
            }

            set
            {
                RequireAlive();
                if (accessibilityHidden == value)
                {
                    return;
                }

                accessibilityHidden = value;
                NotifyChanged();
            }
        }
    }
}
