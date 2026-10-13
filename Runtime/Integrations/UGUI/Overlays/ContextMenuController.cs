using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>为预制的原生 Button 菜单提供焦点和纵向键盘导航。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(AnchoredOverlayElement))]
    public sealed partial class ContextMenuController : MonoBehaviour
    {
        [SerializeField]
        private bool wrapNavigation = true;
        [SerializeField, Min(1)]
        private int maxItems = 128;
        private readonly List<Button> items = new List<Button>();
        private readonly List<Button> available = new List<Button>();
        private readonly Dictionary<Button, UnityEngine.UI.Navigation> authoredNavigation = new Dictionary<Button, UnityEngine.UI.Navigation>();
        private AnchoredOverlayElement overlay;
        private RectTransform content;
        private View view;
        private GameObject restoreSelection;
        private bool restorePending;
        private bool restoringFocus;
        private bool session;
        private bool synchronizing;

        public bool CanHandleBack => isActiveAndEnabled && overlay != null && overlay.CanHandleBack;

        private void OnEnable()
        {
            if (maxItems < 1)
            {
                throw new InvalidOperationException("Context menu capacity must be positive.");
            }

            overlay = GetComponent<AnchoredOverlayElement>();
            overlay.Initialize();
            content = overlay.ContentRoot;
            view = GetComponentInParent<View>(true);
            overlay.PropertyChanged += OnOverlayChanged;
            if (view != null)
            {
                view.InputStateChanged += OnInputChanged;
            }

            Synchronize();
        }

        private void OnDisable()
        {
            if (overlay != null)
            {
                overlay.PropertyChanged -= OnOverlayChanged;
            }

            if (view != null)
            {
                view.InputStateChanged -= OnInputChanged;
            }

            CloseMenuSafely(null);
        }

        private void LateUpdate()
        {
            try
            {
                Synchronize();
            }
            catch (Exception error)
            {
                CloseMenuSafely(error);
            }
            if (session && itemsDirty)
            {
                try
                {
                    RefreshItems();
                }
                catch (Exception error)
                {
                    UIErrors.Report(error);
                }
            }

            if (session)
            {
                synchronizing = true;
                try
                {
                    RefreshNavigationAndFocus();
                }
                catch (Exception error)
                {
                    CloseMenuSafely(error);
                }
                finally
                {
                    synchronizing = false;
                }
            }
            else
            {
                TryRestoreFocus();
            }
        }

        public void Show(RectTransform anchor)
        {
            if (!isActiveAndEnabled || overlay == null || !overlay.IsAlive)
            {
                throw new InvalidOperationException("Context menu controller is not active.");
            }

            if (anchor == null)
            {
                throw new ArgumentNullException(nameof(anchor));
            }

            overlay.Anchor = anchor;
            Synchronize();
        }

        public void Hide()
        {
            if (overlay != null && overlay.IsAlive)
            {
                overlay.Anchor = null;
            }
        }

        public bool HandleBack() => CanHandleBack && overlay.HandleBack();

        public bool HandleOutsidePointer(Vector2 screenPosition, Camera eventCamera) => isActiveAndEnabled && overlay != null && overlay.IsAlive && overlay.HandleOutsidePointer(screenPosition, eventCamera);

        private void OnOverlayChanged(object sender, PropertyChangedEventArgs args)
        {
            if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == nameof(AnchoredOverlayElement.Anchor))
            {
                Synchronize();
            }
        }

        private void OnInputChanged() => Synchronize();

        private bool CanRun() => isActiveAndEnabled && overlay != null && overlay.IsAlive && overlay.isActiveAndEnabled && overlay.Anchor != null && content != null && content.gameObject.activeInHierarchy && (view == null || view.IsInputEnabled);

        private void Synchronize()
        {
            if (synchronizing)
            {
                return;
            }

            synchronizing = true;
            try
            {
                if (!CanRun())
                {
                    if (session)
                    {
                        CloseMenuSafely(null);
                    }
                    else
                    {
                        TryRestoreFocus();
                    }
                    return;
                }

                if (session)
                {
                    return;
                }

                try
                {
                    RebuildItems();
                }
                catch
                {
                    items.Clear();
                    overlay.Anchor = null;
                    throw;
                }

                var system = EventSystem.current;
                // 不要将隐藏菜单项记录为焦点返回位置。
                var selected = system == null ? null : system.currentSelectedGameObject;
                restoreSelection = IsInside(selected) ? overlay.Anchor.gameObject : selected;
                restorePending = false;
                session = true;
                try
                {
                    BeginBackSession();
                    RefreshNavigationAndFocus();
                }
                catch
                {
                    CloseMenuSafely(null);
                    throw;
                }
            }
            finally
            {
                synchronizing = false;
            }
        }

        private void RefreshNavigationAndFocus()
        {
            if (!EnsureSessionActive())
            {
                return;
            }

            available.Clear();
            for (var i = items.Count - 1; i >= 0; --i)
            {
                var item = items[i];
                if (item != null && item.transform.IsChildOf(content))
                {
                    continue;
                }

                if (item != null && authoredNavigation.TryGetValue(item, out var original))
                {
                    item.navigation = original;
                }

                // 已销毁的 Unity 对象仍保留托管身份，可用于从字典移除。
                if (!ReferenceEquals(item, null))
                {
                    authoredNavigation.Remove(item);
                }

                items.RemoveAt(i);
            }

            for (var i = 0; i < items.Count; ++i)
            {
                var item = items[i];
                if (item == null)
                {
                    itemsDirty = true;
                    return;
                }

                var selectable = CanSelect(item.gameObject);
                // 可覆写的 IsInteractable 可能关闭菜单或请求重建按钮层级。
                if (!EnsureSessionActive() || itemsDirty)
                {
                    return;
                }

                if (item == null || i >= items.Count || !ReferenceEquals(items[i], item))
                {
                    itemsDirty = true;
                    return;
                }

                if (selectable)
                {
                    available.Add(item);
                }
            }

            if (available.Count == 0)
            {
                CloseMenuSafely(null);
                return;
            }

            for (var i = 0; i < available.Count; ++i)
            {
                var navigation = new UnityEngine.UI.Navigation
                {
                    mode = UnityEngine.UI.Navigation.Mode.Explicit
                };
                navigation.selectOnUp = i > 0 ? available[i - 1] : wrapNavigation ? available[available.Count - 1] : available[i];
                navigation.selectOnDown = i + 1 < available.Count ? available[i + 1] : wrapNavigation ? available[0] : available[i];
                // 此控制器负责一层菜单，横向输入不能逃逸到页面。
                navigation.selectOnLeft = available[i];
                navigation.selectOnRight = available[i];
                available[i].navigation = navigation;
                if (!EnsureSessionActive() || itemsDirty)
                {
                    return;
                }
            }

            var system = EventSystem.current;
            if (system == null || system.alreadySelecting)
            {
                return;
            }

            var selected = system.currentSelectedGameObject;
            foreach (var item in available)
            {
                if (item.gameObject == selected)
                {
                    return;
                }
            }

            system.SetSelectedGameObject(available.Count == 0 ? null : available[0].gameObject);
        }

        private bool EnsureSessionActive()
        {
            if (!session)
            {
                return false;
            }

            if (CanRun())
            {
                return true;
            }

            CloseMenuSafely(null);
            return false;
        }

        private void CloseMenuSafely(Exception error)
        {
            UIErrors.Report(error);
            try
            {
                EndSession();
            }
            catch (Exception cleanup)
            {
                UIErrors.Report(cleanup);
            }

            try
            {
                Hide();
            }
            catch (Exception cleanup)
            {
                UIErrors.Report(cleanup);
            }

            try
            {
                TryRestoreFocus();
            }
            catch (Exception cleanup)
            {
                UIErrors.Report(cleanup);
            }
        }

        private void EndSession()
        {
            var wasActive = session;
            session = false;
            Exception failure = null;
            try
            {
                EndBackSession();
            }
            catch (Exception error)
            {
                failure = error;
            }

            if (!wasActive)
            {
                if (failure != null)
                {
                    ExceptionDispatchInfo.Capture(failure).Throw();
                }

                return;
            }

            foreach (var pair in authoredNavigation)
            {
                if (pair.Key != null)
                {
                    try
                    {
                        pair.Key.navigation = pair.Value;
                    }
                    catch (Exception error)
                    {
                        failure = failure == null ? error : new AggregateException(failure, error);
                    }
                }
            }

            authoredNavigation.Clear();
            items.Clear();
            available.Clear();
            candidates.Clear();
            itemsDirty = false;
            restorePending = true;
            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }

        private void TryRestoreFocus()
        {
            if (!restorePending || restoringFocus)
            {
                return;
            }

            var system = EventSystem.current;
            if (system == null)
            {
                restorePending = false;
                restoreSelection = null;
                return;
            }

            if (system.alreadySelecting)
            {
                return;
            }

            var selected = system.currentSelectedGameObject;
            // 新页面、对话框或用户选择已取得焦点时，不应强行抢回。
            if (selected != null && !IsInside(selected))
            {
                restorePending = false;
                restoreSelection = null;
                return;
            }

            var candidate = restoreSelection;
            var previousAnchor = overlay != null && overlay.IsAlive ? overlay.Anchor : null;
            restoringFocus = true;
            try
            {
                bool canRestore;
                try
                {
                    canRestore = CanSelect(candidate);
                }
                catch (Exception error)
                {
                    UIErrors.Report(error);
                    canRestore = false;
                }

                // 项目 Selectable 回调可能打开新菜单或把焦点交给其他界面。
                var currentAnchor = overlay != null && overlay.IsAlive ? overlay.Anchor : null;
                if (!restorePending || !ReferenceEquals(candidate, restoreSelection) || session)
                {
                    return;
                }

                if (!ReferenceEquals(previousAnchor, currentAnchor) || !ReferenceEquals(system, EventSystem.current))
                {
                    restorePending = false;
                    restoreSelection = null;
                    return;
                }

                selected = system.currentSelectedGameObject;
                if (selected != null && !IsInside(selected))
                {
                    restorePending = false;
                    restoreSelection = null;
                    return;
                }

                var destination = canRestore && candidate != null && candidate.activeInHierarchy ? candidate : null;
                restorePending = false;
                restoreSelection = null;
                if (selected != destination)
                {
                    system.SetSelectedGameObject(destination);
                }
            }
            finally
            {
                restoringFocus = false;
            }
        }

        private bool IsInside(GameObject target) => target != null && content != null && target.transform.IsChildOf(content);

        private static bool CanSelect(GameObject target)
        {
            if (target == null || !target.activeInHierarchy)
            {
                return false;
            }

            var selectable = target.GetComponent<Selectable>();
            if (selectable == null || !selectable.isActiveAndEnabled || !selectable.IsInteractable())
            {
                return false;
            }

            var owner = target.GetComponentInParent<View>(true);
            return owner == null || owner.IsInputEnabled;
        }
    }
}
