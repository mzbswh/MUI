using System;
using System.Collections.Generic;
using System.Text;

namespace MUI.Editor
{
    internal enum PagePreset
    {
        FullScreen,
        Popup
    }

    internal static class PageSourceTemplate
    {
        internal static bool IsIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || !IsStart(value[0]))
            {
                return false;
            }

            foreach (var c in value)
            {
                if (!IsStart(c) && (c < '0' || c > '9'))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsStart(char c)
        {
            return c == '_' || c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z';
        }

        internal static bool IsNamespace(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            foreach (var part in value.Split('.'))
            {
                if (!IsIdentifier(part))
                {
                    return false;
                }
            }

            return true;
        }

        internal static string Policy(PagePreset preset)
        {
            return preset == PagePreset.Popup
                ? "new global::MUI.Navigation.RoutePolicy(layer: 100, coverage: global::MUI.Navigation.CoveragePolicy.BlockInput, modal: true)"
                : "new global::MUI.Navigation.RoutePolicy(coverage: global::MUI.Navigation.CoveragePolicy.Hide)";
        }

        /// <summary>保留 Element 原名，转义为合法 C# 字符串，不将节点名称当作源码拼接。</summary>
        private static string StringLiteral(string value)
        {
            var result = new StringBuilder("\"");
            foreach (var character in value)
            {
                if (character == '\\' || character == '\"')
                {
                    result.Append('\\').Append(character);
                }
                else if (char.IsControl(character) || char.IsSurrogate(character) || character == '\u2028' || character == '\u2029')
                {
                    result.Append("\\u").Append(((int)character).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                }
                else
                {
                    result.Append(character);
                }
            }
            return result.Append('\"').ToString();
        }

        /// <summary>生成归项目维护的页面源码；与绑定生成器输出的代码分开管理。</summary>
        internal static IReadOnlyDictionary<string, string> Create(
            string name,
            string ns,
            PagePreset preset,
            bool presenter,
            bool typed,
            Type titleElementType = null,
            bool synchronousLifecycle = false,
            string titleElementName = "Title", string closeElementName = "Close", string prefabAssetGuid = null)
        {
            if (!IsIdentifier(name) || !IsNamespace(ns))
            {
                throw new ArgumentException("Invalid page name or namespace.");
            }

            if (!Enum.IsDefined(typeof(PagePreset), preset))
            {
                throw new ArgumentOutOfRangeException(nameof(preset));
            }

            titleElementType = titleElementType ?? typeof(MUI.UGUI.TextElement);
            PageTextBackend.RequireElementType(titleElementType);
            var titleTypeName = "global::@" + titleElementType.FullName.Replace(".", ".@");
            if (string.IsNullOrWhiteSpace(titleElementName) || string.IsNullOrWhiteSpace(closeElementName))
            {
                throw new ArgumentException("页面骨架需要标题和关闭按钮的 Element 名称。");
            }
            var titleName = StringLiteral(titleElementName);
            var closeName = StringLiteral(closeElementName);
            var escapedNamespace = "@" + ns.Replace(".", ".@");
            string Wrap(params string[] lines)
            {
                return "// 由 MUI 页面向导创建。此源码归项目所有，可以自由修改。\n" +
                    "namespace " + escapedNamespace + "\n{\n" + string.Join("\n", lines) + "\n}\n";
            }

            var args = typed ? name + "Args" : "global::MUI.Unit";
            var result = typed ? name + "Result" : "global::MUI.Unit";
            var routeDeclaration = $"    [global::MUI.Navigation.ViewRoute(typeof({args}), typeof({result}), Key = {StringLiteral(ns + "." + name)}";
            if (presenter)
            {
                routeDeclaration += $", PresenterType = typeof({name}Presenter)";
            }

            if (synchronousLifecycle)
            {
                routeDeclaration += ", SupportsSynchronousLifecycle = true";
            }

            routeDeclaration += ")]";

            var files = new Dictionary<string, string>(StringComparer.Ordinal);
            files.Add(name + "ViewModel.cs", Wrap(
                "    /// <summary>页面表现状态，通过生成绑定连接到预制控件。</summary>",
                $"    [global::MUI.ViewContract(\"{name}View\")]",
                routeDeclaration,
                $"    public partial class {name}ViewModel : global::MUI.ViewModel",
                "    {",
                "        /// <summary>页面标题，由生成的 Title 属性发布变化。</summary>",
                "        [global::MUI.ObservableProperty]",
                $"        [global::MUI.Bind({titleName}, nameof({titleTypeName}.Content),",
                $"            ElementType = typeof({titleTypeName}))]",
                $"        private string title = \"{name}\";",
                "",
                "        /// <summary>请求关闭本次激活，不等待自身命令清理。</summary>",
                "        [global::MUI.Command]",
                $"        [global::MUI.BindCommand({closeName}, nameof(global::MUI.UGUI.ButtonElement.Clicked),",
                "            ElementType = typeof(global::MUI.UGUI.ButtonElement))]",
                "        private void Close(global::MUI.CommandContext context)",
                "        {",
                "            context.RequestClose();",
                "        }",
                "    }"));

            if (typed)
            {
                files.Add(name + "Args.cs", Wrap(
                    "    /// <summary>本次打开页面的参数，按项目需求扩展。</summary>",
                    $"    public readonly struct {name}Args",
                    "    {",
                    $"        public {name}Args(string title)",
                    "        {",
                    "            Title = title;",
                    "        }",
                    "",
                    "        /// <summary>页面初始标题。</summary>",
                    "        public string Title { get; }",
                    "    }"));
                files.Add(name + "Result.cs", Wrap(
                    "    /// <summary>页面完成时返回的数据；关闭页面不自动产生此结果。</summary>",
                    $"    public readonly struct {name}Result",
                    "    {",
                    $"        public {name}Result(string value)",
                    "        {",
                    "            Value = value;",
                    "        }",
                    "",
                    "        /// <summary>示例结果值，按业务含义替换。</summary>",
                    "        public string Value { get; }",
                    "    }"));
            }

            if (presenter)
            {
                files.Add(name + "Presenter.cs", Wrap(
                    "    /// <summary>接入页面生命周期，业务数据写入 ViewModel。</summary>",
                    $"    public sealed class {name}Presenter : global::MUI.Presenter<{name}ViewModel, {args}, {result}>",
                    "    {",
                    $"        protected override void OnOpen({args} args)",
                    "        {",
                    typed
                        ? "            ViewModel.Title = args.Title ?? string.Empty;"
                        : "            // 使用本次激活的生命周期加载页面数据。",
                    "        }",
                    "    }"));
            }

            var pageLines = new List<string>
            {
                "    /// <summary>页面资源标识与类型化路由入口。</summary>",
                $"    public static class {name}Page",
                "    {",
                "        /// <summary>页面预制体的资源标识，由资源提供器解析。</summary>",
                "        public static global::MUI.Resources.ViewResource Resource { get; } =",
                $"            {name}ViewModelRoute.Resource;",
                "",
                "        /// <summary>创建路由定义；每次打开的实例由导航器管理。</summary>",
                $"        public static global::MUI.Navigation.Route<{name}ViewModel, {args}, {result}> CreateRoute()",
                "        {",
                $"            return {name}ViewModelRoute.Create(",
                $"                () => new {name}ViewModel(),"
            };
            if (presenter)
            {
                pageLines.Add($"                presenterFactory: model => new {name}Presenter(),");
            }

            pageLines.Add("                policy: " + Policy(preset) + ");");
            pageLines.Add("        }");
            pageLines.Add("    }");
            if (!string.IsNullOrEmpty(prefabAssetGuid))
            {
                pageLines.InsertRange(3, new[]
                {
                    "        /// <summary>关联的已有 Prefab 资产 GUID；项目须将 Resource 登记到该资产，不作为运行时加载路径。</summary>",
                    "        public const string PrefabAssetGuid = " + StringLiteral(prefabAssetGuid) + ";",
                    ""
                });
            }
            files.Add(name + "Page.cs", Wrap(pageLines.ToArray()));
            return files;
        }
    }
}
