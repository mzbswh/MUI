using System.Collections.Generic;
using System.Threading.Tasks;
using MUI.Samples.ResourceIntegration;
using MUI.Themes;
using MUI.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        private async Task DemonstrateMeasuredListAsync(View view, PageViewModel model)
        {
            var lifetime = new LifetimeScope();
            try
            {
                var localization = lifetime.OwnDisposable(new LocalizationService(CreateListLocale(false)));
                var themes = lifetime.OwnDisposable(new ThemeService(CreateListTheme(false)));
                await DemonstrateMeasuredListAsync(view, model, lifetime, localization, themes);
            }
            finally
            {
                await lifetime.DisposeAsync();
            }
        }

        private async Task DemonstrateMeasuredListAsync(View view, PageViewModel model, LifetimeScope lifetime,
            LocalizationService localization, ThemeService themes)
        {
            var list = view.GetElement<VirtualListElement>("VirtualItems");
            var values = new List<VirtualListItem>();
            for (var index = 0; index < 2000; ++index)
            {
                values.Add(new VirtualListItem(index, new ThingItemViewModel(),
                    templateKey: index % 5 == 0 ? "featured" : null));
            }

            var source = new ObservableList<VirtualListItem>(values, itemKey: item => item.Key);
            // 一个目录订阅批量刷新模型并发布 Update，保持逻辑键及选择；屏外项也使用当前语言。
            localization.Observe(lifetime, new LocalizedMessage("message"), value => source.Edit(editor =>
            {
                for (var index = 0; index < source.Count; ++index)
                {
                    var text = value.Text + " " + index;
                    if (index % 3 != 0)
                    {
                        text += "\n" + localization.Format(new LocalizedMessage("long")).Text;
                    }

                    if (index % 3 == 2)
                    {
                        text += "\n" + localization.Format(new LocalizedMessage("detail")).Text;
                    }

                    ((ThingItemViewModel)source[index].ViewModel).Label = text;
                    editor.NotifyUpdated(index);
                }
            }));
            themes.Observe(lifetime, ListFontSizeToken, size =>
            {
                if (list == null || !list.IsAlive)
                {
                    return;
                }

                // 同时更新模板及当前实例；后续复用采用新模板字号，并撤销旧测量值。
                foreach (var text in list.GetComponentsInChildren<Text>(true))
                {
                    if (text.gameObject.name == "ItemLabel")
                    {
                        text.fontSize = Mathf.RoundToInt(size);
                    }
                }

                list.InvalidateSizeMeasurements();
            });
            model.VirtualItems = source;
            var initial = await list.ScrollToKeyAsync(1500, focus: true, cancellationToken: cancellation.Token);
            Debug.Log($"MUI Measured list: initial={initial.Status}; first={list.FirstVisibleIndex}; cells={list.MaterializedCount}");
            if (initial.Status != VirtualListRevealStatus.Ready || view == null || !view.IsAlive)
            {
                return;
            }

            var scroll = list.GetComponentInChildren<ScrollRect>();
            scroll.viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 140);
            var narrowed = await list.ScrollToKeyAsync(1500, cancellationToken: cancellation.Token);
            Debug.Log($"MUI Measured list narrow: result={narrowed.Status}; first={list.FirstVisibleIndex}; cells={list.MaterializedCount}");
            if (narrowed.Status != VirtualListRevealStatus.Ready || view == null || !view.IsAlive)
            {
                return;
            }

            // 服务同步更新内容与字号；测量完成前暂停原生输入，令牌释放后恢复当前页面资格。
            using (view.InputGate.Block("Applying list language and theme"))
            {
                localization.SetCatalog(CreateListLocale(true));
                themes.SetCatalog(CreateListTheme(true));
                Debug.Log($"MUI Measured list services: locale={localization.Locale}; theme={themes.Name}; input={view.IsInputEnabled}");
                var resized = await list.ScrollToKeyAsync(1500, cancellationToken: cancellation.Token);
                Debug.Log($"MUI Measured list font: result={resized.Status}; first={list.FirstVisibleIndex}; cells={list.MaterializedCount}");
                if (resized.Status != VirtualListRevealStatus.Ready || view == null || !view.IsAlive)
                {
                    return;
                }
            }

            Debug.Log($"MUI Measured list input restored: input={view.IsInputEnabled}");
            ((ThingItemViewModel)source[1500].ViewModel).Label += "\n" + localization.Format(new LocalizedMessage("updated")).Text;
            source.NotifyUpdated(1500);
            var updated = await list.ScrollToKeyAsync(1500, cancellationToken: cancellation.Token);
            Debug.Log($"MUI Measured list update: result={updated.Status}; first={list.FirstVisibleIndex}; cells={list.MaterializedCount}");
        }
    }
}
