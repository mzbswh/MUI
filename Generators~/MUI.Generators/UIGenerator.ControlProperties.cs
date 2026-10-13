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
        private static GenerationContext AddControlProperties(GenerationContext context, ref ImmutableArray<INamedTypeSymbol> candidates)
        {
            var compilation = context.Compilation;
            var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var type in candidates)
            {
                if (type == null || !seen.Add(type)) continue;
                var attributes = type.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == "MUI.GenerateControlPropertyAttribute").ToArray();
                if (attributes.Length == 0 && !type.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "MUI.GenerateControlEventAttribute")) continue;
                try
                {
                    ValidateTypeScope(context, type);
                    if (!DerivesFrom(type, "MUI.UGUI.Element") || type.DeclaringSyntaxReferences.Any(r => !(r.GetSyntax() is ClassDeclarationSyntax declaration) || !declaration.Modifiers.Any(SyntaxKind.PartialKeyword)))
                        Fail(type, "GenerateControlProperty requires a partial Element class.");
                    var body = new StringBuilder("partial class ").Append(Escape(type.Name)).Append(TypeParameters(type)).Append(TypeConstraints(type)).Append(" {\n");
                    var names = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var attribute in attributes)
                    {
                        var accessor = attribute.ConstructorArguments[0].Value as string;
                        var nativeName = attribute.ConstructorArguments[1].Value as string;
                        var name = attribute.ConstructorArguments[2].Value as string;
                        if (string.IsNullOrEmpty(name) || !SyntaxFacts.IsValidIdentifier(name) || !names.Add(name) || FindElementMember<ISymbol>(type, name) != null)
                            Fail(type, "Generated control property conflicts or has an invalid name: " + name);
                        if (string.IsNullOrWhiteSpace(accessor) || string.IsNullOrWhiteSpace(nativeName))
                            Fail(type, "Control target and native property names must not be empty.");
                        var owner = FindElementMember<IPropertySymbol>(type, accessor);
                        var component = owner?.Type as INamedTypeSymbol;
                        if (owner == null || owner.IsStatic || owner.IsIndexer || owner.GetMethod == null || !context.Compilation.IsSymbolAccessibleWithin(owner.GetMethod, type) || component == null || !DerivesFrom(component, "UnityEngine.Component"))
                            Fail(type, "Control target must be an accessible Unity component getter: " + accessor);
                        var property = FindElementMember<IPropertySymbol>(component, nativeName);
                        if (property == null || property.IsStatic || property.IsIndexer || !Accessible(property.GetMethod) || !Accessible(property.SetMethod))
                            Fail(type, "Control property must be a public readable and writable instance property: " + nativeName);
                        var setter = attribute.NamedArguments.FirstOrDefault(p => p.Key == "Setter").Value.Value as string;
                        if (setter != null)
                        {
                            var valid = false;
                            for (var current = component; current != null; current = current.BaseType)
                                valid |= current.GetMembers(setter).OfType<IMethodSymbol>().Any(method => !method.IsStatic && !method.IsGenericMethod &&
                                    Accessible(method) && method.ReturnsVoid && method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None &&
                                    SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, property.Type));
                            if (!valid) Fail(type, "Invalid native setter: " + setter);
                        }
                        var valueType = TypeName(property.Type);
                        var notifyAll = attribute.NamedArguments.Any(p => p.Key == "NotifyAll" && p.Value.Value is bool all && all);
                        body.Append("/// <inheritdoc cref=\"").Append(TypeName(component)).Append('.').Append(Escape(nativeName)).Append("\"/>\n")
                            .Append("public ").Append(valueType).Append(' ').Append(Escape(name)).Append(" { get => ").Append(Escape(accessor)).Append('.').Append(Escape(nativeName)).Append("; set {\n")
                            .Append("On").Append(name).Append("Changing(value);\nvar control = ").Append(Escape(accessor)).Append(";\nvar previous = control.").Append(Escape(nativeName)).Append(";\n")
                            .Append("if (global::System.Collections.Generic.EqualityComparer<").Append(valueType).Append(">.Default.Equals(previous, value)) return;\n")
                            .Append("control.").Append(setter == null ? Escape(nativeName) + " = value;" : Escape(setter) + "(value);").Append("\nif (!IsAlive || control == null) return;\n")
                            .Append(notifyAll ? "" : "if (global::System.Collections.Generic.EqualityComparer<" + valueType + ">.Default.Equals(previous, control." + Escape(nativeName) + ")) return;\n")
                            .Append("On").Append(name).Append("Changed();\nif (IsAlive && control != null) NotifyChanged(").Append(Literal(notifyAll ? "" : name)).Append(");\n} }\n")
                            .Append("partial void On").Append(name).Append("Changing(").Append(valueType).Append(" value);\npartial void On").Append(name).Append("Changed();\n");
                    }
                    AppendControlEvents(context, type, body, names);
                    body.Append("}\n");
                    var hint = Identifier(type.ToDisplayString()) + ".ControlProperties.g.cs";
                    var code = context.AddSource(hint, SourceText.From(WrapTypeScope(type, body), Encoding.UTF8));
                    compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(code, (CSharpParseOptions)compilation.SyntaxTrees.First().Options, hint, Encoding.UTF8));
                }
                catch (ContractException error) { context.ReportDiagnostic(Diagnostic.Create(InvalidContract, error.Location, error.Message)); }
            }
            return RefreshCandidates(context, compilation, ref candidates);
        }

        private static GenerationContext RefreshCandidates(GenerationContext context, Compilation compilation, ref ImmutableArray<INamedTypeSymbol> candidates)
        {
            if (compilation == context.Compilation) return context;
            candidates = compilation.SyntaxTrees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Select(node => compilation.GetSemanticModel(tree).GetDeclaredSymbol(node) as INamedTypeSymbol)).Where(type => type != null).ToImmutableArray();
            return context.WithCompilation(compilation);
        }
    }
}
