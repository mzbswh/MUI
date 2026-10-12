using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MUI.Editor
{
    public sealed partial class PageWizard
    {
        [SerializeField] private bool createWidget;

        private void DrawWidgetBehavior()
        {
            using (new EditorGUILayout.VerticalScope(cardStyle))
            {
                GUILayout.Label("Widget 业务逻辑", EditorStyles.boldLabel);
                createPresenter = EditorGUILayout.Toggle("创建 Presenter", createPresenter);
                using (new EditorGUI.DisabledScope(!createPresenter))
                    typedContracts = EditorGUILayout.Toggle("初始化参数", typedContracts);
                GUILayout.Label("由父 View 管理显示、输入和生命周期，不生成导航入口、页面策略或关闭按钮。", noteStyle);
                GUILayout.Label(pageName + "ViewModel.cs", noteStyle);
                if (createPresenter)
                    GUILayout.Label(pageName + "Presenter.cs", noteStyle);
                if (createPresenter && typedContracts)
                    GUILayout.Label(pageName + "Args.cs", noteStyle);
            }
        }

        private IReadOnlyDictionary<string, string> CreateWidgetSources()
        {
            var files = new Dictionary<string, string>();
            var ns = PageSourceTemplate.EscapeQualifiedName(targetNamespace);
            string Wrap(string body) => "// 由 MUI 向导创建，归项目维护。\nnamespace " + ns + "\n{\n" + body + "\n}\n";
            var model = pageName + "ViewModel";
            var args = createPresenter && typedContracts ? pageName + "Args" : "global::MUI.Unit";
            var options = new List<string>();
            var defaultKey = pageName.EndsWith("View", System.StringComparison.Ordinal) ? pageName : pageName + "View";
            if (PageResourceKey != defaultKey)
                options.Add(PageSourceTemplate.StringLiteral(PageResourceKey));
            if (createPresenter)
                options.Add("PresenterType = typeof(" + pageName + "Presenter)");
            var declaration = "    [global::MUI.UIWidget" + (options.Count == 0 ? "" : "(" + string.Join(", ", options) + ")") + "]\n";
            var body = new StringBuilder(declaration).Append("    public partial class ").Append(model).Append(" : global::MUI.ViewModel\n    {\n");
            // 已有 Prefab 不要求 Title。新原生骨架由 Prefab 自动声明提供 Title。
            if (existingPrefab == null && !UsesAutomaticPageBindings)
                body.Append("        [global::MUI.ObservableProperty]\n        [global::MUI.Bind(\"Title\", nameof(global::MUI.ITextElement.Content), ElementType = typeof(global::MUI.ITextElement))]\n        private string title = ").Append(PageSourceTemplate.StringLiteral(pageName)).Append(";\n");
            body.Append("    }");
            files.Add(model + ".cs", Wrap(body.ToString()));
            if (createPresenter)
            {
                files.Add(pageName + "Presenter.cs", Wrap("    public sealed partial class " + pageName + "Presenter : global::MUI.Presenter<" + model + ", " + args + ", global::MUI.Unit>\n    {\n        protected override void OnPrepareOpen(" + args + " args)\n        {\n            // 通过 VM 访问模型；异步准备可实现 IAsyncOpenPreparation<" + args + ">。\n        }\n    }"));
                if (typedContracts)
                    files.Add(pageName + "Args.cs", Wrap("    public readonly struct " + args + "\n    {\n        // 按业务需要添加参数，通过 WidgetReference.Args 传入。\n    }"));
            }
            return files;
        }
    }
}
