using MUI.Navigation.Editor;
using UnityEditor;
using UnityEngine;

namespace MUI.Samples.Navigation.Editor
{
    /// <summary>显式从已初始化示例采集路由，不反射项目静态字段或调用未知路由工厂。</summary>
    public static class DependencyGraphMenu
    {
        [MenuItem("CONTEXT/LocalDependenciesDemo/查看路由依赖图")]
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
                Debug.LogWarning("请先运行并成功初始化共享依赖示例，再采集其路由定义。", demo);
                return;
            }
            RouteGraphWindow.Show(routes);
        }
    }
}
