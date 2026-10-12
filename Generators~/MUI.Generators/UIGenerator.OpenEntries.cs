using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace MUI.Generators
{
    public sealed partial class UIGenerator
    {
        private sealed class OpenEntry
        {
            internal string Name;
            internal IMethodSymbol Method;
            internal IParameterSymbol[] Parameters;
            internal bool HasToken;
            internal bool IsDefault;
        }

        private static INamedTypeSymbol ResolveOpenPresenter(INamedTypeSymbol model, AttributeData route)
        {
            foreach (var option in route.NamedArguments)
                if (option.Key == "PresenterType") return option.Value.Value as INamedTypeSymbol;
            var prefix = model.Name.EndsWith("ViewModel", StringComparison.Ordinal)
                ? model.Name.Substring(0, model.Name.Length - "ViewModel".Length) : model.Name;
            var matches = model.ContainingType == null
                ? model.ContainingNamespace.GetTypeMembers(prefix + "Presenter", 0)
                : model.ContainingType.GetTypeMembers(prefix + "Presenter", 0);
            return matches.Length == 1 ? matches[0] : null;
        }

        private static bool IsOpenToken(IParameterSymbol parameter) => parameter.Type.ToDisplayString() == "System.Threading.CancellationToken";

        private static List<OpenEntry> ReadOpenEntries(INamedTypeSymbol presenter)
        {
            var entries = new List<OpenEntry>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var method in presenter.GetMembers().OfType<IMethodSymbol>())
            {
                var attribute = Attribute(method, "MUI.OpenEntryAttribute");
                if (attribute == null) continue;
                var isDefault = attribute.ConstructorArguments.Length == 0;
                var name = isDefault ? null : attribute.ConstructorArguments[0].Value as string;
                if (!isDefault && (string.IsNullOrEmpty(name) || !IsModuleIdentifier(name) || name == "OpenArgs"))
                    Fail(method, "OpenEntry 名称必须是唯一的有效标识符，且不能为 OpenArgs。");
                var returnType = method.ReturnType.ToDisplayString();
                if (method.MethodKind != MethodKind.Ordinary || method.IsStatic || method.IsAbstract || method.IsVirtual || method.IsOverride ||
                    method.IsGenericMethod || method.ExplicitInterfaceImplementations.Length != 0 ||
                    method.ReturnsVoid && method.IsAsync ||
                    !method.ReturnsVoid && returnType != "System.Threading.Tasks.Task" && returnType != "System.Threading.Tasks.ValueTask")
                    Fail(method, "OpenEntry 必须是非静态、非泛型的普通方法，返回 void、Task 或 ValueTask；不支持 async void 或虚方法。");
                foreach (var parameter in method.Parameters)
                {
                    if (parameter.RefKind != RefKind.None || parameter.IsParams || !IsPageType(parameter.Type) ||
                        !IsPublicPageType(parameter.Type))
                        Fail(parameter, "OpenEntry 参数必须是公开可存储类型，不支持 ref/out/in、params 或开放泛型。");
                    if (IsOpenToken(parameter) && !SymbolEqualityComparer.Default.Equals(parameter, method.Parameters.Last()))
                        Fail(parameter, "框架注入的 CancellationToken 必须位于入口参数末尾。");
                }
                var hasToken = method.Parameters.Length > 0 && IsOpenToken(method.Parameters.Last());
                var parameters = method.Parameters.Take(method.Parameters.Length - (hasToken ? 1 : 0)).ToArray();
                if (parameters.Length > 0 && parameters[0].Type.ToDisplayString() == "MUI.Pages.OpenCallOptions")
                    Fail(method, "OpenCallOptions 为生成入口保留的导航选项，不能作为首个业务参数。");
                if (isDefault)
                    name = parameters.Length == 0 ? "Default" : "Entry" + Identifier(string.Join(";", parameters.Select(parameter => TypeName(parameter.Type))));
                if (!names.Add(name))
                    Fail(method, "OpenEntry 名称或业务参数签名重复；CancellationToken 不参与重载区分。");
                var properties = new HashSet<string>(StringComparer.Ordinal);
                foreach (var parameter in parameters)
                {
                    var property = OpenProperty(parameter);
                    if (property == name || property == "Equals" || property == "GetHashCode" || property == "GetType" ||
                        property == "ToString" || property == "MemberwiseClone" || property == "Finalize" || !properties.Add(property))
                        Fail(parameter, "OpenEntry 参数生成的属性名称冲突：" + property);
                }
                entries.Add(new OpenEntry { Name = name, Method = method, Parameters = parameters, HasToken = hasToken, IsDefault = isDefault });
            }
            if (entries.Count == 0) Fail(presenter, "自动参数 Presenter 必须声明至少一个 OpenEntry。");
            var defaults = entries.Where(entry => entry.IsDefault).ToArray();
            for (var i = 0; i < defaults.Length; i++)
            {
                for (var j = i + 1; j < defaults.Length; j++)
                {
                    var left = defaults[i].Parameters;
                    var right = defaults[j].Parameters;
                    var supplied = Math.Max(left.Count(parameter => !parameter.IsOptional), right.Count(parameter => !parameter.IsOptional));
                    bool SamePrefix(int count) => Enumerable.Range(0, count).All(index =>
                        SymbolEqualityComparer.Default.Equals(left[index].Type, right[index].Type));
                    if (left.Length == right.Length && SamePrefix(left.Length))
                        Fail(defaults[j].Method, "OpenEntry 业务参数签名重复；方法名称、参数名称、返回类型和 CancellationToken 不参与重载区分。");
                    if (supplied < left.Length && supplied < right.Length && SamePrefix(supplied))
                        Fail(defaults[j].Method, "无名 OpenEntry 的可选参数会造成调用歧义；请增加必填参数或为其中一个入口命名。");
                }
            }
            return entries.OrderBy(entry => entry.Name, StringComparer.Ordinal).ToList();
        }

        private static string OpenProperty(IParameterSymbol parameter) => char.ToUpperInvariant(parameter.Name[0]) + parameter.Name.Substring(1);
        private static string OpenParameters(OpenEntry entry, bool defaults) => string.Join(", ", entry.Parameters.Select(parameter =>
            TypeName(parameter.Type) + " " + Escape(parameter.Name) +
                (defaults && parameter.HasExplicitDefaultValue ? " = " + OpenDefaultValue(parameter) : "")));
        private static string OpenArguments(OpenEntry entry) => string.Join(", ", entry.Parameters.Select(parameter => Escape(parameter.Name)));

        private static string OpenDefaultValue(IParameterSymbol parameter)
        {
            var value = parameter.ExplicitDefaultValue;
            if (value == null) return "default(" + TypeName(parameter.Type) + ")";
            string literal;
            if (value is string text) literal = Literal(text);
            else if (value is char character) literal = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(character, true);
            else if (value is bool flag) literal = flag ? "true" : "false";
            else if (value is float single)
            {
                literal = float.IsNaN(single) ? "global::System.Single.NaN" :
                    float.IsPositiveInfinity(single) ? "global::System.Single.PositiveInfinity" :
                    float.IsNegativeInfinity(single) ? "global::System.Single.NegativeInfinity" :
                    single.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "F";
            }
            else if (value is double number)
            {
                literal = double.IsNaN(number) ? "global::System.Double.NaN" :
                    double.IsPositiveInfinity(number) ? "global::System.Double.PositiveInfinity" :
                    double.IsNegativeInfinity(number) ? "global::System.Double.NegativeInfinity" :
                    number.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "D";
            }
            else
            {
                literal = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
                if (value is decimal) literal += "M";
                else if (value is ulong) literal += "UL";
                else if (value is long) literal += "L";
                else if (value is uint) literal += "U";
            }
            return parameter.Type.TypeKind == TypeKind.Enum ? "(" + TypeName(parameter.Type) + ")" + literal : literal;
        }

        /// <summary>在同一次生成内补全语义模型，供已有页面定义和命令生成复用；不依赖生成器再次运行。</summary>
        private static GenerationContext AddOpenEntries(GenerationContext context, ref ImmutableArray<INamedTypeSymbol> candidates)
        {
            var compilation = context.Compilation;
            var models = candidates.Where(type => type != null && Attribute(type, "MUI.UIPageAttribute") is AttributeData route && route.ConstructorArguments.Length < 2)
                .Distinct(SymbolEqualityComparer.Default).Cast<INamedTypeSymbol>().ToArray();
            var owners = new Dictionary<INamedTypeSymbol, int>(SymbolEqualityComparer.Default);
            foreach (var model in models)
            {
                var declaration = Attribute(model, "MUI.UIPageAttribute");
                var presenter = ResolveOpenPresenter(model, declaration);
                if (presenter != null) owners[presenter] = owners.TryGetValue(presenter, out var count) ? count + 1 : 1;
            }
            foreach (var candidate in candidates.Where(type => type != null).Distinct(SymbolEqualityComparer.Default).Cast<INamedTypeSymbol>())
            {
                if (!owners.ContainsKey(candidate) && candidate.GetMembers().Any(member => Attribute(member, "MUI.OpenEntryAttribute") != null))
                    context.ReportDiagnostic(Diagnostic.Create(InvalidContract, candidate.Locations.FirstOrDefault(),
                        "OpenEntry 需要关联声明 UIPage 的模型；请使用约定 Presenter 名称或显式指定 PresenterType。"));
            }
            foreach (var model in models)
            {
                try
                {
                    ValidateTypeScope(context, model);
                    var declaration = Attribute(model, "MUI.UIPageAttribute");
                    var presenter = ResolveOpenPresenter(model, declaration);
                    var result = declaration.ConstructorArguments.Length == 0
                        ? context.Compilation.GetTypeByMetadataName("MUI.Unit")
                        : declaration.ConstructorArguments[0].Value as ITypeSymbol;
                    if (presenter == null || presenter.IsAbstract || presenter.IsStatic || presenter.TypeKind != TypeKind.Class ||
                        !SymbolEqualityComparer.Default.Equals(presenter.ContainingAssembly, context.Compilation.Assembly))
                        Fail(model, "找不到可用的 Presenter：请在同一作用域声明约定名称的具体 Presenter，或通过 PresenterType 显式指定当前程序集类型。");
                    ValidateTypeScope(context, presenter);
                    if (owners[presenter] != 1) Fail(presenter, "自动参数 Presenter 只能对应一个 UIPage。");
                    if (model.IsAbstract || presenter.Arity != 0 || !IsPageType(model) || !IsPageType(presenter) || !IsPageType(result))
                        Fail(model, "自动参数页面要求模型、Presenter 和结果均为非开放类型。");
                    if (model.DeclaredAccessibility == Accessibility.Public && (!IsPublicPageType(presenter) || !IsPublicPageType(result)))
                        Fail(model, "公共页面的 Presenter 和结果类型必须公开。");
                    if (presenter.BaseType != null && presenter.BaseType.SpecialType != SpecialType.System_Object)
                        Fail(presenter, "自动参数 Presenter 的基类由生成器提供，请移除手写基类。");
                    foreach (var reference in presenter.DeclaringSyntaxReferences)
                        if (!(reference.GetSyntax() is ClassDeclarationSyntax syntax) || !syntax.Modifiers.Any(SyntaxKind.PartialKeyword))
                            Fail(presenter, "自动参数 Presenter 必须使用 partial class。");
                    if (presenter.GetMembers("OpenArgs").Length != 0 || presenter.GetMembers().OfType<IMethodSymbol>().Any(method =>
                        method.IsOverride && method.Name == "OnOpen") || presenter.AllInterfaces.Any(type =>
                        type.OriginalDefinition.ToDisplayString() == "MUI.IAsyncOpenPresenter<TArgs>"))
                        Fail(presenter, "Presenter 已有与生成的打开协议冲突的成员。");
                    var entries = ReadOpenEntries(presenter);
                    var body = new StringBuilder();
                    var argsName = TypeName(presenter) + ".OpenArgs";
                    body.Append(presenter.DeclaredAccessibility == Accessibility.Public ? "public" : "internal")
                        .Append(" partial class ").Append(Escape(presenter.Name)).Append(" : global::MUI.Presenter<")
                        .Append(TypeName(model)).Append(", ").Append(argsName).Append(", ").Append(TypeName(result))
                        .Append(">, global::MUI.IAsyncOpenPresenter<").Append(argsName).Append(">\n{\n")
                        .Append("public abstract class OpenArgs\n{\nprivate OpenArgs() {}\n");
                    foreach (var entry in entries)
                    {
                        body.Append("public sealed class ").Append(Escape(entry.Name)).Append(" : OpenArgs\n{\n");
                        foreach (var parameter in entry.Parameters)
                            body.Append("public ").Append(TypeName(parameter.Type)).Append(' ').Append(Escape(OpenProperty(parameter))).Append(" { get; }\n");
                        body.Append("public ").Append(Escape(entry.Name)).Append('(').Append(OpenParameters(entry, false)).Append(")\n{\n");
                        foreach (var parameter in entry.Parameters)
                            body.Append("this.").Append(Escape(OpenProperty(parameter))).Append(" = ").Append(Escape(parameter.Name)).Append(";\n");
                        body.Append("}\n}\n");
                    }
                    body.Append("}\nglobal::System.Threading.Tasks.ValueTask global::MUI.IAsyncOpenPresenter<")
                        .Append(argsName).Append(">.OnOpenAsync(").Append(argsName)
                        .Append(" args, global::System.Threading.CancellationToken cancellationToken)\n{\ncancellationToken.ThrowIfCancellationRequested();\nswitch (args)\n{\n");
                    foreach (var entry in entries)
                    {
                        body.Append("case OpenArgs.").Append(Escape(entry.Name)).Append(" value:\n{");
                        var arguments = entry.Parameters.Select(parameter => "value." + Escape(OpenProperty(parameter))).ToList();
                        if (entry.HasToken) arguments.Add("cancellationToken");
                        var call = "this." + Escape(entry.Method.Name) + "(" + string.Join(", ", arguments) + ")";
                        if (entry.Method.ReturnsVoid) body.Append(call).Append("; return default;");
                        else if (entry.Method.ReturnType.ToDisplayString() == "System.Threading.Tasks.Task")
                            body.Append("return new global::System.Threading.Tasks.ValueTask(").Append(call).Append(");");
                        else body.Append("return ").Append(call).Append(';');
                        body.Append("}\n");
                    }
                    body.Append("default: throw new global::System.ArgumentException(\"Invalid open entry arguments.\", nameof(args));\n}\n}\n}\n");
                    var hint = Identifier(presenter.ToDisplayString()) + ".OpenEntries.g.cs";
                    var code = context.AddSource(hint, SourceText.From(WrapTypeScope(presenter, body), Encoding.UTF8));
                    compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(code,
                        (CSharpParseOptions)compilation.SyntaxTrees.First().Options, hint, Encoding.UTF8));
                }
                catch (ContractException error)
                {
                    context.ReportDiagnostic(Diagnostic.Create(InvalidContract, error.Location, error.Message));
                }
            }
            if (compilation == context.Compilation) return context;
            candidates = compilation.SyntaxTrees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Select(node => compilation.GetSemanticModel(tree).GetDeclaredSymbol(node) as INamedTypeSymbol)).Where(type => type != null).ToImmutableArray();
            return context.WithCompilation(compilation);
        }

        private static void AppendOpenEntryCalls(StringBuilder output, INamedTypeSymbol model, INamedTypeSymbol presenter,
            string argsName, string resultName)
        {
            var routeType = "global::MUI.Pages.PageDefinition<" + TypeName(model) + ", " + argsName + ", " + resultName + ">";
            foreach (var entry in ReadOpenEntries(presenter))
            {
                var parameters = OpenParameters(entry, true);
                var arguments = OpenArguments(entry);
                var suffix = parameters.Length == 0 ? "" : ", " + parameters;
                var argsType = argsName + "." + Escape(entry.Name);
                var argsFactory = entry.IsDefault ? "CreateArgs" : "Args" + entry.Name;
                output.Append("public static ").Append(argsType).Append(' ').Append(argsFactory).Append('(').Append(parameters)
                    .Append(") => new ").Append(argsType).Append('(').Append(arguments).Append(");\n");
                // Infrastructure names never shadow business parameter names.
                var names = new HashSet<string>(entry.Parameters.Select(parameter => parameter.Name), StringComparer.Ordinal);
                string Unique(string name) { while (!names.Add(name)) name = "_" + name; return name; }
                var pages = Unique("pages");
                var route = Unique("route");
                var options = Unique("options");
                foreach (var begin in new[] { false, true })
                {
                    var method = (begin ? "BeginOpen" : "Open") + (entry.IsDefault ? "" : entry.Name) + (begin ? "" : "Async");
                    var returnType = begin ? "global::MUI.Pages.OpenRequest<" + resultName + ">" :
                        "global::System.Threading.Tasks.ValueTask<global::MUI.Pages.OpenOutcome<" + resultName + ">>";
                    foreach (var withOptions in new[] { false, true })
                    {
                        output.Append("public static ").Append(returnType).Append(' ').Append(method)
                            .Append("(global::MUI.Pages.IPageManager ").Append(pages).Append(", ").Append(routeType).Append(' ').Append(route);
                        if (withOptions) output.Append(", global::MUI.Pages.OpenCallOptions ").Append(options);
                        output.Append(suffix).Append(") => ").Append(pages).Append(begin ? ".BeginOpen(" : ".OpenAsync(")
                            .Append(route).Append(", new ").Append(argsType).Append('(').Append(arguments).Append(')');
                        if (withOptions) output.Append(", cancellationToken: ").Append(options).Append(".CancellationToken, owner: ").Append(options).Append(".Owner");
                        output.Append(");\n");
                    }
                }
            }
        }
    }
}
