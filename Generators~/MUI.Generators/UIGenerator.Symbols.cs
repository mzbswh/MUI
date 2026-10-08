using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MUI.Generators
{
    public sealed partial class UIGenerator
    {
        // 所有编译期成员查找集中在此，生成流程只使用已经校验的符号。
        /// <summary>按基类到派生类收集绑定，跨程序集使用生成的公共属性元数据。</summary>
        private static IEnumerable<ISymbol> CollectBindingMembers(INamedTypeSymbol type)
        {
            var hierarchy = new Stack<INamedTypeSymbol>();
            for (var current = type; current != null && current.ToDisplayString() != "MUI.ViewModel";
                current = current.BaseType)
            {
                if (current.DeclaringSyntaxReferences.Length == 0 &&
                    HasSiblingType(current, current.Name + "BindingFactory"))
                {
                    var metadata = Attribute(current, "MUI.GeneratedBindingMetadataAttribute");
                    if (metadata == null || metadata.ConstructorArguments.Length != 1 ||
                        !Equals(metadata.ConstructorArguments[0].Value, 1))
                    {
                        Fail(type, "基类绑定元数据缺失或版本不支持，请使用当前生成器重新编译：" + current.ToDisplayString());
                    }
                }

                hierarchy.Push(current);
            }

            foreach (var current in hierarchy)
            {
                foreach (var member in current.GetMembers())
                {
                    // 引用程序集可能暴露私有符号；生成契约只读取公开属性上的归一化声明。
                    if (current.DeclaringSyntaxReferences.Length == 0 &&
                        Attribute(current, "MUI.GeneratedBindingMetadataAttribute") != null &&
                        !(member is IPropertySymbol))
                    {
                        continue;
                    }

                    if (SymbolEqualityComparer.Default.Equals(current, type) ||
                        Attribute(member, "MUI.BindAttribute") != null ||
                        Attribute(member, "MUI.BindCommandAttribute") != null)
                    {
                        yield return member;
                    }
                }
            }
        }

        /// <summary>绑定按声明类型访问；不允许派生类遮蔽同名通知来源造成歧义。</summary>
        private static void ValidateInheritedSource(INamedTypeSymbol type, ISymbol member, string sourceName)
        {
            for (var current = type; current != null &&
                !SymbolEqualityComparer.Default.Equals(current, member.ContainingType); current = current.BaseType)
            {
                if (current.GetMembers(sourceName).Length != 0)
                {
                    Fail(type, "派生模型不能遮蔽或重写基类绑定来源：" + sourceName +
                        "。请使用不同的属性名，避免同名通知与绑定目标歧义。");
                }
            }
        }

        /// <summary>把私有声明的绑定复制到公共生成属性，显式携带推断出的控件类型。</summary>
        private static void AppendBindingMetadata(GenerationContext context, StringBuilder output,
            ISymbol member, IEnumerable<AttributeData> attributes, bool command)
        {
            foreach (var attribute in attributes)
            {
                var element = ResolveElement(context, attribute);
                if (element == null)
                {
                    Fail(member, "绑定元数据需要明确的控件类型。");
                }

                output.Append(command ? "        [global::MUI.BindCommand(" : "        [global::MUI.Bind(")
                    .Append(Literal(attribute.ConstructorArguments[0].Value as string)).Append(", ")
                    .Append(Literal(attribute.ConstructorArguments[1].Value as string));
                if (!command)
                {
                    var converter = attribute.ConstructorArguments.Length > 2
                        ? attribute.ConstructorArguments[2].Value as ITypeSymbol : null;
                    var mode = attribute.ConstructorArguments.Length > 3 ? (int)attribute.ConstructorArguments[3].Value : 0;
                    output.Append(", ").Append(converter == null ? "null" : "typeof(" + TypeName(converter) + ")")
                        .Append(", (global::MUI.BindingMode)").Append(mode);
                }

                output.Append(", ElementType = typeof(").Append(TypeName(element)).Append(')');
                if (command)
                {
                    foreach (var argument in attribute.NamedArguments)
                    {
                        if (argument.Key == "InteractableProperty")
                        {
                            output.Append(", InteractableProperty = ").Append(Literal(argument.Value.Value as string));
                        }
                    }
                }
                else
                {
                    foreach (var argument in attribute.NamedArguments)
                    {
                        if (argument.Key == "ValidationProperty")
                        {
                            output.Append(", ValidationProperty = ").Append(Literal(argument.Value.Value as string));
                        }
                        else if (argument.Key == "SourcePath")
                        {
                            output.Append(", SourcePath = ").Append(Literal(argument.Value.Value as string));
                        }
                        else if (argument.Key == "NullValue")
                        {
                            output.Append(", NullValue = ").Append(AttributeConstant(argument.Value));
                        }
                    }
                }

                output.Append(")]\n");
            }
        }

        /// <summary>只向编辑器构建写入真实声明位置，避免把开发机路径带入 Player。</summary>
        private static void AppendSourceLocation(StringBuilder output, AttributeData attribute, ISymbol member)
        {
            var location = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                ?? member.Locations.FirstOrDefault(value => value.IsInSource);
            if (location == null || !location.IsInSource)
            {
                return;
            }

            var span = location.GetLineSpan();
            if (string.IsNullOrEmpty(span.Path))
            {
                return;
            }

            output.Append("\n#if UNITY_EDITOR\n                    , ").Append(Literal(span.Path))
                .Append(", ").Append(span.StartLinePosition.Line + 1)
                .Append("\n#endif\n                ");
        }

        /// <summary>生成成员不得无意遮蔽可访问的基类成员，特别是通知与属性赋值基础方法。</summary>
        private static HashSet<string> CollectReservedMemberNames(GenerationContext context, INamedTypeSymbol type)
        {
            var names = new HashSet<string>(type.GetMembers().Select(member => member.Name), StringComparer.Ordinal);
            names.UnionWith(type.TypeParameters.Select(parameter => parameter.Name));
            for (var current = type.BaseType; current != null; current = current.BaseType)
            {
                foreach (var member in current.GetMembers())
                {
                    if (member.CanBeReferencedByName && context.Compilation.IsSymbolAccessibleWithin(member, type))
                    {
                        names.Add(member.Name);
                    }

                    // 同一次编译中，基类将生成的公共成员还不在 Roslyn 输入符号里。
                    // 即使声明字段或方法是 private，生成属性仍会被派生模型继承。
                    var observable = Attribute(member, "MUI.ObservablePropertyAttribute");
                    if (member is IFieldSymbol field && observable != null)
                    {
                        names.Add(ObservablePropertyName(field, observable));
                    }

                    var command = Attribute(member, "MUI.CommandAttribute");
                    if (member is IMethodSymbol method && command != null)
                    {
                        names.Add(CommandPropertyName(method, command));
                    }
                }
            }

            return names;
        }

        /// <summary>绑定上下文是模型的同级类型，控件类型必须满足运行时泛型约束并对它可见。</summary>
        private static void ValidateElementType(GenerationContext context, ISymbol member, INamedTypeSymbol element)
        {
            if (element == null || !element.AllInterfaces.Any(i => i.ToDisplayString() == "MUI.IElement"))
            {
                Fail(member, "The binding target type must implement MUI.IElement.");
            }

            if (!element.IsReferenceType || element.IsUnboundGenericType ||
                !context.Compilation.IsSymbolAccessibleWithin(element, context.Compilation.Assembly))
            {
                Fail(member, "Element must be a closed reference type accessible from the generated BindingContext.");
            }
        }

        /// <summary>优先使用显式 ElementType，否则从 nameof 的类型或成员推断。</summary>
        private static INamedTypeSymbol ResolveElement(GenerationContext context, AttributeData attribute)
        {
            foreach (var named in attribute.NamedArguments)
            {
                if (named.Key == "ElementType")
                {
                    return named.Value.Value as INamedTypeSymbol;
                }
            }

            if (!(attribute.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax syntax) || syntax.ArgumentList == null)
            {
                return null;
            }

            var argument = syntax.ArgumentList.Arguments.FirstOrDefault(a => a.NameColon?.Name.Identifier.ValueText == "propertyName" || a.NameColon?.Name.Identifier.ValueText == "eventName");
            if (argument == null && syntax.ArgumentList.Arguments.Count > 1)
            {
                argument = syntax.ArgumentList.Arguments[1];
            }

            if (!(argument?.Expression is InvocationExpressionSyntax invocation) ||
                invocation.Expression.ToString() != "nameof" || invocation.ArgumentList.Arguments.Count != 1)
            {
                return null;
            }

            var expression = invocation.ArgumentList.Arguments[0].Expression;
            var model = context.Compilation.GetSemanticModel(syntax.SyntaxTree);
            if (expression is MemberAccessExpressionSyntax access && model.GetSymbolInfo(access.Expression).Symbol is INamedTypeSymbol type)
            {
                return type;
            }

            return model.GetSymbolInfo(expression).Symbol?.ContainingType;
        }

        /// <summary>遵守类和接口的最近同名声明；接口分支存在歧义时拒绝隐式选择。</summary>
        private static TMember FindElementMember<TMember>(INamedTypeSymbol type, string name)
            where TMember : class, ISymbol
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var members = current.GetMembers(name);
                if (members.Length != 0)
                {
                    return members.OfType<TMember>().FirstOrDefault();
                }
            }

            if (type.TypeKind == TypeKind.Interface)
            {
                var candidates = new List<ISymbol>();
                foreach (var parent in type.AllInterfaces)
                {
                    candidates.AddRange(parent.GetMembers(name));
                }

                // 更具体的父接口声明遮蔽它的祖先；菱形继承的同一符号只出现一次。
                var nearest = candidates.Where(candidate => !candidates.Any(other =>
                    !SymbolEqualityComparer.Default.Equals(candidate.ContainingType, other.ContainingType) &&
                    other.ContainingType.AllInterfaces.Any(parent =>
                        SymbolEqualityComparer.Default.Equals(parent, candidate.ContainingType))))
                    .Distinct(SymbolEqualityComparer.Default).ToArray();
                return nearest.Length == 1 ? nearest[0] as TMember : null;
            }

            return null;
        }

        private static bool Accessible(IMethodSymbol method) => method != null && !method.IsInitOnly && method.DeclaredAccessibility == Accessibility.Public;

        private static bool DerivesFrom(INamedTypeSymbol type, string name)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (current.ToDisplayString() == name)
                {
                    return true;
                }
            }

            return false;
        }

        private static AttributeData Attribute(ISymbol symbol, string name) => symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == name);

        /// <summary>从命令声明处查找条件成员，沿用 C# 的继承、遮蔽和访问权限规则。</summary>
        private static string ResolveCanExecute(GenerationContext context, INamedTypeSymbol type,
            ISymbol command, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                Fail(command, "CanExecute must name an accessible bool property or parameterless bool method.");
            }

            var declaration = command.DeclaringSyntaxReferences[0];
            var model = context.Compilation.GetSemanticModel(declaration.SyntaxTree);
            var candidates = model.LookupSymbols(declaration.Span.Start, type, name);
            foreach (var candidate in candidates)
            {
                if (candidate.IsStatic || !candidate.CanBeReferencedByName)
                {
                    continue;
                }

                if (candidate is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary &&
                    !method.IsGenericMethod && method.Parameters.Length == 0 &&
                    method.ReturnType.SpecialType == SpecialType.System_Boolean)
                {
                    return "() => this." + Escape(name) + "()";
                }

                // 属性本身可见不代表 getter 可见，例如基类的 public 属性可以有 private getter。
                if (candidate is IPropertySymbol property && !property.IsIndexer &&
                    property.Type.SpecialType == SpecialType.System_Boolean && property.GetMethod != null &&
                    context.Compilation.IsSymbolAccessibleWithin(property.GetMethod, type))
                {
                    return "() => this." + Escape(name);
                }
            }

            Fail(command, "CanExecute must name an accessible bool property or parameterless bool method on this ViewModel or its base types.");
            return null;
        }
    }
}
