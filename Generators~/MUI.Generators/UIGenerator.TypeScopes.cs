using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MUI.Generators
{
    public sealed partial class UIGenerator
    {
        /// <summary>嵌套生成代码沿用原有类型作用域，不把同名模型压平到命名空间。</summary>
        private static void ValidateTypeScope(GenerationContext context, INamedTypeSymbol model)
        {
            if ((model.DeclaredAccessibility != Accessibility.Public &&
                model.DeclaredAccessibility != Accessibility.Internal) ||
                !context.Compilation.IsSymbolAccessibleWithin(model, context.Compilation.Assembly))
            {
                Fail(model, "生成模型必须为程序集可访问的 public 或 internal 类型。");
            }

            foreach (var parameter in model.TypeParameters)
            {
                if (parameter.Name == model.Name + "BindingContext" || parameter.Name == model.Name + "BindingFactory" ||
                    parameter.Name == model.Name + "Route" || parameter.Name == "Manifest" || parameter.Name == "ResourceKey" ||
                    parameter.Name == "Create" || parameter.Name == "Register" || parameter.Name == "Key" || parameter.Name == "Resource")
                {
                    Fail(parameter, "类型参数名称与生成工厂的类型或成员冲突，请为该类型参数换名。");
                }
            }

            for (var parent = model.ContainingType; parent != null; parent = parent.ContainingType)
            {
                if (parent.TypeParameters.Length != 0 || parent.TypeKind != TypeKind.Class || parent.IsRecord)
                {
                    Fail(model, "嵌套模型的外层必须是非泛型 partial class。");
                }

                foreach (var reference in parent.DeclaringSyntaxReferences)
                {
                    if (!(reference.GetSyntax() is ClassDeclarationSyntax declaration) ||
                        !declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
                    {
                        Fail(parent, "包含生成模型的每一层 class 声明都必须包含 partial。");
                    }
                }
            }
        }

        private static string TypeParameters(INamedTypeSymbol model) => model.Arity == 0 ? "" :
            "<" + string.Join(", ", model.TypeParameters.Select(parameter => Escape(parameter.Name))) + ">";

        /// <summary>模型与生成类型保持相同约束；使用语义类型全名，避免依赖项目 using 或别名。</summary>
        private static string TypeConstraints(INamedTypeSymbol model)
        {
            var output = new StringBuilder();
            foreach (var parameter in model.TypeParameters)
            {
                var constraints = new List<string>();
                if (parameter.HasUnmanagedTypeConstraint)
                {
                    constraints.Add("unmanaged");
                }
                else if (parameter.HasValueTypeConstraint)
                {
                    constraints.Add("struct");
                }
                else if (parameter.HasReferenceTypeConstraint)
                {
                    constraints.Add(parameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated ? "class?" : "class");
                }
                else if (parameter.HasNotNullConstraint)
                {
                    constraints.Add("notnull");
                }

                foreach (var constraint in parameter.ConstraintTypes)
                {
                    constraints.Add(constraint.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
                        SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier)));
                }

                if (parameter.HasConstructorConstraint && !parameter.HasValueTypeConstraint && !parameter.HasUnmanagedTypeConstraint)
                {
                    constraints.Add("new()");
                }

                if (constraints.Count != 0)
                {
                    output.Append("\n        where ").Append(Escape(parameter.Name)).Append(" : ").Append(string.Join(", ", constraints));
                }
            }
            return output.ToString();
        }

        private static bool HasSiblingType(INamedTypeSymbol model, string name) =>
            model.ContainingType == null
                ? model.ContainingNamespace.GetTypeMembers(name, model.Arity).Length != 0
                : model.ContainingType.GetTypeMembers(name, model.Arity).Length != 0;

        private static string FactoryQualifiedName(INamedTypeSymbol model, string name) =>
            model.ContainingType != null ? TypeName(model.ContainingType) + "." + name :
            "global::" + (model.ContainingNamespace.IsGlobalNamespace ? "" :
                model.ContainingNamespace.ToDisplayString() + ".") + name;

        /// <summary>模型、绑定和路由使用相同外层声明，使访问规则及生成类型位置保持一致。</summary>
        private static string WrapTypeScope(INamedTypeSymbol model, StringBuilder body)
        {
            var parents = new Stack<INamedTypeSymbol>();
            for (var parent = model.ContainingType; parent != null; parent = parent.ContainingType)
            {
                parents.Push(parent);
            }

            var result = new StringBuilder("// <auto-generated/>\n#nullable enable annotations\n");
            var hasNamespace = !model.ContainingNamespace.IsGlobalNamespace;
            if (hasNamespace)
            {
                result.Append("namespace ").Append(model.ContainingNamespace.ToDisplayString()).Append("\n{\n");
            }

            var depth = 0;
            foreach (var parent in parents)
            {
                var indent = new string(' ', (depth + 1) * 4);
                // 可见性和 static 等修饰符沿用另一部分声明，不重复改变外层类型的契约。
                result.Append(indent).Append("partial class ").Append(Escape(parent.Name)).Append("\n")
                    .Append(indent).Append("{\n");
                ++depth;
            }
            var bodyIndent = new string(' ', depth * 4);
            foreach (var line in body.ToString().TrimEnd('\n').Split('\n'))
            {
                result.Append(line.Length == 0 ? "" : bodyIndent + line).Append('\n');
            }

            while (depth > 0)
            {
                result.Append(new string(' ', depth * 4)).Append("}\n");
                --depth;
            }
            if (hasNamespace)
            {
                result.Append("}\n");
            }

            return result.ToString();
        }
    }
}
