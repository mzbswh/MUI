using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MUI.UGUI
{
    public sealed partial class UIHost
    {
        private static readonly Dictionary<EventSystem, SharedInputGroup> sharedInputGroups =
            new Dictionary<EventSystem, SharedInputGroup>();
        private SharedInputGroup sharedInputGroup;
        private Canvas sharedCanvas;
        private GraphicRaycaster sharedRaycaster;
        private CanvasGroup sharedCanvasGroup;
        private bool ownsSharedCanvasGroup;
        private int sharedInputPriority;
        private bool sharedRaycasterSuppressed;
        private bool sharedRaycasterWasEnabled;

        /// <summary>
        /// 显式接入共享 EventSystem。宿主、View 根和独立根 Canvas 必须位于同一对象，
        /// 各宿主使用同一排序层及相机，priority 在同一 EventSystem 内唯一。
        /// </summary>
        public void RegisterSharedInput(EventSystem eventSystem, Canvas canvas, int priority)
        {
            UnityMainThread.Require();
            if (sharedInputGroup != null)
            {
                throw new InvalidOperationException("UIHost 已登记共享输入。");
            }

            if (navigator == null || navigator.IsShutdown || shutdown != null || destroyed)
            {
                throw new InvalidOperationException("共享输入须在 UIHost 初始化后、退出前登记。");
            }

            if (eventSystem == null || !eventSystem.isActiveAndEnabled)
            {
                throw new ArgumentException("共享输入需要启用的 EventSystem。", nameof(eventSystem));
            }

            if (canvas == null || !canvas.isActiveAndEnabled || !canvas.isRootCanvas ||
                canvas.transform != transform || (viewRoot != null && viewRoot != canvas.transform) ||
                canvas.renderMode == RenderMode.WorldSpace)
            {
                throw new ArgumentException("共享输入需要宿主与 View 根所在的独立屏幕根 Canvas。", nameof(canvas));
            }

            if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
            {
                throw new ArgumentException("Camera Canvas 需要明确的渲染与事件相机。", nameof(canvas));
            }

            if (priority < short.MinValue || priority > short.MaxValue || canvas.sortingOrder != priority)
            {
                throw new ArgumentOutOfRangeException(nameof(priority), "输入优先级须与 Canvas.sortingOrder 一致且在有效排序范围内。");
            }

            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null || !raycaster.isActiveAndEnabled)
            {
                throw new ArgumentException("共享输入 Canvas 需要启用的 GraphicRaycaster。", nameof(canvas));
            }

            var rootGroup = canvas.GetComponent<CanvasGroup>();
            if (rootGroup != null && (!rootGroup.isActiveAndEnabled || !rootGroup.interactable ||
                !rootGroup.blocksRaycasts || rootGroup.ignoreParentGroups))
            {
                throw new ArgumentException("共享输入 Canvas 根的 CanvasGroup 必须允许交互与射线，且不能忽略父组。", nameof(canvas));
            }

            if (!sharedInputGroups.TryGetValue(eventSystem, out var group))
            {
                group = new SharedInputGroup(eventSystem);
                sharedInputGroups.Add(eventSystem, group);
            }

            try
            {
                group.Add(this, canvas, priority);
            }
            catch
            {
                if (group.Count == 0)
                {
                    sharedInputGroups.Remove(eventSystem);
                }

                throw;
            }
            var createdGroup = rootGroup == null;
            try
            {
                if (createdGroup)
                {
                    rootGroup = canvas.gameObject.AddComponent<CanvasGroup>();
                }
            }
            catch
            {
                group.Remove(this);
                if (group.Count == 0)
                {
                    sharedInputGroups.Remove(eventSystem);
                }

                throw;
            }

            sharedInputGroup = group;
            sharedCanvas = canvas;
            sharedRaycaster = raycaster;
            sharedCanvasGroup = rootGroup;
            ownsSharedCanvasGroup = createdGroup;
            sharedInputPriority = priority;
            group.Refresh();
        }

        /// <summary>同时提高 Canvas 显示顺序与共享输入优先级。</summary>
        public void BringToFront()
        {
            UnityMainThread.Require();
            if (sharedInputGroup == null)
            {
                throw new InvalidOperationException("UIHost 未登记共享输入。");
            }

            sharedInputGroup.BringToFront(this);
        }

        /// <summary>撤销共享输入登记，并恢复框架临时关闭的射线组件。</summary>
        public void UnregisterSharedInput()
        {
            UnityMainThread.Require();
            var group = sharedInputGroup;
            if (group == null)
            {
                return;
            }

            sharedInputGroup = null;
            group.Remove(this);
            RestoreSharedRaycaster();
            if (ownsSharedCanvasGroup && sharedCanvasGroup != null)
            {
                Destroy(sharedCanvasGroup);
            }

            sharedCanvas = null;
            sharedRaycaster = null;
            sharedCanvasGroup = null;
            ownsSharedCanvasGroup = false;
            if (group.Count == 0)
            {
                sharedInputGroups.Remove(group.EventSystem);
            }
            else
            {
                group.Refresh();
            }
        }

        internal bool CanReceiveSharedPointerInput()
        {
            var group = sharedInputGroup;
            return group == null || group.CanReceivePointer(this);
        }

        internal bool CanReceiveSharedKeyboardInput()
        {
            var group = sharedInputGroup;
            return group == null || group.KeyboardOwner == this;
        }

        internal EventSystem GetInputEventSystem() =>
            sharedInputGroup == null ? EventSystem.current : sharedInputGroup.EventSystem;

        internal void ClaimSharedKeyboardFocus()
        {
            if (sharedInputGroup != null)
            {
                sharedInputGroup.ClaimFocus(this);
            }
        }

        internal void ReleaseSharedKeyboardFocus()
        {
            if (sharedInputGroup != null)
            {
                sharedInputGroup.ReleaseFocus(this);
            }
        }

        internal bool ShouldConsumeSharedCancel(EventSystem eventSystem)
        {
            var group = sharedInputGroup;
            return group != null && group.EventSystem == eventSystem && group.ShouldConsumeCancel();
        }

        private UIHost ResolveBackRecipient(EventSystem eventSystem)
        {
            if (eventSystem == null || !sharedInputGroups.TryGetValue(eventSystem, out var group))
            {
                return sharedInputGroup == null ? this : null;
            }

            return sharedInputGroup == group ? group.KeyboardOwner : null;
        }

        private bool HasSharedInputContent()
        {
            if (navigator == null || navigator.IsShutdown || shutdown != null || !isActiveAndEnabled)
            {
                return false;
            }

            foreach (var view in GetComponentsInChildren<View>(true))
            {
                if (view != null && view.IsInputEnabled)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasSharedModalBarrier()
        {
            foreach (var barrier in GetComponentsInChildren<ModalPointerBarrier>(true))
            {
                if (barrier == null || !barrier.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var image = barrier.GetComponent<Image>();
                if (image != null && image.raycastTarget)
                {
                    return true;
                }
            }

            return false;
        }

        private View FindSharedFocusView()
        {
            foreach (var view in GetComponentsInChildren<View>(true))
            {
                if (view != null && view.WantsNativeFocus && view.IsInputEnabled)
                {
                    return view;
                }
            }

            return null;
        }

        private void SuppressSharedRaycaster(bool suppress)
        {
            if (sharedRaycaster == null)
            {
                return;
            }

            if (suppress)
            {
                if (!sharedRaycasterSuppressed)
                {
                    foreach (var view in GetComponentsInChildren<View>(true))
                    {
                        if (view != null)
                        {
                            view.InvalidateInputGestures();
                        }
                    }
                    sharedRaycasterWasEnabled = sharedRaycaster.enabled;
                    sharedRaycaster.enabled = false;
                    sharedRaycasterSuppressed = true;
                }

                if (sharedCanvasGroup != null)
                {
                    sharedCanvasGroup.interactable = false;
                    sharedCanvasGroup.blocksRaycasts = false;
                }
            }
            else
            {
                RestoreSharedRaycaster();
            }
        }

        private void RestoreSharedRaycaster()
        {
            if (sharedRaycasterSuppressed && sharedRaycaster != null)
            {
                sharedRaycaster.enabled = sharedRaycasterWasEnabled;
            }

            if (sharedCanvasGroup != null && sharedRaycasterSuppressed)
            {
                sharedCanvasGroup.interactable = true;
                sharedCanvasGroup.blocksRaycasts = true;
            }

            sharedRaycasterSuppressed = false;
        }

        private void LateUpdate()
        {
            if (sharedInputGroup != null)
            {
                sharedInputGroup.Refresh();
            }
        }

        private void OnEnable()
        {
            if (sharedInputGroup != null)
            {
                sharedInputGroup.Refresh();
            }
        }

        private void OnDisable()
        {
            if (sharedInputGroup != null)
            {
                sharedInputGroup.Refresh();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedInput() => sharedInputGroups.Clear();

        private sealed class SharedInputGroup
        {
            private readonly List<UIHost> hosts = new List<UIHost>();
            private int sortingLayer;
            private RenderMode renderMode;
            private Camera renderCamera;
            private bool reportedConfigurationError;
            private UIHost lastKeyboardOwner;
            private UIHost requestedKeyboardOwner;

            internal SharedInputGroup(EventSystem eventSystem) => EventSystem = eventSystem;

            internal EventSystem EventSystem
            {
                get;
            }

            internal int Count => hosts.Count;

            internal UIHost KeyboardOwner
            {
                get
                {
                    if (!Validate())
                    {
                        return null;
                    }

                    var modal = ModalOwner();
                    if (modal != null)
                    {
                        return modal;
                    }

                    var selected = EventSystem.currentSelectedGameObject;
                    if (selected != null)
                    {
                        foreach (var host in hosts)
                        {
                            if (host.HasSharedInputContent() && selected.transform.IsChildOf(host.transform))
                            {
                                var view = selected.GetComponentInParent<View>();
                                if (view != null && view.IsInputEnabled)
                                {
                                    return host;
                                }
                            }
                        }
                    }

                    UIHost focused = null;
                    UIHost fallback = null;
                    foreach (var host in hosts)
                    {
                        if (!host.HasSharedInputContent())
                        {
                            continue;
                        }

                        if (fallback == null || host.sharedInputPriority > fallback.sharedInputPriority)
                        {
                            fallback = host;
                        }

                        if (host.navigator.FocusedHandle.IsValid &&
                            (focused == null || host.sharedInputPriority > focused.sharedInputPriority))
                        {
                            focused = host;
                        }
                    }

                    if (requestedKeyboardOwner != null && requestedKeyboardOwner.HasSharedInputContent())
                    {
                        return requestedKeyboardOwner;
                    }

                    return focused ?? fallback;
                }
            }

            internal void Add(UIHost host, Canvas canvas, int priority)
            {
                if (hosts.Count == 0)
                {
                    sortingLayer = canvas.sortingLayerID;
                    renderMode = canvas.renderMode;
                    renderCamera = canvas.worldCamera;
                }
                else if (canvas.sortingLayerID != sortingLayer || canvas.renderMode != renderMode ||
                    (renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != renderCamera))
                {
                    throw new InvalidOperationException("共享 EventSystem 的 Canvas 排序层、渲染模式和相机必须一致。");
                }

                foreach (var other in hosts)
                {
                    if (other.sharedCanvas == canvas || other.sharedInputPriority == priority)
                    {
                        throw new InvalidOperationException("共享 EventSystem 的 Canvas 和输入优先级必须各自唯一。");
                    }
                }

                if (!HasUnambiguousChildren(canvas))
                {
                    throw new InvalidOperationException("共享输入 Canvas 下不能有覆盖根排序或跳过根交互门控的组件。");
                }

                hosts.Add(host);
            }

            internal void Remove(UIHost host)
            {
                hosts.Remove(host);
                if (lastKeyboardOwner == host)
                {
                    lastKeyboardOwner = null;
                }

                ReleaseFocus(host);
            }

            internal void ClaimFocus(UIHost host)
            {
                if (!Validate())
                {
                    return;
                }

                var modal = ModalOwner();
                if (modal != null && modal != host)
                {
                    return;
                }

                requestedKeyboardOwner = host;
                if (EventSystem == null || EventSystem.alreadySelecting)
                {
                    return;
                }

                var selected = EventSystem.currentSelectedGameObject;
                if (selected != null && !selected.transform.IsChildOf(host.transform))
                {
                    EventSystem.SetSelectedGameObject(null);
                }
            }

            internal void ReleaseFocus(UIHost host)
            {
                if (requestedKeyboardOwner == host)
                {
                    requestedKeyboardOwner = null;
                }
            }

            internal void BringToFront(UIHost host)
            {
                if (!Validate())
                {
                    throw new InvalidOperationException("Canvas 排序已被外部修改，无法置前宿主。");
                }

                var highest = host.sharedInputPriority;
                foreach (var other in hosts)
                {
                    highest = Math.Max(highest, other.sharedInputPriority);
                }

                if (highest == short.MaxValue)
                {
                    throw new InvalidOperationException("Canvas 排序已达到上限，无法置前宿主。");
                }

                host.sharedInputPriority = highest + 1;
                host.sharedCanvas.sortingOrder = host.sharedInputPriority;
                Refresh();
            }

            internal bool CanReceivePointer(UIHost host)
            {
                if (!Validate())
                {
                    return false;
                }

                var modal = ModalOwner();
                return host.isActiveAndEnabled && (modal == null || modal == host);
            }

            internal bool ShouldConsumeCancel() => !Validate() || ModalOwner() != null;

            internal void Refresh()
            {
                var valid = Validate();
                var modal = valid ? ModalOwner() : null;
                foreach (var host in hosts)
                {
                    host.SuppressSharedRaycaster(!valid || !host.isActiveAndEnabled ||
                        host.navigator == null || host.navigator.IsShutdown || host.shutdown != null ||
                        (modal != null && modal != host));
                }

                if (EventSystem == null || EventSystem.alreadySelecting)
                {
                    return;
                }

                var owner = valid ? KeyboardOwner : null;
                var selected = EventSystem.currentSelectedGameObject;
                // 没有页面取得输入资格时，项目的原生入口（例如 Basic 的重开按钮）仍可持有选择。
                var projectSelection = valid && modal == null && owner == null && selected != null &&
                    selected.GetComponentInParent<View>(true) == null;
                if (selected != null && !projectSelection && (owner == null || !selected.transform.IsChildOf(owner.transform)))
                {
                    EventSystem.SetSelectedGameObject(null);
                    selected = null;
                }

                if (owner == null)
                {
                    lastKeyboardOwner = null;
                    return;
                }

                if (owner != lastKeyboardOwner || selected == null)
                {
                    lastKeyboardOwner = owner;
                    var view = owner.FindSharedFocusView();
                    if (view != null)
                    {
                        view.SetFocused(true);
                    }
                }
            }

            private UIHost ModalOwner()
            {
                UIHost modal = null;
                foreach (var host in hosts)
                {
                    if (!host.isActiveAndEnabled || host.navigator == null || host.navigator.IsShutdown ||
                        host.shutdown != null || !host.HasSharedModalBarrier())
                    {
                        continue;
                    }

                    if (modal == null || host.sharedInputPriority > modal.sharedInputPriority)
                    {
                        modal = host;
                    }
                }

                return modal;
            }

            private bool Validate()
            {
                var valid = EventSystem != null && EventSystem.isActiveAndEnabled;
                foreach (var host in hosts)
                {
                    var canvas = host.sharedCanvas;
                    valid &= canvas != null && canvas.isRootCanvas && canvas.sortingLayerID == sortingLayer &&
                        canvas.sortingOrder == host.sharedInputPriority && canvas.renderMode == renderMode &&
                        (renderMode != RenderMode.ScreenSpaceCamera || canvas.worldCamera == renderCamera) &&
                        host.sharedCanvasGroup != null &&
                        (!host.gameObject.activeInHierarchy || host.sharedCanvasGroup.isActiveAndEnabled) &&
                        host.sharedRaycaster != null && HasUnambiguousChildren(canvas);
                }

                if (!valid && !reportedConfigurationError)
                {
                    reportedConfigurationError = true;
                    UIErrors.Report(new InvalidOperationException("共享 EventSystem 的 Canvas 排序或输入配置已失效。"));
                }
                else if (valid)
                {
                    reportedConfigurationError = false;
                }

                return valid;
            }

            private static bool HasUnambiguousChildren(Canvas root)
            {
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (canvas != root && canvas.overrideSorting)
                    {
                        return false;
                    }
                }

                foreach (var group in root.GetComponentsInChildren<CanvasGroup>(true))
                {
                    if (group != root.GetComponent<CanvasGroup>() && group.ignoreParentGroups)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
