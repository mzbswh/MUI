using MUI.Navigation.Editor;
using UnityEditor;
using UnityEngine;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Samples.Navigation.Editor
{
    /// <summary>显式从已初始化示例采集路由，不反射项目静态字段或调用未知路由工厂。</summary>
    public static class DependencyGraphMenu
    {
        [MenuItem("CONTEXT/LocalDependenciesDemo/查看路由依赖图 (Route Graph)")]
        private static void Show(MenuCommand command)
        {
            var demo = command.context as LocalDependenciesDemo;
            if (demo == null)
            {
                return;
            }
            var routes = demo.GetDiagnosticRoutes();
            if (routes.Count == 0)
            {
                Debug.LogWarning(L.Get("sample.navigation.graphNotReady"), demo);
                return;
            }
            RouteGraphWindow.Show(routes);
        }
    }
}
