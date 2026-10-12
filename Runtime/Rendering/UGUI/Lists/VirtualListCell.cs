using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace MUI.UGUI
{
    internal sealed class VirtualListCell
    {
        // 换源或 Reset 后，即使业务键相同，也必须解除旧来源的绑定和焦点。
        public long SourceGeneration;
        public RectTransform Root;
        public RectTransform MeasurementRoot;
        public NestedViewElement Element;
        public object Key;
        public string TemplateKey;
        public VirtualListItemSelection Selection;
        public Button SelectionButton;
        public UnityEngine.Events.UnityAction SelectionHandler;
        public readonly List<VirtualListFocusInput> FocusInputs = new List<VirtualListFocusInput>();
        internal void Release()
        {
            if (Element != null)
            {
                Element.ReleaseOwnedNode(() => ReleaseNative(), "VirtualListCell");
            }
            else
            {
                ReleaseNative();
            }
        }
        private void ReleaseNative()
        {
            var errors = new List<Exception>();
            try
            {
                View.ReleaseFocusWithin(Root);
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            try
            {
                DetachSelection();
                DetachFocus();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            if (errors.Count != 0)
            {
                throw new AggregateException("Virtual list cell cleanup failed.", errors);
            }

            if (Root != null)
            {
                UnityEngine.Object.Destroy(Root.gameObject);
            }
        }
        internal void DetachSelection()
        {
            if (SelectionButton != null && SelectionHandler != null)
            {
                SelectionButton.onClick.RemoveListener(SelectionHandler);
            }

            SelectionHandler = null;
            if (Selection != null)
            {
                Selection.SetSelected(false);
            }
        }
        internal void DetachFocus()
        {
            foreach (var relay in FocusInputs)
            {
                if (relay != null)
                {
                    relay.Detach();
                    UnityEngine.Object.Destroy(relay);
                }
            }

            FocusInputs.Clear();
        }
    }
}
