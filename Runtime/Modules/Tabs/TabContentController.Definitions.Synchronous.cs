using System;
using System.Collections.Generic;

namespace MUI.Tabs
{
    public sealed partial class TabContentController
    {
        /// <summary>同步替换定义目录、清理失效缓存并选择回退项；先验证完整目录再修改现状。</summary>
        public TabSelectionResult UpdateDefinitions(IEnumerable<TabContentDefinition> replacement, string preferredKey = null)
        {
            RequireSynchronousMode();
            if (inactive || !scope.IsActive)
            {
                return new TabSelectionResult(TabSelectionStatus.ParentInactive);
            }

            if (IsChanging)
            {
                return new TabSelectionResult(TabSelectionStatus.Rejected, TabRejection.Reentrant);
            }

            if (replacement == null)
            {
                throw new ArgumentNullException(nameof(replacement));
            }

            changing = true;
            try
            {
                return scope.RunSynchronous(() => work.Run(_ => UpdateDefinitionsSynchronousCore(replacement, preferredKey)));
            }
            catch (Exception error)
            {
                return new TabSelectionResult(TabSelectionStatus.Failed, error: error);
            }
            finally
            {
                changing = false;
            }
        }

        private TabSelectionResult UpdateDefinitionsSynchronousCore(IEnumerable<TabContentDefinition> replacement, string preferredKey)
        {
            slot.RequireSynchronousIdle();
            var catalog = new Dictionary<string, TabContentDefinition>(StringComparer.Ordinal);
            var items = new List<TabItemState>();
            string fallback = null;
            var keep = false;
            foreach (var definition in replacement)
            {
                if (definition == null)
                {
                    throw new ArgumentException("Null Tab definition.", nameof(replacement));
                }

                RequireDefinitionMode(definition);
                catalog.Add(definition.Key, definition);
                var enabled = definition.IsEnabled();
                items.Add(new TabItemState(definition.Key, definition.Label, enabled));
                if (enabled && (fallback == null || definition.Key == preferredKey))
                {
                    fallback = definition.Key;
                }

                if (enabled && definition.Key == ViewModel.Snapshot.SelectedTab &&
                    definitions.TryGetValue(definition.Key, out var old) && ReferenceEquals(old, definition))
                {
                    keep = true;
                }
            }

            scope.RequireActive();
            definitions.Clear();
            foreach (var pair in catalog)
            {
                definitions.Add(pair.Key, pair.Value);
            }

            ViewModel.ReplaceItems(items);
            try
            {
                InvalidateCacheSynchronous(false);
                if (keep)
                {
                    var snapshot = ViewModel.Snapshot;
                    return new TabSelectionResult(snapshot.Phase == TabPhase.Ready ? TabSelectionStatus.Ready :
                        snapshot.Phase == TabPhase.Error ? TabSelectionStatus.Failed : TabSelectionStatus.Empty, error: snapshot.Error);
                }

                ClearContent();
                Publish(new TabSnapshot(null, null, TabPhase.Empty, null, ++version));
                return fallback == null ? new TabSelectionResult(TabSelectionStatus.Empty)
                    : SelectSynchronousCore(catalog[fallback], true);
            }
            catch (Exception error)
            {
                var snapshot = ViewModel.Snapshot;
                Publish(new TabSnapshot(snapshot.SelectedTab, slot.Current == null ? null : snapshot.DisplayedTab,
                    TabPhase.Error, error, ++version));
                return new TabSelectionResult(TabSelectionStatus.Failed, error: error);
            }
            finally
            {
                ViewModel.NotifyItemsChanged();
            }
        }

        /// <summary>同步重新求值可用性，并按目录顺序或指定键替换已失效选择。</summary>
        public TabSelectionResult ReconcileAvailability(string preferredKey = null)
        {
            RequireSynchronousMode();
            var ordered = new List<TabContentDefinition>();
            foreach (var item in ViewModel.Items)
            {
                if (definitions.TryGetValue(item.Key, out var definition))
                {
                    ordered.Add(definition);
                }
            }

            return UpdateDefinitions(ordered, preferredKey);
        }
    }
}
