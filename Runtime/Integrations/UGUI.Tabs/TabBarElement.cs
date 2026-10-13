using System;
using System.ComponentModel;
using System.Threading.Tasks;
using MUI.Tabs;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MUI.UGUI
{
    [DisallowMultipleComponent]
    public sealed partial class TabBarElement : Element, IElementBoundary
    {
        [SerializeField]
        private Entry[] entries = Array.Empty<Entry>();
        private TabViewModel model;
        private TabContentController controller;
        private int renderVersion;
        private bool rendering;
        private bool renderPending;

        public event Action<string> SelectionRequested;

        public TabViewModel ViewModel
        {
            get => model;
            set
            {
                RequireAlive();
                // 直接换模型不能保留旧控制器，否则显示状态与点击请求会指向不同页面。
                if (controller != null && !ReferenceEquals(controller.ViewModel, value))
                {
                    controller = null;
                }

                if (ReferenceEquals(model, value))
                {
                    return;
                }

                if (model != null)
                {
                    model.PropertyChanged -= OnStateChanged;
                }

                model = value;
                if (model != null)
                {
                    model.PropertyChanged += OnStateChanged;
                }

                Render();
            }
        }

        protected override AccessibilityRole DefaultAccessibilityRole => AccessibilityRole.TabList;

        public void Bind(TabContentController value)
        {
            RequireAlive();
            controller = value;
            ViewModel = value == null ? null : value.ViewModel;
        }

        protected override void OnInitialize()
        {
            if (entries.Length == 0)
            {
                var discovered = new System.Collections.Generic.List<Entry>();
                foreach (Transform child in transform)
                {
                    var button = child.GetComponent<Button>();
                    if (button == null || button == buttonTemplate)
                    {
                        continue;
                    }

                    var mark = child.Find("Selected");
                    discovered.Add(new Entry { Key = child.name, Button = button, SelectionMark = mark == null ? null : mark.gameObject });
                }

                entries = discovered.ToArray();
            }

            if (buttonTemplate != null)
            {
                ValidateTemplate(buttonTemplate);
            }

            var keys = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            var buttons = new System.Collections.Generic.HashSet<Button>();
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || entry.Button == null || !keys.Add(entry.Key))
                {
                    throw new InvalidOperationException("TabBar entries require unique keys and Buttons.");
                }

                if (!buttons.Add(entry.Button))
                {
                    throw new InvalidOperationException("同一个 Button 不能绑定多个 Tab 条目。");
                }

                if (entry.Button == buttonTemplate)
                {
                    throw new InvalidOperationException("A Tab button template cannot also be an authored entry.");
                }

                if (!entry.Button.transform.IsChildOf(transform) || entry.Button.transform == transform)
                {
                    throw new InvalidOperationException("TabBar Buttons must belong to the TabBar region.");
                }

                if (entry.SelectionMark != null && (entry.SelectionMark == entry.Button.gameObject || !entry.SelectionMark.transform.IsChildOf(entry.Button.transform)))
                {
                    throw new InvalidOperationException("Selection mark must be a child of its Button, not the Button itself.");
                }
            }

            // 完整校验通过后再挂接监听，避免无效配置留下部分可响应的条目。
            foreach (var entry in entries)
            {
                var captured = entry;
                UnityAction handler = () => RequestSelection(captured);
                entry.Button.onClick.AddListener(handler);
                OnDispose(() =>
                {
                    if (captured.Button != null)
                    {
                        captured.Button.onClick.RemoveListener(handler);
                    }
                });
            }

            OnDispose(() =>
            {
                ReleaseGenerated();
                if (model != null)
                {
                    model.PropertyChanged -= OnStateChanged;
                }

                model = null;
                controller = null;
                SelectionRequested = null;
                renderPending = false;
            });
            ready = true;
            Render();
        }

        private void OnStateChanged(object sender, PropertyChangedEventArgs args) => Render();

        protected override void OnVisualRetentionEnded()
        {
            Render();
        }

        private void Render()
        {
            var version = unchecked(++renderVersion);
            if (rendering)
            {
                // 层级回调可能再次请求渲染。只标记待刷新，避免在 Instantiate 中递归创建按钮。
                renderPending = true;
                return;
            }

            renderPending = false;
            rendering = true;
            try
            {
                RenderCurrent(version);
            }
            finally
            {
                rendering = false;
            }
        }

        private void LateUpdate()
        {
            if (renderPending && IsAlive && ready && !IsVisualRetentionActive)
            {
                Render();
            }
        }

        private void RenderCurrent(int version)
        {
            if (!IsAlive || IsVisualRetentionActive)
            {
                return;
            }

            if (!ready)
            {
                return;
            }

            var events = EventSystem.current;
            var focused = events == null ? null : events.currentSelectedGameObject;
            var ownedFocus = false;
            if (focused != null)
            {
                foreach (var entry in entries)
                {
                    if (entry != null && entry.Button != null && (focused == entry.Button.gameObject || focused.transform.IsChildOf(entry.Button.transform)))
                    {
                        ownedFocus = true;
                    }
                }
            }

            SynchronizeButtons(version);
            if (!IsCurrentRender(version))
            {
                return;
            }

            ArrangeButtons(version);
            if (!IsCurrentRender(version))
            {
                return;
            }

            foreach (var entry in entries)
            {
                if (entry == null || entry.Button == null)
                {
                    continue;
                }

                var enabled = false;
                TabItemState matched = null;
                if (model != null)
                {
                    foreach (var item in model.Items)
                    {
                        if (item.Key == entry.Key)
                        {
                            matched = item;
                            enabled = item.Enabled && model.Snapshot.Phase != TabPhase.Inactive;
                            break;
                        }
                    }
                }

                entry.Button.gameObject.SetActive(matched != null);
                if (!IsCurrentRender(version))
                {
                    return;
                }

                if (entry.Button == null)
                {
                    continue;
                }

                if (matched != null)
                {
                    var label = entry.Button.transform.Find("Label");
                    if (label != null)
                    {
                        var text = label.GetComponent<Text>();
                        if (text != null)
                        {
                            text.text = matched.Label;
                            if (!IsCurrentRender(version))
                            {
                                return;
                            }
                        }
                    }
                }

                if (entry.Button == null)
                {
                    continue;
                }

                entry.Button.interactable = enabled;
                if (!IsCurrentRender(version))
                {
                    return;
                }

                if (entry.SelectionMark != null)
                {
                    entry.SelectionMark.SetActive(model != null && model.Snapshot.SelectedTab == entry.Key);
                    if (!IsCurrentRender(version))
                    {
                        return;
                    }
                }
            }

            // 显式水平导航将键盘和手柄焦点保留在 TabBar 中，
            // 异步内容完成不会自动选中内容区控件。
            for (var i = 0; i < entries.Length; ++i)
            {
                var entry = entries[i];
                if (entry == null || entry.Button == null)
                {
                    continue;
                }

                var navigation = entry.Button.navigation;
                navigation.mode = UnityEngine.UI.Navigation.Mode.Explicit;
                navigation.selectOnLeft = FindEnabled(i, -1);
                navigation.selectOnRight = FindEnabled(i, 1);
                navigation.selectOnUp = null;
                navigation.selectOnDown = null;
                entry.Button.navigation = navigation;
            }

            if (ownedFocus && focused != null && events != null && (events.currentSelectedGameObject == null || events.currentSelectedGameObject == focused))
            {
                var selectable = focused.GetComponentInParent<Selectable>();
                if (!focused.activeInHierarchy || selectable == null || !selectable.IsInteractable())
                {
                    Button fallback = null;
                    foreach (var entry in entries)
                    {
                        if (entry == null || entry.Button == null || !entry.Button.gameObject.activeInHierarchy || !entry.Button.IsInteractable())
                        {
                            continue;
                        }

                        if (fallback == null || (model != null && model.Snapshot.SelectedTab == entry.Key))
                        {
                            fallback = entry.Button;
                        }
                    }

                    events.SetSelectedGameObject(fallback == null ? null : fallback.gameObject);
                }
            }
        }

        // 启停、文字和布局回调可能换绑或释放当前控件，旧渲染不得继续覆盖新状态。
        private bool IsCurrentRender(int version)
        {
            return IsAlive && !IsVisualRetentionActive && version == renderVersion;
        }

        /// <summary>统一处理预制与动态按钮输入，尊重控件状态及父界面的有效输入门控。</summary>
        private void RequestSelection(Entry entry)
        {
            // 原生事件可能被直接调用，或在其他监听器改变门控后到达此处，不能只依赖射线阻挡。
            if (entry == null || !CanReceiveInput(entry.Button))
            {
                return;
            }

            if (controller != null)
            {
                _ = ObserveAsync(controller.SelectAsync(entry.Key));
            }
            else
            {
                SelectionRequested?.Invoke(entry.Key);
            }
        }

        private Button FindEnabled(int index, int direction)
        {
            for (var step = 1; step < entries.Length; ++step)
            {
                var entry = entries[(index + direction * step + entries.Length) % entries.Length];
                if (entry != null && entry.Button != null && entry.Button.gameObject.activeInHierarchy && entry.Button.IsInteractable())
                {
                    return entry.Button;
                }
            }

            return null;
        }

        private static async Task ObserveAsync(ValueTask<TabSelectionResult> selection)
        {
            try
            {
                await selection;
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }

        [Serializable]
        public sealed class Entry
        {
            public string Key;
            public Button Button;
            public GameObject SelectionMark;
        }
    }
}
