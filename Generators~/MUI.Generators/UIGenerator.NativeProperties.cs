using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace MUI.Generators
{
    public sealed partial class UIGenerator
    {
        private static string NativeAdapterName(INamedTypeSymbol component, string property) => "MUI.GeneratedNativeProperties." + NativeIdentifier(component.ToDisplayString() + "." + property);

        private static string NativeIdentifier(string value)
        {
            using (var hash = System.Security.Cryptography.SHA256.Create())
                return "Native_" + string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(b => b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
        }

        // Both Prefab declarations and code bindings use the same access and notification semantics.
        private static string BuildNativePropertyAdapter(GenerationContext context, ISymbol source, INamedTypeSymbol component,
            ISymbol nativeMember, string adapterName, string signalName, string setterName)
        {
            var property = nativeMember as IPropertySymbol;
            var field = nativeMember as IFieldSymbol;
            var name = nativeMember.Name;
            var valueType = property != null ? property.Type : field.Type;
            var readable = property == null || Accessible(property.GetMethod);
            var writable = property != null ? Accessible(property.SetMethod) : !field.IsReadOnly && !field.IsConst;
            var listen = "";
            if (signalName != null)
            {
                var signalProperty = FindElementMember<IPropertySymbol>(component, signalName);
                var signalField = FindElementMember<IFieldSymbol>(component, signalName);
                var signalType = signalProperty != null && !signalProperty.IsStatic && !signalProperty.IsIndexer && Accessible(signalProperty.GetMethod)
                    ? signalProperty.Type : signalField != null && !signalField.IsStatic && signalField.DeclaredAccessibility == Accessibility.Public ? signalField.Type : null;
                var arity = -1;
                for (var current = signalType as INamedTypeSymbol; current != null; current = current.BaseType)
                    if (current.ContainingNamespace.ToDisplayString() == "UnityEngine.Events" && current.Name == "UnityEvent" && current.Arity <= 1)
                    { arity = current.Arity; break; }
                if (arity < 0) Fail(source, "Notification must be a public UnityEvent with zero or one argument: " + signalName);
                listen = "base.Listen(base.Component." + Escape(signalName) + ", " + (arity == 0 ? "()" : "_") +
                    " => { if (!writing) base.Changed(" + Literal(name) + "); });";
            }
            var write = "control." + Escape(name) + " = value;";
            if (!string.IsNullOrEmpty(setterName))
            {
                var valid = false;
                for (var current = component; current != null; current = current.BaseType)
                    if (current.GetMembers(setterName).OfType<IMethodSymbol>().Any(m => !m.IsStatic && !m.IsGenericMethod && m.DeclaredAccessibility == Accessibility.Public && m.ReturnsVoid && m.Parameters.Length == 1 && m.Parameters[0].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, valueType)))
                        valid = true;
                if (!valid) Fail(source, "Invalid notification setter: " + setterName);
                write = "control." + Escape(setterName) + "(value);";
            }
            var body = new StringBuilder("internal sealed class ").Append(adapterName)
                .Append(" : global::MUI.UGUI.GeneratedPropertyAdapter<").Append(TypeName(component)).Append("> {\n")
                .Append(signalName == null ? "" : "private bool writing;\n")
                .Append("public ").Append(adapterName).Append("(global::MUI.UGUI.NativeControl owner) : base(owner) {").Append(listen).Append("}\n")
                .Append("public ").Append(TypeName(valueType)).Append(' ').Append(Escape(name)).Append(" {\n");
            if (readable) body.Append("get => base.Component.").Append(Escape(name)).Append(";\n");
            if (writable)
            {
                body.Append("set { var control = base.Component;\n");
                if (readable) body.Append("if (global::System.Collections.Generic.EqualityComparer<").Append(TypeName(valueType)).Append(">.Default.Equals(control.").Append(Escape(name)).Append(", value)) return;\n");
                if (signalName != null) body.Append("writing = true; try {\n");
                body.Append(write).Append('\n');
                if (signalName != null) body.Append("} finally { writing = false; }\n");
                // Raw setters may change other properties (e.g. Slider.minValue clips value).
                body.Append("if (base.Owner.IsAlive && control != null) base.Changed(string.Empty);\n}\n");
            }
            body.Append("}\n}\n");
            return body.ToString();
        }

        private static GenerationContext AddNativeProperties(GenerationContext context, ref ImmutableArray<INamedTypeSymbol> candidates)
        {
            var compilation = context.Compilation;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var type in candidates.Where(t => t != null).Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default))
            foreach (var member in type.GetMembers())
            foreach (var attribute in member.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == "MUI.BindAttribute"))
            {
                var component = ResolveElement(context, attribute);
                if (component == null || !DerivesFrom(component, "UnityEngine.Component")) continue;
                // Element bindings retain their own semantics; only raw Unity components use generated access.
                if (DerivesFrom(component, "MUI.UGUI.Element")) continue;
                try
                {
                    var name = attribute.ConstructorArguments[1].Value as string;
                    var property = FindElementMember<IPropertySymbol>(component, name);
                    var field = FindElementMember<IFieldSymbol>(component, name);
                    var nativeMember = (ISymbol)property ?? field;
                    var readable = property != null ? !property.IsIndexer && Accessible(property.GetMethod) : field != null && field.DeclaredAccessibility == Accessibility.Public;
                    if (nativeMember == null || nativeMember.IsStatic || !readable || !context.Compilation.IsSymbolAccessibleWithin(component, context.Compilation.Assembly) || nativeMember.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "System.ObsoleteAttribute"))
                        Fail(member, "Native binding requires a public, non-obsolete instance property or field: " + name);
                    var qualified = NativeAdapterName(component, name);
                    if (!seen.Add(qualified)) continue;
                    var adapter = qualified.Substring(qualified.LastIndexOf('.') + 1);
                    var notified = TryBuiltInNotification(component, name, out var setter);
                    var body = "// <auto-generated/>\nnamespace MUI.GeneratedNativeProperties {\n" +
                        BuildNativePropertyAdapter(context, member, component, nativeMember, adapter, notified ? "onValueChanged" : null, setter) + "}\n";
                    var hint = adapter + ".NativeProperty.g.cs";
                    var code = context.AddSource(hint, SourceText.From(body.ToString(), Encoding.UTF8));
                    compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(code, (CSharpParseOptions)compilation.SyntaxTrees.First().Options, hint, Encoding.UTF8));
                }
                catch (ContractException error) { context.ReportDiagnostic(Diagnostic.Create(InvalidContract, error.Location, error.Message)); }
            }
            return RefreshCandidates(context, compilation, ref candidates);
        }
    }
}
