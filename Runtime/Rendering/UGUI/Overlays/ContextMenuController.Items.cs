using System;
using System.Collections.Generic;
using UnityEngine.UI;

namespace MUI.UGUI
{
    public sealed partial class ContextMenuController
    {
        private readonly List<Button> candidates = new List<Button>();
        private bool itemsDirty;

        /// <summary>
        /// 动态菜单子层级提交后调用，保留仍存在的 Button 身份及
        /// 原导航设置。焦点回调中的调用会合并到 LateUpdate。
        /// </summary>
        public void RefreshItems()
        {
            if (!isActiveAndEnabled)
            {
                throw new InvalidOperationException("Context menu controller is not active.");
            }

            if (synchronizing)
            {
                itemsDirty = true;
                return;
            }

            // 已关闭菜单会在下次打开时重新读取完整层级。
            if (!session)
            {
                return;
            }

            synchronizing = true;
            try
            {
                if (!CanRun())
                {
                    EndSession();
                    TryRestoreFocus();
                    return;
                }

                RebuildItems();
                RefreshNavigationAndFocus();
                if (CanRun())
                {
                    overlay.Refresh();
                }
            }
            catch
            {
                // 更新无效时，不能让新增且未托管的按钮留在可交互菜单中。
                CloseMenuSafely(null);
                throw;
            }
            finally
            {
                synchronizing = false;
            }
        }

        private void RebuildItems()
        {
            itemsDirty = false;
            candidates.Clear();
            content.GetComponentsInChildren(true, candidates);
            if (maxItems < 1 || candidates.Count > maxItems)
            {
                candidates.Clear();
                throw new InvalidOperationException("Context menu item capacity exceeded or invalid.");
            }

            foreach (var previous in items)
            {
                if (previous != null && candidates.Contains(previous))
                {
                    continue;
                }

                if (previous != null && authoredNavigation.TryGetValue(previous, out var navigation))
                {
                    previous.navigation = navigation;
                }

                if (!ReferenceEquals(previous, null))
                {
                    authoredNavigation.Remove(previous);
                }
            }

            foreach (var candidate in candidates)
            {
                if (candidate != null && !authoredNavigation.ContainsKey(candidate))
                {
                    authoredNavigation.Add(candidate, candidate.navigation);
                }
            }

            items.Clear();
            items.AddRange(candidates);
            candidates.Clear();
        }
    }
}
