using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace MUI.Generators
{
    public sealed partial class UIGenerator
    {
        /// <summary>生成页面定义的类型接线，不生成服务构造、资源加载或运行时类型扫描。</summary>
        private static void GeneratePageDefinition(GenerationContext context, INamedTypeSymbol model,
            IEnumerable<INamedTypeSymbol> candidates)
        {
            var declaration = Attribute(model, "MUI.UIPageAttribute");
            if (declaration == null)
            {
                return;
            }

            if (model.IsAbstract)
            {
                Fail(model, "UIPage 必须声明在具体模型上。");
            }

            var automaticArgs = declaration.ConstructorArguments.Length < 2;
            var declaredPresenter = ResolveOpenPresenter(model, declaration);
            var args = automaticArgs ? PresenterBase(declaredPresenter)?.TypeArguments[1] : declaration.ConstructorArguments[0].Value as ITypeSymbol;
            var result = declaration.ConstructorArguments.Length == 0
                ? context.Compilation.GetTypeByMetadataName("MUI.Unit")
                : declaration.ConstructorArguments[automaticArgs ? 0 : 1].Value as ITypeSymbol;
            if (!IsPageType(args) || !IsPageType(result))
            {
                Fail(model, "页面定义参数及结果必须是可用作泛型参数的非开放类型。");
            }

            if (!context.Compilation.IsSymbolAccessibleWithin(args, context.Compilation.Assembly) ||
                !context.Compilation.IsSymbolAccessibleWithin(result, context.Compilation.Assembly))
            {
                Fail(model, "页面定义参数及结果必须能从生成工厂访问。");
            }

            var key = model.ToDisplayString();
            var explicitKey = false;
            string policyPreset = null;
            INamedTypeSymbol presenter = null;
            var explicitPresenter = false;
            foreach (var option in declaration.NamedArguments)
            {
                if (option.Key == "Key")
                {
                    explicitKey = true;
                    key = option.Value.Value as string;
                }
                else if (option.Key == "PresenterType")
                {
                    explicitPresenter = true;
                    presenter = option.Value.Value as INamedTypeSymbol;
                }
                else if (option.Key == "PolicyPreset")
                {
                    policyPreset = option.Value.Value as string;
                    if (string.IsNullOrWhiteSpace(policyPreset))
                    {
                        Fail(model, "UIPage.PolicyPreset 不能为空。");
                    }
                    if (context.Compilation.GetTypeByMetadataName("MUI.UGUI.MUISettings") == null)
                    {
                        Fail(model, "UIPage.PolicyPreset 需要引用 MUI.UGUI。");
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(key))
            {
                Fail(model, "UIPage 页面定义键不能为空。");
            }

            if (automaticArgs)
            {
                presenter = declaredPresenter;
                explicitPresenter = true;
            }
            if (explicitPresenter)
            {
                if (!IsCompatiblePresenter(presenter, model, args, result) ||
                    !context.Compilation.IsSymbolAccessibleWithin(presenter, context.Compilation.Assembly))
                {
                    Fail(model, "显式 Presenter 必须是可访问的具体类型，并匹配模型、参数和结果类型。");
                }
            }
            else
            {
                var compatible = candidates.Where(type => IsCompatiblePresenter(type, model, args, result) &&
                    context.Compilation.IsSymbolAccessibleWithin(type, context.Compilation.Assembly))
                    .Distinct(SymbolEqualityComparer.Default).ToArray();
                if (compatible.Length > 1)
                {
                    Fail(model, "存在多个兼容 Presenter，请通过 UIPage.PresenterType 明确选择。");
                }

                if (compatible.Length == 1)
                {
                    presenter = (INamedTypeSymbol)compatible[0];
                }
            }

            var routeName = model.Name + "Definition";
            if (HasSiblingType(model, routeName))
            {
                Fail(model, "生成页面定义工厂与现有类型重名：" + routeName);
            }

            var publicApi = model.DeclaredAccessibility == Accessibility.Public;
            if (publicApi && (!IsPublicPageType(args) || !IsPublicPageType(result)))
            {
                Fail(model, "公共模型生成的页面定义要求参数及结果类型公开可访问。");
            }

            var modelName = TypeName(model);
            var argsName = TypeName(args);
            var resultName = TypeName(result);
            var presenterBase = "global::MUI.Presenter<" + modelName + ", " + argsName + ", " + resultName + ">";
            var canConstructPresenter = presenter != null && presenter.InstanceConstructors.Any(ctor =>
                ctor.Parameters.Length == 0 && context.Compilation.IsSymbolAccessibleWithin(ctor, context.Compilation.Assembly));
            var requirePresenterFactory = presenter != null && !canConstructPresenter;
            var typeParameters = TypeParameters(model);
            var keyExpression = model.Arity != 0 && !explicitKey ? "typeof(" + modelName + ").FullName" : Literal(key);
            var output = new StringBuilder();
            output.Append("    /// <summary>生成的类型化页面定义接线；模型及业务依赖由项目工厂创建。</summary>\n    ")
                .Append(publicApi ? "public" : "internal").Append(" static class ").Append(routeName).Append(typeParameters).Append(TypeConstraints(model)).Append("\n    {\n")
                .Append(model.Arity == 0 ? "        public const string Key = " : "        public static readonly string Key = ").Append(keyExpression).Append(";\n")
                .Append("        public static global::MUI.Resources.ViewResource Resource\n        {\n            get;\n        } = new global::MUI.Resources.ViewResource(")
                .Append(model.Name).Append("BindingFactory").Append(typeParameters).Append(".ResourceKey);\n\n")
                .Append("        public static global::MUI.Pages.PageDefinition<").Append(modelName).Append(", ").Append(argsName).Append(", ").Append(resultName).Append("> Create(\n")
                .Append("            global::System.Func<").Append(modelName).Append("> modelFactory,\n")
                .Append("            global::System.Func<").Append(modelName).Append(", ").Append(presenterBase).Append("> presenterFactory")
                .Append(requirePresenterFactory ? ",\n" : " = null,\n")
                .Append("            global::MUI.Pages.PagePolicy policy = null,\n")
                .Append("            global::System.Func<").Append(argsName).Append(", ").Append(argsName).Append(", bool> argsEqual = null,\n")
                .Append("            global::System.Collections.Generic.IReadOnlyList<global::MUI.Pages.PageDependency<").Append(argsName).Append(">> dependencies = null,\n")
                .Append("            string key = null, global::MUI.Resources.ViewResource resource = null,\n")
                .Append("            long? estimatedRetainedBytes = null)\n        {\n");
            if (requirePresenterFactory)
            {
                output.Append("            if (presenterFactory == null)\n            {\n                throw new global::System.ArgumentNullException(nameof(presenterFactory));\n            }\n");
            }
            else if (canConstructPresenter)
            {
                output.Append("            if (presenterFactory == null)\n            {\n                presenterFactory = model => new ").Append(TypeName(presenter)).Append("();\n            }\n");
            }

            output.Append("            return new global::MUI.Pages.PageDefinition<").Append(modelName).Append(", ").Append(argsName).Append(", ").Append(resultName).Append(">(\n")
                .Append("                key ?? Key, resource ?? Resource, modelFactory, presenterFactory, ").Append(model.Name).Append("BindingFactory").Append(typeParameters).Append(".Create,\n")
                .Append("                policy: policy, argsEqual: argsEqual, estimatedRetainedBytes: estimatedRetainedBytes")
                .Append(", dependencies: dependencies);\n        }\n");
            if (context.Compilation.GetTypeByMetadataName("MUI.UGUI.MUISettings") != null)
            {
                AppendPageDefinitionFactory(context, model, output, modelName, argsName, resultName,
                    presenterBase, requirePresenterFactory, policyPreset);
            }
            if (automaticArgs) AppendOpenEntryCalls(output, model, presenter, argsName, resultName);
            output.Append("    }\n");
            GeneratePageFacade(context, model, presenter, args, result, policyPreset, requirePresenterFactory, automaticArgs);
            context.AddSource(Identifier(model.ToDisplayString()) + ".PageDefinition.g.cs", SourceText.From(WrapTypeScope(model, output), Encoding.UTF8));
        }

        private static void AppendPageDefinitionFactory(GenerationContext context, INamedTypeSymbol model,
            StringBuilder output, string modelName, string argsName, string resultName,
            string presenterBase, bool requirePresenterFactory, string policyPreset)
        {
            var canConstructModel = model.InstanceConstructors.Any(ctor => ctor.Parameters.Length == 0 &&
                context.Compilation.IsSymbolAccessibleWithin(ctor, context.Compilation.Assembly));
            var modelParameter = "            global::System.Func<" + modelName + "> modelFactory";
            var presenterParameter = "            global::System.Func<" + modelName + ", " + presenterBase + "> presenterFactory";
            output.Append("\n        /// <summary>创建页面页面定义定义，按声明预设解析策略；实例由页面管理器管理。</summary>\n")
                .Append("        public static global::MUI.Pages.PageDefinition<").Append(modelName).Append(", ").Append(argsName).Append(", ").Append(resultName).Append("> CreateDefinition(\n");
            // 有业务依赖的类型必须显式传入工厂；普通页面可以直接 CreateDefinition()。
            if (!canConstructModel) output.Append(modelParameter).Append(",\n");
            if (requirePresenterFactory) output.Append(presenterParameter).Append(",\n");
            output.Append("            global::MUI.UGUI.MUISettings settings = null,\n")
                .Append("            global::MUI.Pages.PagePolicyOverrides overrides = null,\n");
            if (canConstructModel) output.Append(modelParameter).Append(" = null,\n");
            if (!requirePresenterFactory) output.Append(presenterParameter).Append(" = null,\n");
            output.Append("            global::System.Func<").Append(argsName).Append(", ").Append(argsName).Append(", bool> argsEqual = null,\n")
                .Append("            global::System.Collections.Generic.IReadOnlyList<global::MUI.Pages.PageDependency<").Append(argsName).Append(">> dependencies = null,\n")
                .Append("            string key = null, global::MUI.Resources.ViewResource resource = null,\n")
                .Append("            long? estimatedRetainedBytes = null)\n        {\n");
            if (canConstructModel)
            {
                output.Append("            if (modelFactory == null)\n            {\n                modelFactory = () => new ").Append(modelName).Append("();\n            }\n");
            }
            output.Append("            return Create(modelFactory, presenterFactory,\n")
                .Append("                policy: settings == null\n")
                .Append("                    ? global::MUI.UGUI.MUISettings.ResolveGlobalPolicy(").Append(policyPreset == null ? "null" : Literal(policyPreset)).Append(", overrides: overrides)\n")
                .Append("                    : settings.ResolvePolicy(").Append(policyPreset == null ? "null" : Literal(policyPreset)).Append(", overrides: overrides),\n")
                .Append("                argsEqual: argsEqual, dependencies: dependencies, key: key, resource: resource,\n")
                .Append("                estimatedRetainedBytes: estimatedRetainedBytes);\n        }\n");
        }

        private static bool IsPageType(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol array)
            {
                return IsPageType(array.ElementType);
            }

            return type is INamedTypeSymbol named && named.TypeKind != TypeKind.Error &&
                named.SpecialType != SpecialType.System_Void && !named.IsStatic &&
                !named.IsUnboundGenericType && !named.IsRefLikeType &&
                named.TypeArguments.All(IsPageType) &&
                HasClosedContainingTypes(named);
        }

        // 外层 static class 可以包含合法参数类型；只检查外层泛型是否全部闭合。
        private static bool HasClosedContainingTypes(INamedTypeSymbol type)
        {
            for (var parent = type.ContainingType; parent != null; parent = parent.ContainingType)
            {
                if (parent.IsUnboundGenericType || !parent.TypeArguments.All(IsPageType))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsPublicPageType(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol array)
            {
                return IsPublicPageType(array.ElementType);
            }

            return type is INamedTypeSymbol named && named.DeclaredAccessibility == Accessibility.Public &&
                (named.ContainingType == null || IsPublicPageType(named.ContainingType)) &&
                named.TypeArguments.All(IsPublicPageType);
        }

        private static bool IsCompatiblePresenter(INamedTypeSymbol type, INamedTypeSymbol model,
            ITypeSymbol args, ITypeSymbol result)
        {
            if (type == null || type.IsAbstract || type.IsStatic || !IsPageType(type))
            {
                return false;
            }

            for (var parent = type.BaseType; parent != null; parent = parent.BaseType)
            {
                if (parent.OriginalDefinition.ToDisplayString() != "MUI.Presenter<TViewModel, TArgs, TResult>")
                {
                    continue;
                }

                return SymbolEqualityComparer.Default.Equals(parent.TypeArguments[0], model) &&
                    SymbolEqualityComparer.Default.Equals(parent.TypeArguments[1], args) &&
                    SymbolEqualityComparer.Default.Equals(parent.TypeArguments[2], result);
            }
            return false;
        }
    }
}
