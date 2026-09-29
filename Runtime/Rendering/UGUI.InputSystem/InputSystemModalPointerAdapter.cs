using System;
using MUI.UGUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace MUI.UGUI.InputSystem
{
    internal static class InputSystemModalPointerAdapter
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => ModalPointerBarrier.RegisterPressStateProvider(Capture);

        private static Func<bool> Capture(BaseInputModule module, PointerEventData.InputButton button)
        {
            var input = module as InputSystemUIInputModule;
            if (input == null)
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
            return action.IsPressed;
        }
    }
}
