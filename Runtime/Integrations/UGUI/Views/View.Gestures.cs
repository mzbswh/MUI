using System;
using System.Collections.Generic;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class View
    {
        private readonly List<NativeInputGesture> nativeGestures = new List<NativeInputGesture>();
        private bool invalidatingGestures;

        public event Action InputGesturesInvalidated;

        /// <summary>同步结束原生及自定义捕获；清理不受输入门控阻断，观察者异常隔离报告。</summary>
        public void InvalidateInputGestures()
        {
            UnityMainThread.Require();
            if (invalidatingGestures)
            {
                return;
            }

            invalidatingGestures = true;
            try
            {
                foreach (var gesture in nativeGestures.ToArray())
                {
                    if (gesture != null)
                    {
                        gesture.Invalidate();
                    }
                }

                var handlers = InputGesturesInvalidated;
                if (handlers != null)
                {
                    foreach (Action handler in handlers.GetInvocationList())
                    {
                        try
                        {
                            handler();
                        }
                        catch (Exception error)
                        {
                            UIErrors.Report(error);
                        }
                    }
                }
            }
            finally
            {
                invalidatingGestures = false;
            }
        }

        internal void AttachNativeGesture(Transform node)
        {
            if (node.GetComponent<UnityEngine.UI.Selectable>() != null ||
                node.GetComponent<UnityEngine.UI.ScrollRect>() != null)
            {
                var gesture = NativeInputGesture.Attach(this, node.gameObject);
                if (!nativeGestures.Contains(gesture))
                {
                    nativeGestures.Add(gesture);
                }
            }
        }
    }
}
