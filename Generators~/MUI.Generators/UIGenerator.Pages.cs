using System;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace MUI.Generators
{
    public sealed partial class UIGenerator
    {
        private static string PageFacadeName(INamedTypeSymbol model)
        {
            var attribute = Attribute(model, "MUI.UIPageAttribute");
            foreach (var option in attribute.NamedArguments)
                if (option.Key == "PageName") return option.Value.Value as string;
            var name = model.Name.EndsWith("ViewModel", StringComparison.Ordinal)
                ? model.Name.Substring(0, model.Name.Length - "ViewModel".Length) : model.Name;
            return name.EndsWith("Page", StringComparison.Ordinal) ? name : name + "Page";
        }

        private static void GeneratePageFacade(GenerationContext context, INamedTypeSymbol model, INamedTypeSymbol presenter,
            ITypeSymbol args, ITypeSymbol result, string policyPreset, bool requirePresenterFactory, bool automaticArgs)
        {
            // 核心程序集仍可只使用显式页面定义；全局页面入口仅在引用 UGUI 时生成。
            if (context.Compilation.GetTypeByMetadataName("MUI.UGUI.UIHost") == null) return;
            var pageName = PageFacadeName(model);
            if (!IsModuleIdentifier(pageName)) Fail(model, "UIPage.PageName 必须是有效的非关键字类名。");
            if (HasSiblingType(model, pageName) || pageName == model.Name + "Definition" ||
                pageName == model.Name + "BindingFactory" || pageName == model.Name + "BindingContext")
                Fail(model, "生成的页面入口与现有类型重名，请设置 UIPage.PageName：" + pageName);
            var siblings = model.ContainingType == null ? model.ContainingNamespace.GetTypeMembers() : model.ContainingType.GetTypeMembers();
            foreach (var other in siblings)
            {
                if (SymbolEqualityComparer.Default.Equals(other, model) || other.Arity != model.Arity ||
                    Attribute(other, "MUI.UIPageAttribute") == null) continue;
                if (PageFacadeName(other) == pageName)
                    Fail(model, "多个页面生成了相同入口，请设置不同的 UIPage.PageName：" + pageName);
            }
            var memberNames = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal) { "ConfigureDefinition" };
            if (automaticArgs)
            {
                foreach (var entry in ReadOpenEntries(presenter))
                {
                    var suffix = entry.IsDefault ? "" : entry.Name;
                    memberNames.Add("Open" + suffix + "Async");
                    memberNames.Add("BeginOpen" + suffix);
                    memberNames.Add(entry.IsDefault ? "CreateArgs" : "Args" + entry.Name);
                }
            }
            else
            {
                memberNames.Add("OpenAsync");
                memberNames.Add("BeginOpen");
            }
            if (memberNames.Contains(pageName)) Fail(model, "页面入口类名与生成方法重名，请更改 UIPage.PageName。");
            var modelName = TypeName(model);
            var argsName = TypeName(args);
            var resultName = TypeName(result);
            var routeType = "global::MUI.Pages.PageDefinition<" + modelName + ", " + argsName + ", " + resultName + ">";
            var typeParameters = TypeParameters(model);
            var routeFactory = Escape(model.Name + "Definition") + typeParameters;
            var pageType = Escape(pageName) + typeParameters;
            var canConstruct = !requirePresenterFactory && model.InstanceConstructors.Any(ctor => ctor.Parameters.Length == 0 &&
                context.Compilation.IsSymbolAccessibleWithin(ctor, context.Compilation.Assembly));
            var defaultFactory = !canConstruct ? "null" : "() => " + routeFactory + ".CreateDefinition()";
            var output = new StringBuilder();
            output.Append("/// <summary>业务页面入口，使用唯一 UIHost；页面定义按宿主生命周期复用。</summary>\n")
                .Append(model.DeclaredAccessibility == Accessibility.Public ? "public" : "internal")
                .Append(" static class ").Append(Escape(pageName)).Append(typeParameters).Append(TypeConstraints(model)).Append("\n{\n")
                .Append("public static void ConfigureDefinition(global::System.Func<").Append(routeType).Append("> factory) => ")
                .Append("global::MUI.UGUI.UIHost.Current.ConfigurePageDefinition(typeof(").Append(pageType).Append("), factory);\n");
            // 开放泛型上下文也使用封闭页面 Type 作为缓存键，避免实例间串用。
            void AppendCall(string method, string parameters, string argsExpression, bool begin, bool withOptions, string optionsName)
            {
                var returnType = begin ? "global::MUI.Pages.OpenRequest<" + resultName + ">" :
                    "global::System.Threading.Tasks.ValueTask<global::MUI.Pages.OpenOutcome<" + resultName + ">>";
                var signature = withOptions ? "global::MUI.Pages.OpenCallOptions " + optionsName +
                    (parameters.Length == 0 ? "" : ", ") + parameters : parameters;
                var hostName = "__muiHost";
                while (parameters.Contains(hostName) || optionsName == hostName) hostName += "_";
                var routeName = "__muiDefinition";
                while (parameters.Contains(routeName) || optionsName == routeName) routeName += "_";
                output.Append("public static ").Append(returnType).Append(' ').Append(method).Append('(').Append(signature).Append(")\n{\n")
                    .Append("var ").Append(hostName).Append(" = global::MUI.UGUI.UIHost.Current;\n")
                    .Append("var ").Append(routeName).Append(" = ").Append(hostName).Append(".GetPageDefinition<").Append(routeType)
                    .Append(">(typeof(").Append(pageType).Append("), ").Append(defaultFactory).Append(");\n")
                    .Append("return ").Append(hostName).Append(begin ? ".Pages.BeginOpen(" : ".Pages.OpenAsync(")
                    .Append(routeName).Append(", ").Append(argsExpression);
                if (withOptions) output.Append(", cancellationToken: ").Append(optionsName).Append(".CancellationToken, owner: ").Append(optionsName).Append(".Owner");
                output.Append(");\n}\n");
            }
            if (automaticArgs)
            {
                foreach (var entry in ReadOpenEntries(presenter))
                {
                    var parameters = OpenParameters(entry, true);
                    var arguments = OpenArguments(entry);
                    var argsType = argsName + "." + Escape(entry.Name);
                    var argsFactory = entry.IsDefault ? "CreateArgs" : "Args" + entry.Name;
                    output.Append("public static ").Append(argsType).Append(' ').Append(argsFactory).Append('(').Append(parameters)
                        .Append(") => new ").Append(argsType).Append('(').Append(arguments).Append(");\n");
                    var optionsName = "options";
                    while (entry.Parameters.Any(parameter => parameter.Name == optionsName)) optionsName = "_" + optionsName;
                    foreach (var begin in new[] { false, true })
                        foreach (var withOptions in new[] { false, true })
                            AppendCall((begin ? "BeginOpen" : "Open") + (entry.IsDefault ? "" : entry.Name) + (begin ? "" : "Async"),
                                parameters, "new " + argsType + "(" + arguments + ")", begin, withOptions, optionsName);
                }
            }
            else
            {
                var unit = args.ToDisplayString() == "MUI.Unit";
                foreach (var begin in new[] { false, true })
                    foreach (var withOptions in new[] { false, true })
                        AppendCall(begin ? "BeginOpen" : "OpenAsync", unit ? "" : argsName + " args",
                            unit ? "global::MUI.Unit.Value" : "args", begin, withOptions, "options");
            }
            output.Append("}\n");
            context.AddSource(Identifier(model.ToDisplayString()) + ".Page.g.cs", SourceText.From(WrapTypeScope(model, output), Encoding.UTF8));
        }
    }
}
