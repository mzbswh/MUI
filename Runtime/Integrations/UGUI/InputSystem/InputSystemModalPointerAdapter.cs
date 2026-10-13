using System;
using MUI.UGUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;

namespace MUI.UGUI.InputSystem
{
    internal static class InputSystemModalPointerAdapter
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => ModalPointerBarrier.RegisterPointerPressStateProvider(Capture);

        private static Func<bool> Capture(BaseInputModule module, PointerEventData pointer, PointerEventData.InputButton button)
        {
            var input = module as InputSystemUIInputModule;
            var extended = pointer as ExtendedPointerEventData;
            if (input == null || extended == null || extended.device == null)
            {
                return null;
            }

            InputActionReference reference;
            switch (button)
            {
                case PointerEventData.InputButton.Right:
                    reference = input.rightClick;
                    break;
                case PointerEventData.InputButton.Middle:
                    reference = input.middleClick;
                    break;
                default:
                    reference = input.leftClick;
                    break;
            }

            if (reference == null || reference.action == null)
            {
                return null;
            }

            var action = reference.action;
            var device = extended.device;
            if (extended.touchId != 0 && device is Touchscreen touchscreen)
            {
                var touchId = extended.touchId;
                return () =>
                {
                    if (!touchscreen.added)
                    {
                        return false;
                    }

                    foreach (var touch in touchscreen.touches)
                    {
                        if (touch.touchId.ReadValue() == touchId)
                        {
                            return touch.press.isPressed;
                        }
                    }

                    return false;
                };
            }

            return () =>
            {
                if (!action.enabled || !device.added)
                {
                    return false;
                }

                foreach (var control in action.controls)
                {
                    if (ReferenceEquals(control.device, device) && control is ButtonControl press && press.isPressed)
                    {
                        return true;
                    }
                }

                return false;
            };
        }
    }
}
