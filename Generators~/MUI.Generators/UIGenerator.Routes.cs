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
        /// <summary>生成路由的类型接线，不生成服务构造、资源加载或运行时类型扫描。</summary>
        private static void GenerateRoute(GenerationContext context, INamedTypeSymbol model,
            IEnumerable<INamedTypeSymbol> candidates)
        {
            var declaration = Attribute(model, "MUI.Navigation.ViewRouteAttribute");
            if (declaration == null)
            {
                return;
            }

            if (model.IsAbstract)
            {
                Fail(model, "ViewRoute 必须声明在具体模型上。");
            }

            var contract = Attribute(model, "MUI.ViewContractAttribute");
            if (contract == null)
            {
                Fail(model, "ViewRoute 需要模型自身声明非空 ViewContract 资源键。");
            }

            var args = declaration.ConstructorArguments[0].Value as ITypeSymbol;
            var result = declaration.ConstructorArguments[1].Value as ITypeSymbol;
            if (!IsRouteType(args) || !IsRouteType(result))
            {
                Fail(model, "路由参数及结果必须是可用作泛型参数的非开放类型。");
            }

            if (!context.Compilation.IsSymbolAccessibleWithin(args, context.Compilation.Assembly) ||
                !context.Compilation.IsSymbolAccessibleWithin(result, context.Compilation.Assembly))
            {
                Fail(model, "路由参数及结果必须能从生成工厂访问。");
            }

            var key = model.ToDisplayString();
            var explicitKey = false;
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
            }
            if (string.IsNullOrWhiteSpace(key))
            {
                Fail(model, "ViewRoute 路由键不能为空。");
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
                    Fail(model, "存在多个兼容 Presenter，请通过 ViewRoute.PresenterType 明确选择。");
                }

                if (compatible.Length == 1)
                {
                    presenter = (INamedTypeSymbol)compatible[0];
                }
            }

            var routeName = model.Name + "Route";
            if (HasSiblingType(model, routeName))
            {
                Fail(model, "生成路由工厂与现有类型重名：" + routeName);
            }

            var publicApi = model.DeclaredAccessibility == Accessibility.Public;
            if (publicApi && (!IsPublicRouteType(args) || !IsPublicRouteType(result)))
            {
                Fail(model, "公共模型生成的路由要求参数及结果类型公开可访问。");
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
            output.Append("    /// <summary>生成的类型化路由接线；模型及业务依赖由项目工厂创建。</summary>\n    ")
                .Append(publicApi ? "public" : "internal").Append(" static class ").Append(routeName).Append(typeParameters).Append(TypeConstraints(model)).Append("\n    {\n")
                .Append(model.Arity == 0 ? "        public const string Key = " : "        public static readonly string Key = ").Append(keyExpression).Append(";\n")
                .Append("        public static global::MUI.Resources.ViewResource Resource\n        {\n            get;\n        } = new global::MUI.Resources.ViewResource(")
                .Append(model.Name).Append("BindingFactory").Append(typeParameters).Append(".ResourceKey);\n\n")
                .Append("        public static global::MUI.Navigation.Route<").Append(modelName).Append(", ").Append(argsName).Append(", ").Append(resultName).Append("> Create(\n")
                .Append("            global::System.Func<").Append(modelName).Append("> modelFactory,\n")
                .Append("            global::System.Func<").Append(modelName).Append(", ").Append(presenterBase).Append("> presenterFactory")
                .Append(requirePresenterFactory ? ",\n" : " = null,\n")
                .Append("            global::MUI.Navigation.RoutePolicy policy = null,\n")
                .Append("            global::System.Func<").Append(argsName).Append(", ").Append(argsName).Append(", bool> argsEqual = null,\n")
                .Append("            global::System.Collections.Generic.IReadOnlyList<global::MUI.Navigation.RouteDependency<").Append(argsName).Append(">> dependencies = null,\n")
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

            output.Append("            return new global::MUI.Navigation.Route<").Append(modelName).Append(", ").Append(argsName).Append(", ").Append(resultName).Append(">(\n")
                .Append("                key ?? Key, resource ?? Resource, modelFactory, presenterFactory, ").Append(model.Name).Append("BindingFactory").Append(typeParameters).Append(".Create,\n")
                .Append("                policy: policy, argsEqual: argsEqual, estimatedRetainedBytes: estimatedRetainedBytes")
                .Append(", dependencies: dependencies);\n        }\n    }\n");
            context.AddSource(Identifier(model.ToDisplayString()) + ".Route.g.cs", SourceText.From(WrapTypeScope(model, output), Encoding.UTF8));
        }

        private static bool IsRouteType(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol array)
            {
                return IsRouteType(array.ElementType);
            }

            return type is INamedTypeSymbol named && named.TypeKind != TypeKind.Error &&
                named.SpecialType != SpecialType.System_Void && !named.IsStatic &&
                !named.IsUnboundGenericType && !named.IsRefLikeType &&
                named.TypeArguments.All(IsRouteType) &&
                HasClosedContainingTypes(named);
        }

        // 外层 static class 可以包含合法参数类型；只检查外层泛型是否全部闭合。
        private static bool HasClosedContainingTypes(INamedTypeSymbol type)
        {
            for (var parent = type.ContainingType; parent != null; parent = parent.ContainingType)
            {
                if (parent.IsUnboundGenericType || !parent.TypeArguments.All(IsRouteType))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsPublicRouteType(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol array)
            {
                return IsPublicRouteType(array.ElementType);
            }

            return type is INamedTypeSymbol named && named.DeclaredAccessibility == Accessibility.Public &&
                (named.ContainingType == null || IsPublicRouteType(named.ContainingType)) &&
                named.TypeArguments.All(IsPublicRouteType);
        }

        private static bool IsCompatiblePresenter(INamedTypeSymbol type, INamedTypeSymbol model,
            ITypeSymbol args, ITypeSymbol result)
        {
            if (type == null || type.IsAbstract || type.IsStatic || !IsRouteType(type))
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
