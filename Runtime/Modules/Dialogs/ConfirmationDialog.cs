using MUI.Pages;
using MUI.Resources;

namespace MUI.Dialogs
{
    /// <summary>页面策略与生命周期工厂；显示绑定由具体 View 提供。</summary>
    public static class ConfirmationDialog
    {
        /// <summary>默认创建单实例模态路由；可显式传入已解析的项目策略。</summary>
        public static PageDefinition<ConfirmationViewModel, CloseConfirmation, bool> CreateDefinition(
            ViewResource resource, System.Func<IView, ConfirmationViewModel, BindingContext<ConfirmationViewModel>> bind, string key = "mui.confirmation", int layer = 1000, PagePolicy policy = null)
        {
            return new PageDefinition<ConfirmationViewModel, CloseConfirmation, bool>(
                key, resource,
                () => new ConfirmationViewModel(),
                model => new ConfirmationPresenter(),
                bind ?? throw new System.ArgumentNullException(nameof(bind)),
                policy ?? new PagePolicy(layer: layer, allowMultiple: true, maxInstances: 1,
                    coverage: CoveragePolicy.BlockInput, modal: true, pageRole: PageRole.Independent));
        }

    }
}
