using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MUI.UGUI
{
    public sealed partial class TabBarElement
    {
        [SerializeField]
        private Button buttonTemplate;
        private readonly Dictionary<string, GeneratedEntry> generated = new Dictionary<string, GeneratedEntry>(StringComparer.Ordinal);
        private bool ready;
        private System.Collections.Generic.IReadOnlyList<MUI.Tabs.TabItemState> synchronizedItems;

        /// <summary>
        /// 为缺少预制条目的键提供可选的非激活 Button 模板。
        /// TabBar 的布局组负责几何布局；模板包含 Label/Selected 子项，
        /// 不能带持久化点击动作或嵌套 MUI 投影。
        /// </summary>
        public void ConfigureButtonTemplate(Button template)
        {
            RequireAlive();
            ValidateTemplate(template);
            if (ready)
            {
                foreach (var entry in entries)
                {
                    if (entry != null && entry.Button == template)
                    {
                        throw new InvalidOperationException("An existing Tab entry cannot serve as a button template.");
                    }
                }
            }

            if (generated.Count != 0 && template != buttonTemplate)
            {
                throw new InvalidOperationException("Cannot change the button template while generated entries are in use.");
            }

            buttonTemplate = template;
            synchronizedItems = null;
            if (ready)
            {
                Render();
            }
        }

        private static void ValidateTemplate(Button template)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (template.gameObject.activeSelf)
            {
                throw new InvalidOperationException("Tab button template must be inactive.");
            }

            if (template.onClick.GetPersistentEventCount() != 0)
            {
                throw new InvalidOperationException("Tab button template must not contain persistent click actions.");
            }

            if (template.GetComponentsInChildren<View>(true).Length != 0 || template.GetComponentsInChildren<Element>(true).Length != 0)
            {
                throw new InvalidOperationException("Tab button template cannot contain MUI View or Element components.");
            }
        }

        private void SynchronizeButtons(int version)
        {
            var items = model == null ? null : model.Items;
            if (items != null && ReferenceEquals(items, synchronizedItems))
            {
                return;
            }

            var wanted = new HashSet<string>(StringComparer.Ordinal);
            if (model != null)
            {
                foreach (var item in model.Items)
                {
                    wanted.Add(item.Key);
                }
            }

            foreach (var key in new List<string>(generated.Keys))
            {
                if (!wanted.Contains(key))
                {
                    RemoveGenerated(key);
                    if (!IsCurrentRender(version))
                    {
                        return;
                    }
                }
            }

            var byKey = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry != null && entry.Button != null)
                {
                    byKey.Add(entry.Key, entry);
                }
            }

            if (model != null && buttonTemplate != null)
            {
                ValidateTemplate(buttonTemplate);
                foreach (var item in model.Items)
                {
                    if (byKey.ContainsKey(item.Key))
                    {
                        continue;
                    }

                    Button button = null;
                    try
                    {
                        button = Instantiate(buttonTemplate, transform, false);
                        // 挂到层级时可能释放当前控件；释放后不能再接管新按钮。
                        if (!IsAlive)
                        {
                            if (button != null)
                            {
                                Destroy(button.gameObject);
                            }

                            return;
                        }

                        if (button == null)
                        {
                            throw new InvalidOperationException("动态 Tab 按钮在创建回调中被销毁。");
                        }

                        button.name = item.Key;
                        button.onClick = new Button.ButtonClickedEvent();
                        var mark = button.transform.Find("Selected");
                        var entry = new Entry
                        {
                            Key = item.Key,
                            Button = button,
                            SelectionMark = mark == null ? null : mark.gameObject
                        };
                        UnityAction listener = () => RequestSelection(entry);
                        button.onClick.AddListener(listener);
                        generated.Add(item.Key, new GeneratedEntry { Entry = entry, Listener = listener });
                        byKey.Add(item.Key, entry);
                        var allEntries = new List<Entry>(entries)
                        {
                            entry
                        };
                        entries = allEntries.ToArray();
                        // 换绑时先保留新建按钮的清理归属，再让下一轮根据最新条目决定复用或移除。
                        if (!IsCurrentRender(version))
                        {
                            return;
                        }
                    }
                    catch
                    {
                        if (button != null)
                        {
                            var buttonObject = button.gameObject;
                            buttonObject.SetActive(false);
                            if (buttonObject != null)
                            {
                                Destroy(buttonObject);
                            }
                        }

                        throw;
                    }
                }
            }

            var ordered = new List<Entry>();
            if (model != null)
            {
                foreach (var item in model.Items)
                {
                    if (byKey.TryGetValue(item.Key, out var entry))
                    {
                        ordered.Add(entry);
                        byKey.Remove(item.Key);
                    }
                }
            }

            foreach (var entry in byKey.Values)
            {
                ordered.Add(entry);
            }

            entries = ordered.ToArray();
            synchronizedItems = items;
        }

        private void ArrangeButtons(int version)
        {
            var directCount = 0;
            foreach (var entry in entries)
            {
                if (entry != null && entry.Button != null && entry.Button.transform.parent == transform)
                {
                    ++directCount;
                }
            }

            // 保留模板和装饰节点在前，按钮按条目顺序排在末尾；已就位的按钮不再触发层级回调。
            var siblingIndex = transform.childCount - directCount;
            foreach (var entry in entries)
            {
                if (entry == null || entry.Button == null || entry.Button.transform.parent != transform)
                {
                    continue;
                }

                var buttonTransform = entry.Button.transform;
                if (buttonTransform.GetSiblingIndex() != siblingIndex)
                {
                    buttonTransform.SetSiblingIndex(siblingIndex);
                    if (!IsCurrentRender(version))
                    {
                        return;
                    }
                }

                ++siblingIndex;
            }
        }

        private void RemoveGenerated(string key)
        {
            if (!generated.TryGetValue(key, out var value))
            {
                return;
            }

            generated.Remove(key);
            var remaining = new List<Entry>();
            foreach (var entry in entries)
            {
                if (!ReferenceEquals(entry, value.Entry))
                {
                    remaining.Add(entry);
                }
            }

            entries = remaining.ToArray();
            if (value.Entry.Button == null)
            {
                return;
            }

            value.Entry.Button.onClick.RemoveListener(value.Listener);
            var buttonObject = value.Entry.Button.gameObject;
            buttonObject.SetActive(false);
            if (buttonObject != null)
            {
                Destroy(buttonObject);
            }
        }

        private void ReleaseGenerated()
        {
            foreach (var key in new List<string>(generated.Keys))
            {
                RemoveGenerated(key);
            }
        }

        private sealed class GeneratedEntry
        {
            public Entry Entry;
            public UnityAction Listener;
        }
    }
}
