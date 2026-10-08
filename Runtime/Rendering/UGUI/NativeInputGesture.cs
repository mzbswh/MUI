using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>借用原生 Pointer 状态，在换绑或输入失效时结束捕获，阻止剩余手势命中新对象。</summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class NativeInputGesture : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IInitializePotentialDragHandler, IEndDragHandler
    {
        private readonly List<PointerCapture> captures = new List<PointerCapture>();
        private View owner;
        private Selectable selectable;
        private ScrollRect scroll;

        private bool CanCapture
        {
            get
            {
                if (owner == null || !owner.IsInputEnabled || !isActiveAndEnabled ||
                    selectable != null && (!selectable.isActiveAndEnabled || !selectable.IsInteractable()) ||
                    scroll != null && !scroll.isActiveAndEnabled)
                {
                    return false;
                }

                var host = owner.GetComponentInParent<UIHost>(true);
                return host == null || host.CanReceiveSharedPointerInput();
            }
        }

        internal static NativeInputGesture Attach(View view, GameObject node)
        {
            var observer = node.GetComponent<NativeInputGesture>();
            if (observer == null)
            {
                observer = node.AddComponent<NativeInputGesture>();
            }

            observer.owner = view;
            observer.selectable = node.GetComponent<Selectable>();
            observer.scroll = node.GetComponent<ScrollRect>();
            return observer;
        }

        public void OnPointerDown(PointerEventData eventData) => Capture(eventData);

        // 不实现 IDragHandler，避免让普通按钮变成输入模块的拖动目标。
        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (eventData != null && !eventData.eligibleForClick)
            {
                // 输入模块在 PointerDown 回调返回后才赋值 pointerDrag。
                // 回调中失效的按下不能因随后恢复门控而重新取得拖动资格。
                Cancel(new PointerCapture(eventData), true);
                return;
            }

            Capture(eventData);
        }

        public void OnPointerUp(PointerEventData eventData) => Forget(eventData, false);

        public void OnEndDrag(PointerEventData eventData) => Forget(eventData, true);

        private void Capture(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (!CanCapture)
            {
                Cancel(new PointerCapture(eventData), true);
                return;
            }

            for (var i = captures.Count - 1; i >= 0; --i)
            {
                if (captures[i].Matches(eventData))
                {
                    return;
                }
            }

            captures.Add(new PointerCapture(eventData));
        }

        private void Forget(PointerEventData eventData, bool endingDrag)
        {
            if (eventData == null || !endingDrag && eventData.dragging && eventData.pointerDrag == gameObject)
            {
                return;
            }

            for (var i = captures.Count - 1; i >= 0; --i)
            {
                if (captures[i].Matches(eventData))
                {
                    captures.RemoveAt(i);
                }
            }
        }

        internal void Invalidate()
        {
            if (captures.Count == 0)
            {
                if (scroll != null)
                {
                    scroll.StopMovement();
                }

                return;
            }

            // 先撤销拥有权；原生或适配器收尾回调重入时，不会再次结束同一捕获。
            var previous = captures.ToArray();
            captures.Clear();
            foreach (var capture in previous)
            {
                try
                {
                    Cancel(capture, true);
                }
                catch (Exception error)
                {
                    UIErrors.Report(error);
                }
            }
        }

        private void Cancel(PointerCapture capture, bool cleanNativeState)
        {
            var data = capture.Data;
            if (!capture.Matches(data))
            {
                return;
            }

            // 禁止 Click 与 Drop；不调用 ExecuteEvents，避免派发项目的结束业务回调。
            data.eligibleForClick = false;
            var ownsDrag = data.pointerDrag == gameObject;
            if (ownsDrag)
            {
                data.pointerDrag = null;
                data.dragging = false;
            }

            if (data.pointerPress == gameObject)
            {
                data.pointerPress = null;
                data.rawPointerPress = null;
            }

            if (!cleanNativeState)
            {
                return;
            }

            if (selectable != null)
            {
                selectable.OnPointerUp(data);
            }

            if (ownsDrag && selectable is IEndDragHandler endDrag)
            {
                // Legacy/TMP 输入框沿原生接口停止选区拖动，无需引入后端程序集依赖。
                endDrag.OnEndDrag(data);
            }

            if (scroll != null)
            {
                scroll.OnEndDrag(data);
                scroll.StopMovement();
                var listInput = GetComponent<VirtualListScrollInput>();
                if (listInput != null)
                {
                    listInput.OnEndDrag(data);
                }
            }
        }

        private void OnCanvasGroupChanged()
        {
            if (owner != null && !CanCapture)
            {
                Invalidate();
            }
        }

        private void OnDisable() => Invalidate();

        private sealed class PointerCapture
        {
            private readonly BaseInputModule module;
            private readonly int pointerId;
            private readonly PointerEventData.InputButton button;

            internal PointerCapture(PointerEventData data)
            {
                Data = data;
                module = data.currentInputModule;
                pointerId = data.pointerId;
                button = data.button;
            }

            internal PointerEventData Data
            {
                get;
            }

            internal bool Matches(PointerEventData data) => data != null &&
                ReferenceEquals(Data, data) && data.currentInputModule == module &&
                data.pointerId == pointerId && data.button == button;
        }
    }
}
