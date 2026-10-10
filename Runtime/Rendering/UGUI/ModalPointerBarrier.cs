using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>视觉退出后保留透明的模态射线屏障，直到原输入模块释放关闭手势。</summary>
    [DisallowMultipleComponent]
    public sealed class ModalPointerBarrier : MonoBehaviour
    {
        private static readonly List<ModalPointerBarrier> held = new List<ModalPointerBarrier>();
        private static readonly List<PressStateRegistration> pressStateProviders = new List<PressStateRegistration>();
        private int? closingRenderOrder;
        private int closingSortingLayer;
        private PointerEventData pointer;
        private BaseInputModule module;
        private Func<bool> isPressed;
        private int pointerId;
        private PointerEventData.InputButton pointerButton;
        private int releaseFrame;
        private bool releasing;
        private bool standaloneInputFailed;

        internal void SetClosingRenderOrder(int sortingLayerId, int order)
        {
            closingSortingLayer = sortingLayerId;
            closingRenderOrder = order;
        }

        /// <summary>兼容不区分指针的输入适配器；多指针模块应使用包含 PointerEventData 的重载。</summary>
        public static IDisposable RegisterPressStateProvider(Func<BaseInputModule, PointerEventData.InputButton, Func<bool>> provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            return RegisterPointerPressStateProvider((inputModule, eventData, button) => provider(inputModule, button));
        }

        /// <summary>按关闭手势的指针登记状态读取器；不支持该模块时返回 null，凭证用于撤销登记。</summary>
        public static IDisposable RegisterPointerPressStateProvider(
            Func<BaseInputModule, PointerEventData, PointerEventData.InputButton, Func<bool>> provider)
        {
            UnityMainThread.Require();
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            var registration = new PressStateRegistration(provider);
            pressStateProviders.Add(registration);
            return registration;
        }

        internal void HoldUntilRelease(PointerEventData eventData, BaseInputModule inputModule, PointerEventData.InputButton button)
        {
            pointer = eventData;
            module = inputModule;
            pointerId = eventData.pointerId;
            pointerButton = button;
            CapturePressState(button);
            releaseFrame = Time.frameCount;
            releasing = true;
            var image = GetComponent<Image>();
            if (image != null)
            {
                image.color = Color.clear;
            }

            if (closingRenderOrder.HasValue)
            {
                var canvas = GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.overrideSorting = true;
                    canvas.sortingLayerID = closingSortingLayer;
                    canvas.sortingOrder = closingRenderOrder.Value;
                }
            }
            held.Add(this);
            transform.SetAsLastSibling();
        }

        internal static void RaiseHeldBarriers(Transform parent)
        {
            for (var i = held.Count - 1; i >= 0; --i)
            {
                if (i >= held.Count)
                {
                    continue;
                }

                var barrier = held[i];
                if (barrier != null && barrier.transform.parent == parent)
                {
                    barrier.transform.SetAsLastSibling();
                }
            }
        }

        private void LateUpdate()
        {
            if (!releasing || Time.frameCount == releaseFrame)
            {
                return;
            }

            var system = module == null ? null : module.GetComponent<EventSystem>();
            if (pointer == null || module == null || system == null || !system.isActiveAndEnabled || !system.isFocused ||
                system.currentInputModule != module ||
                !IsPressActive())
            {
                Destroy(gameObject);
            }
        }

        private void CapturePressState(PointerEventData.InputButton button)
        {
            if (module == null)
            {
                return;
            }

            var providers = pressStateProviders.ToArray();
            for (var i = providers.Length - 1; i >= 0; --i)
            {
                var provider = providers[i].Provider;
                if (provider == null)
                {
                    continue;
                }

                try
                {
                    isPressed = provider(module, pointer, button);
                    if (isPressed != null)
                    {
                        return;
                    }
                }
                catch (Exception error)
                {
                    UIErrors.Report(error);
                }
            }
        }

        private bool IsPressActive()
        {
            if (pointer.pointerId != pointerId || pointer.button != pointerButton || pointer.currentInputModule != module)
            {
                return false;
            }

            if (isPressed != null)
            {
                try
                {
                    return isPressed();
                }
                catch (Exception error)
                {
                    // 输入模块卸载期间沿用原生 Pointer 状态完成收尾。
                    isPressed = null;
                    UIErrors.Report(error);
                }
            }

            if (!standaloneInputFailed)
            {
                try
                {
                    var pressed = ReadStandalonePress();
                    if (pressed.HasValue)
                    {
                        return pressed.Value;
                    }
                }
                catch (Exception error)
                {
                    standaloneInputFailed = true;
                    UIErrors.Report(error);
                }
            }

            return pointer.eligibleForClick || pointer.pointerPress != null || pointer.dragging;
        }

        private bool? ReadStandalonePress()
        {
            if (!(module is StandaloneInputModule))
            {
                return null;
            }

            var input = module.input;
            if (input == null)
            {
                return null;
            }

            if (pointerId >= 0 && pointerButton == PointerEventData.InputButton.Left)
            {
                for (var i = 0; i < input.touchCount; ++i)
                {
                    var touch = input.GetTouch(i);
                    if (touch.fingerId == pointerId && touch.type != TouchType.Indirect)
                    {
                        return touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
                    }
                }

                return false;
            }

            if (pointerId == PointerInputModule.kMouseLeftId - (int)pointerButton)
            {
                return input.GetMouseButton((int)pointerButton);
            }

            return null;
        }

        private void OnDisable()
        {
            if (releasing)
            {
                held.Remove(this);
                Destroy(gameObject);
            }
        }

        private void OnDestroy() => held.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            held.Clear();
            foreach (var registration in pressStateProviders)
            {
                registration.Clear();
            }

            pressStateProviders.Clear();
        }

        private sealed class PressStateRegistration : IDisposable
        {
            private Func<BaseInputModule, PointerEventData, PointerEventData.InputButton, Func<bool>> provider;

            public PressStateRegistration(Func<BaseInputModule, PointerEventData, PointerEventData.InputButton, Func<bool>> provider)
            {
                this.provider = provider;
            }

            public Func<BaseInputModule, PointerEventData, PointerEventData.InputButton, Func<bool>> Provider => provider;

            public void Dispose()
            {
                UnityMainThread.Require();
                if (provider == null)
                {
                    return;
                }

                pressStateProviders.Remove(this);
                Clear();
            }

            public void Clear() => provider = null;
        }
    }
}
