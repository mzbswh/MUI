using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MUI.UGUI
{
    /// <summary>只持有本次 EventSystem 事件的 pointerDrag 关联，不是操作系统级指针捕获。</summary>
    internal sealed class PointerDragCapture : IDisposable
    {
        private PointerEventData data;
        private readonly GameObject source;
        private readonly PointerIdentity pointer;
        private Action releaseVisual;

        internal PointerDragCapture(PointerEventData data, GameObject source, Action releaseVisual = null)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (data.pointerDrag != source)
            {
                throw new InvalidOperationException("The input module has not assigned this pointer's drag source.");
            }

            pointer = PointerIdentity.From(data);
            this.data = data;
            this.source = source;
            this.releaseVisual = releaseVisual;
        }

        public void Dispose()
        {
            var previous = data;
            data = null;
            var cleanup = releaseVisual;
            releaseVisual = null;
            try
            {
                // 输入模块可能已把同一个事件对象切换到另一鼠标键。
                // 不修改不属于本次捕获的状态，视觉收尾仍始终执行。
                if (previous == null || !pointer.Equals(PointerIdentity.From(previous)) || previous.pointerDrag != source)
                {
                    return;
                }

                previous.pointerDrag = null;
                previous.dragging = false;
            }
            finally
            {
                if (cleanup != null)
                {
                    cleanup();
                }
            }
        }
    }
}
