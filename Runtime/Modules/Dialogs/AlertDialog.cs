using MUI.Pages;
using MUI.Resources;

namespace MUI.Dialogs
{
    /// <summary>页面策略与生命周期工厂；显示绑定由具体 View 提供。</summary>
    public static class AlertDialog
    {
        /// <summary>默认创建单实例模态路由；可显式传入已解析的项目策略。</summary>
        public static PageDefinition<AlertViewModel, AlertRequest, Unit> CreateDefinition(
            ViewResource resource, System.Func<IView, AlertViewModel, BindingContext<AlertViewModel>> bind, string key = "mui.alert", int layer = 1000, PagePolicy policy = null)
        {
            return new PageDefinition<AlertViewModel, AlertRequest, Unit>(
                key, resource,
                () => new AlertViewModel(),
                model => new AlertPresenter(),
                bind ?? throw new System.ArgumentNullException(nameof(bind)),
                policy ?? new PagePolicy(layer: layer, allowMultiple: true, maxInstances: 1,
                    coverage: CoveragePolicy.BlockInput, modal: true, pageRole: PageRole.Independent));
        }

    }
}
