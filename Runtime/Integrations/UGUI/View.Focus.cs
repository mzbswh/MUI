using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MUI.UGUI
{
    public sealed partial class View
    {
        [SerializeField]
        private Selectable defaultSelection = null;
        private GameObject previousSelection;
        private bool wantsNativeFocus;
        private VirtualListElement previousVirtualList;
        private VirtualListFocusPosition previousVirtualFocus;
        private CancellationTokenSource focusRestoration;

        internal bool WantsNativeFocus => wantsNativeFocus;

        /// <summary>池化节点换绑前解除原生焦点及页面保存的节点引用，防止焦点随对象复用转移到其他数据。</summary>
        internal static void ReleaseFocusWithin(Transform boundary)
        {
            if (boundary == null)
            {
                return;
            }

            // 同时清除条目自身和外层页面的历史选择；仅清空 EventSystem 会被页面下一帧重新选回。
            foreach (var view in boundary.GetComponentsInChildren<View>(true))
            {
                view.ForgetSelectionWithin(boundary);
            }

            foreach (var view in boundary.GetComponentsInParent<View>(true))
            {
                var inputSystem = GetInputEventSystem(boundary);
                var inputSelection = inputSystem == null ? null : inputSystem.currentSelectedGameObject;
                if (inputSelection != null && inputSelection.transform.IsChildOf(boundary))
                {
                    view.RememberFocus(inputSelection);
                }

                view.ForgetSelectionWithin(boundary);
            }

            var system = GetInputEventSystem(boundary);
            var selected = system == null ? null : system.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(boundary))
            {
                if (system.alreadySelecting)
                {
                    throw new System.InvalidOperationException("Cannot recycle a focused UI node during a selection callback.");
                }

                NativeFocusObserver.SetFrameworkSelection(system, null);
            }
        }

        private void ForgetSelectionWithin(Transform boundary)
        {
            if (previousSelection != null && previousSelection.transform.IsChildOf(boundary))
            {
                previousSelection = null;
            }
        }

        public void SetFocused(bool focused)
        {
            RequireAlive();
            wantsNativeFocus = focused;
            if (!focused && focusRestoration != null)
            {
                CancelFocusRestoration(focusRestoration);
            }
            var host = GetComponentInParent<UIHost>();
            var system = host == null ? EventSystem.current : host.GetInputEventSystem();
            if (!focused && host != null)
            {
                host.ReleaseSharedKeyboardFocus();
            }

            if (system == null || system.alreadySelecting)
            {
                return;
            }

            if (focused && host != null)
            {
                host.ClaimSharedKeyboardFocus();
            }
            var selected = system.currentSelectedGameObject;
            if (!focused)
            {
                if (BelongsToView(selected))
                {
                    RememberFocus(selected);
                    NativeFocusObserver.SetFrameworkSelection(system, null);
                }

                return;
            }

            if (CanReceiveSharedKeyboardInput())
            {
                ConstrainFocusCore(true);
            }
        }

        public void ConstrainFocus() => ConstrainFocusCore(false);

        private void ConstrainFocusCore(bool acquiringFocus)
        {
            RequireAlive();
            if (!acquiringFocus && !wantsNativeFocus)
            {
                // 导航的逐帧维护不能在失焦期间重写已保存的逻辑条目与控件。
                return;
            }

            var host = GetComponentInParent<UIHost>();
            var system = host == null ? EventSystem.current : host.GetInputEventSystem();
            if (system == null || system.alreadySelecting)
            {
                return;
            }

            var selected = system.currentSelectedGameObject;
            if (!CanReceiveSharedKeyboardInput())
            {
                if (BelongsToView(selected))
                {
                    RememberFocus(selected);
                    NativeFocusObserver.SetFrameworkSelection(system, null);
                }

                return;
            }

            if (!IsInputEnabled)
            {
                if (BelongsToView(selected))
                {
                    RememberFocus(selected);
                    NativeFocusObserver.SetFrameworkSelection(system, null);
                }

                return;
            }

            if (CanSelect(selected))
            {
                if (!acquiringFocus || previousVirtualFocus == null)
                {
                    RememberFocus(selected);
                    return;
                }
            }

            if (focusRestoration != null)
            {
                return;
            }

            if (!acquiringFocus && selected != null)
            {
                if (host != null && !selected.transform.IsChildOf(host.transform))
                {
                    return;
                }
            }

            if (acquiringFocus && previousVirtualList != null && previousVirtualList.IsAlive &&
                previousVirtualList.transform.IsChildOf(transform) && previousVirtualFocus != null)
            {
                var cancellation = CancellationTokenSource.CreateLinkedTokenSource(childActivationToken);
                focusRestoration = cancellation;
                _ = RestoreVirtualFocusAsync(previousVirtualList, previousVirtualFocus, system, cancellation);
                return;
            }

            GameObject candidate = CanSelect(previousSelection) ? previousSelection : null;
            if (candidate == null)
            {
                var fallback = FindFocusTarget(null);
                candidate = fallback == null ? null : fallback.gameObject;
            }

            if (selected != candidate)
            {
                NativeFocusObserver.SetFrameworkSelection(system, candidate);
            }
        }

        private void RememberFocus(GameObject selection)
        {
            previousSelection = selection;
            var list = selection == null ? null : selection.GetComponentInParent<VirtualListElement>(true);
            previousVirtualList = list != null && list.IsAlive && list.transform.IsChildOf(transform) ? list : null;
            previousVirtualFocus = previousVirtualList == null ? null : previousVirtualList.CaptureFocus();
        }

        private async Task RestoreVirtualFocusAsync(VirtualListElement list, VirtualListFocusPosition position,
            EventSystem system, CancellationTokenSource cancellation)
        {
            var request = NativeFocusObserver.Capture(system);
            var scope = childViews;
            var fallback = false;
            try
            {
                var result = await list.RestoreFocusAsync(position, cancellation.Token);
                fallback = result.Status == VirtualListRevealStatus.NotFound ||
                    result.Status == VirtualListRevealStatus.NoSelectable ||
                    result.Status == VirtualListRevealStatus.IncompatibleSource ||
                    result.Status == VirtualListRevealStatus.Failed;
                if (result.Status == VirtualListRevealStatus.Failed && result.Error != null)
                {
                    UIErrors.Report(result.Error);
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                // 页面再次失焦或激活结束，旧恢复不得继续发布原生选择。
            }
            catch (Exception error)
            {
                fallback = true;
                UIErrors.Report(error);
            }
            finally
            {
                var current = ReferenceEquals(focusRestoration, cancellation);
                if (current)
                {
                    focusRestoration = null;
                }

                var eligible = current && !cancellation.IsCancellationRequested && IsAlive && wantsNativeFocus &&
                    ReferenceEquals(childViews, scope) && IsInputEnabled && CanReceiveSharedKeyboardInput();
                cancellation.Dispose();
                if (eligible && fallback && request != null && request.IsCurrent(GetInputEventSystem(transform)))
                {
                    previousSelection = null;
                    previousVirtualList = null;
                    previousVirtualFocus = null;
                    try
                    {
                        ConstrainFocusCore(false);
                    }
                    catch (Exception error)
                    {
                        UIErrors.Report(error);
                    }
                }
            }
        }

        private void ResetFocusState()
        {
            previousSelection = null;
            previousVirtualList = null;
            previousVirtualFocus = null;
            wantsNativeFocus = false;
            if (focusRestoration != null)
            {
                var previous = focusRestoration;
                focusRestoration = null;
                CancelFocusRestoration(previous);
            }
        }

        private static void CancelFocusRestoration(CancellationTokenSource cancellation)
        {
            try
            {
                cancellation.Cancel();
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }

        private bool BelongsToView(GameObject candidate) => candidate != null && candidate.transform.IsChildOf(transform);

        internal static EventSystem GetInputEventSystem(Transform boundary)
        {
            var host = boundary.GetComponentInParent<UIHost>();
            return host == null ? EventSystem.current : host.GetInputEventSystem();
        }

        internal bool CanReceiveSharedKeyboardInput()
        {
            var host = GetComponentInParent<UIHost>();
            return host == null || host.CanReceiveSharedKeyboardInput();
        }

        /// <summary>按绑定元素名恢复控件；目标失效时依次使用声明的默认控件和首个可交互控件。</summary>
        internal Selectable FindFocusTarget(string elementName, IReadOnlyList<string> controlPath = null)
        {
            if (!string.IsNullOrEmpty(elementName))
            {
                foreach (var element in GetComponentsInChildren<Element>(true))
                {
                    if (element != null && element.IsAlive && element.GetComponentInParent<View>(true) == this &&
                        element.Name == elementName && CanSelect(element.gameObject))
                    {
                        return element.GetComponent<Selectable>();
                    }
                }
            }

            if (controlPath != null)
            {
                var node = transform;
                foreach (var segment in controlPath)
                {
                    Transform match = null;
                    for (var i = 0; i < node.childCount; ++i)
                    {
                        var child = node.GetChild(i);
                        if (child.name == segment)
                        {
                            if (match != null)
                            {
                                match = null;
                                break;
                            }

                            match = child;
                        }
                    }

                    node = match;
                    if (node == null)
                    {
                        break;
                    }
                }

                if (node != null && CanSelect(node.gameObject))
                {
                    return node.GetComponent<Selectable>();
                }
            }

            if (defaultSelection != null && CanSelect(defaultSelection.gameObject))
            {
                return defaultSelection;
            }

            foreach (var selectable in GetComponentsInChildren<Selectable>())
            {
                if (selectable != null && CanSelect(selectable.gameObject))
                {
                    return selectable;
                }
            }

            return null;
        }

        /// <summary>逻辑控件路径只使用唯一同级名称；不以回收节点引用或可变的兄弟序号标识控件。</summary>
        internal string[] CaptureFocusPath(Transform selected)
        {
            if (selected == null || !selected.IsChildOf(transform))
            {
                return null;
            }

            var path = new List<string>();
            for (var node = selected; node != transform; node = node.parent)
            {
                var parent = node.parent;
                for (var i = 0; i < parent.childCount; ++i)
                {
                    var sibling = parent.GetChild(i);
                    if (sibling != node && sibling.name == node.name)
                    {
                        return null;
                    }
                }

                path.Add(node.name);
            }

            path.Reverse();
            return path.ToArray();
        }

        private bool CanSelect(GameObject candidate)
        {
            if (!BelongsToView(candidate) || !candidate.activeInHierarchy)
            {
                return false;
            }

            var selectable = candidate.GetComponent<Selectable>();
            var owner = candidate.GetComponentInParent<View>(true);
            return selectable != null && selectable.isActiveAndEnabled && selectable.IsInteractable() &&
                owner != null && owner.IsInputEnabled;
        }
    }
}
