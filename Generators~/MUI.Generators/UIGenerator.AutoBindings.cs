using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace MUI.Generators
{
    public sealed partial class UIGenerator
    {
        // Unity does not pass Prefabs as AdditionalFiles. Resolve Assets from compilation source
        // paths (never from a developer-specific absolute path). The editor requests compilation
        // after saving metadata; player builds read the same committed Prefabs.
#pragma warning disable RS1035
        private static GenerationContext AddPrefabBindings(GenerationContext context, ref ImmutableArray<INamedTypeSymbol> candidates)
        {
            var roots = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tree in context.Compilation.SyntaxTrees)
            {
                if (string.IsNullOrEmpty(tree.FilePath)) continue;
                var path = Path.GetFullPath(tree.FilePath).Replace('\\', '/');
                var index = path.IndexOf("/Assets/", StringComparison.Ordinal);
                if (index < 0) index = path.IndexOf("/Packages/", StringComparison.Ordinal);
                if (index >= 0 && Directory.Exists(path.Substring(0, index) + "/Assets")) roots.Add(path.Substring(0, index) + "/Assets");
            }
            var definitions = new Dictionary<string, XElement>(StringComparer.Ordinal);
            foreach (var root in roots.OrderBy(value => value, StringComparer.Ordinal))
            foreach (var path in Directory.EnumerateFiles(root, "*.prefab", SearchOption.AllDirectories).OrderBy(value => value, StringComparer.Ordinal))
            {
                try
                {
                    foreach (var line in File.ReadLines(path))
                    {
                        const string marker = "muiAutoBindings: ";
                        var text = line.Trim();
                        if (!text.StartsWith(marker, StringComparison.Ordinal)) continue;
                        var data = text.Substring(marker.Length).Trim().Trim('"', '\'');
                        if (data.Length == 0) continue;
                        var metadata = XElement.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(data)));
                        if ((string)metadata.Attribute("assembly") != context.Compilation.AssemblyName) continue;
                        var model = (string)metadata.Attribute("model");
                        if (string.IsNullOrEmpty(model)) throw new InvalidOperationException("Missing ViewModel type.");
                        if (definitions.ContainsKey(model)) throw new InvalidOperationException("Multiple Prefabs generate bindings for " + model);
                        definitions.Add(model, metadata);
                    }
                }
                catch (Exception error) when (error is IOException || error is FormatException || error is System.Xml.XmlException || error is InvalidOperationException)
                {
                    context.ReportDiagnostic(Diagnostic.Create(InvalidContract, Location.None, path + ": " + error.Message));
                }
            }
            var compilation = context.Compilation;
            foreach (var pair in definitions)
            {
                if (!pair.Value.Elements("binding").Any()) continue;
                var type = compilation.GetTypeByMetadataName(pair.Key);
                if (type == null) continue;
                try
                {
                    if (type.Arity != 0 || type.ContainingType != null) Fail(type, "Automatic Prefab bindings require a non-nested, non-generic ViewModel.");
                    var names = CollectReservedMemberNames(context.WithCompilation(compilation), type);
                    var members = CollectBindingMembers(type).ToArray();
                    var presenter = AutoBindingPresenter(context.Compilation.GetTypeByMetadataName(pair.Key), candidates);
                    var authoredMembers = presenter == null ? members : members.Concat(CollectBindingMembers(presenter)).ToArray();
                    foreach (var member in members)
                    {
                        var observable = Attribute(member, "MUI.ObservablePropertyAttribute");
                        if (member is IFieldSymbol field && observable != null) names.Add(ObservablePropertyName(field, observable));
                        var commandAttribute = Attribute(member, "MUI.CommandAttribute");
                        if (member is IMethodSymbol method && commandAttribute != null) names.Add(CommandPropertyName(method, commandAttribute));
                    }
                    var body = new StringBuilder();
                    var adapters = new StringBuilder();
                    var writes = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var entry in pair.Value.Elements("binding"))
                    {
                        var name = (string)entry.Attribute("name");
                        var property = (string)entry.Attribute("property");
                        var contractName = (string)entry.Attribute("contract");
                        var command = (string)entry.Attribute("command") == "true";
                        // An authored binding owns this target member, not the whole control.
                        if (authoredMembers.Any(member => member.GetAttributes().Any(attribute =>
                            (attribute.AttributeClass?.ToDisplayString() == "MUI.BindAttribute" || attribute.AttributeClass?.ToDisplayString() == "MUI.BindCommandAttribute") &&
                            attribute.ConstructorArguments.Length > 1 && attribute.ConstructorArguments[0].Value as string == name && CanonicalAutoProperty(attribute.ConstructorArguments[1].Value as string) == CanonicalAutoProperty(property)))) continue;
                        if (!writes.Add(name + "\0" + CanonicalAutoProperty(property))) Fail(type, "Duplicate automatic target binding: " + name + "." + property);
                        var sourceName = (string)entry.Attribute("source") ?? (command ? name + "Command" : name);
                        if (!SyntaxFacts.IsValidIdentifier(name) || !SyntaxFacts.IsValidIdentifier(sourceName) || sourceName == type.Name || !names.Add(sourceName)) Fail(type, "Automatic binding name conflicts: " + sourceName);
                        var contract = compilation.GetTypeByMetadataName(contractName);
                        if (contract == null) Fail(type, "Binding adapter contract unavailable: " + contractName);
                        var componentName = (string)entry.Attribute("component");
                        var componentType = componentName == null ? null : compilation.GetTypeByMetadataName(componentName);
                        if (componentName != null && componentType == null) Fail(type, "Native component type unavailable: " + componentName);
                        var extensionName = (string)entry.Attribute("extension");
                        var extension = extensionName == null ? null : compilation.GetTypeByMetadataName(extensionName);
                        if (extensionName != null && (extension == null || extension.IsAbstract || extension.ContainingType != null || extension.Arity != 0 ||
                            !extension.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "MUI.UGUI.ControlPropertyExtensionAttribute") ||
                            !extension.InstanceConstructors.Any(c => c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length == 1 && c.Parameters[0].Type.ToDisplayString() == "MUI.UGUI.NativeControl")))
                            Fail(type, "Invalid property extension: " + extensionName);
                        var target = FindElementMember<IPropertySymbol>(extension ?? componentType ?? contract, property);
                        var extensionNotifies = extension != null && target != null && target.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "MUI.UGUI.NotifiesChangeAttribute");
                        if (!command && target != null && (target.SetMethod == null || !Accessible(target.SetMethod)))
                        {
                            if (!Accessible(target.GetMethod) || target.IsStatic || target.IsIndexer || target.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "System.ObsoleteAttribute"))
                                Fail(type, "Property cannot be exposed for read access: " + property);
                            var readField = "__muiRead_" + sourceName;
                            if (!names.Add(readField)) Fail(type, "Read accessor field conflicts: " + readField);
                            body.Append("internal global::System.Func<").Append(TypeName(target.Type)).Append("> ").Append(readField).Append(";\n")
                                .Append("[global::MUI.Bind(").Append(Literal(name)).Append(", ").Append(Literal(property)).Append(", ElementType = typeof(").Append(TypeName(contract)).Append(")")
                                .Append(componentType == null ? "" : ", ComponentType = typeof(" + TypeName(componentType) + ")")
                                .Append(extension == null ? "" : ", PropertyAdapterType = typeof(" + TypeName(extension) + ")")
                                .Append(", ReadAccessorField = ").Append(Literal(readField)).Append(")]\npublic ").Append(TypeName(target.Type)).Append(' ').Append(Escape(sourceName))
                                .Append(" => ").Append(readField).Append(" == null ? throw new global::System.InvalidOperationException(\"View read access requires an active binding session.\") : ").Append(readField).Append("();\n");
                            continue;
                        }
                        if (command)
                        {
                            if (FindElementMember<IEventSymbol>(contract, property) == null) Fail(type, "Binding event unavailable: " + property);
                            var hook = "On" + (sourceName.EndsWith("Command", StringComparison.Ordinal) ? sourceName.Substring(0, sourceName.Length - 7) : sourceName);
                            var method = "__muiAutoInvoke" + sourceName;
                            if (!names.Add(method)) Fail(type, "Automatic command conflicts: " + method);
                            var hookMembers = type.GetMembers(hook);
                            if (hookMembers.Any(m => !(m is IMethodSymbol h) || h.PartialDefinitionPart == null && !h.IsPartialDefinition && !h.DeclaringSyntaxReferences.Any(r => r.GetSyntax() is Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax d && d.Modifiers.Any(SyntaxKind.PartialKeyword))))
                                Fail(type, "Automatic command hook conflicts: " + hook);
                            body.Append("[global::MUI.Command(").Append(Literal(sourceName)).Append(")]\n[global::MUI.BindCommand(").Append(Literal(name)).Append(", ").Append(Literal(property)).Append(", ElementType = typeof(").Append(TypeName(contract)).Append("))]\nprivate void ").Append(method).Append("(global::MUI.CommandContext context) => ").Append(Escape(hook)).Append("(context);\npartial void ").Append(Escape(hook)).Append("(global::MUI.CommandContext context);\n");
                        }
                        else
                        {
                            if (target == null || target.SetMethod == null || target.SetMethod.DeclaredAccessibility != Accessibility.Public || target.IsIndexer || target.IsStatic) Fail(type, "Binding property must be readable and writable: " + property);
                            var field = "__muiAuto_" + sourceName;
                            if (!names.Add(field)) Fail(type, "Automatic backing field conflicts: " + field);
                            var mode = (string)entry.Attribute("mode") ?? "OneWay";
                            if (!new[] { "OneWay", "TwoWay", "OneWayToSource", "OneTime" }.Contains(mode)) Fail(type, "Invalid automatic binding direction.");
                            string adapterName = extension == null ? null : TypeName(extension);
                            if (extension != null && !extensionNotifies && mode != "OneWay" && mode != "OneTime")
                                Fail(type, "Extension properties without NotifiesChange support OneWay or OneTime only.");
                            if (componentType != null && extension == null)
                            {
                                var signalName = (string)entry.Attribute("notificationEvent");
                                var setterName = (string)entry.Attribute("notificationSetter");
                                // Built-in event semantics are known without requiring an Inspector save.
                                // Otherwise compilation errors can prevent the Inspector from regenerating metadata.
                                if (signalName == null && TryBuiltInNotification(componentType, property, out var builtInSetter))
                                {
                                    signalName = "onValueChanged";
                                    setterName = builtInSetter;
                                    entry.SetAttributeValue("notificationEvent", signalName);
                                }
                                if (signalName == null && mode != "OneWay" && mode != "OneTime")
                                    Fail(type, "Missing native event mapping for " + componentType.ToDisplayString() + "." + property + " (" + sourceName + "). Configure its UnityEvent mapping and regenerate bindings, or use OneWay/OneTime.");
                                if (target.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "System.ObsoleteAttribute")) Fail(type, "Native property is obsolete: " + property);
                                adapterName = Identifier(pair.Key + "." + sourceName) + "PropertyAdapter";
                                if (compilation.GetTypeByMetadataName((type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString() + ".") + adapterName) != null)
                                    Fail(type, "Generated property adapter name conflicts: " + adapterName);
                                var access = "base.Component." + Escape(property);
                                var listen = "";
                                if (signalName != null)
                                {
                                    var signalProperty = FindElementMember<IPropertySymbol>(componentType, signalName);
                                    var signalField = FindElementMember<IFieldSymbol>(componentType, signalName);
                                    var signalType = signalProperty != null && !signalProperty.IsStatic && !signalProperty.IsIndexer && Accessible(signalProperty.GetMethod)
                                        ? signalProperty.Type : signalField != null && !signalField.IsStatic && signalField.DeclaredAccessibility == Accessibility.Public ? signalField.Type : null;
                                    var arity = -1;
                                    for (var current = signalType as INamedTypeSymbol; current != null; current = current.BaseType)
                                        if (current.ContainingNamespace.ToDisplayString() == "UnityEngine.Events" && current.Name == "UnityEvent" && current.Arity <= 1) { arity = current.Arity; break; }
                                    if (arity < 0) Fail(type, "Notification must be a public UnityEvent with zero or one argument: " + signalName);
                                    listen = "base.Listen(base.Component." + Escape(signalName) + ", " + (arity == 0 ? "()" : "_") + " => base.Changed(" + Literal(property) + "));";
                                }
                                var write = access + " = value;";
                                if (!string.IsNullOrEmpty(setterName))
                                {
                                    var validSetter = false;
                                    for (var current = componentType; current != null; current = current.BaseType)
                                        if (current.GetMembers(setterName).OfType<IMethodSymbol>().Any(m => !m.IsStatic && !m.IsGenericMethod && m.DeclaredAccessibility == Accessibility.Public && m.ReturnsVoid && m.Parameters.Length == 1 && m.Parameters[0].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, target.Type))) validSetter = true;
                                    if (!validSetter) Fail(type, "Invalid notification setter: " + setterName);
                                    write = "base.Component." + Escape(setterName) + "(value);";
                                }
                                adapters.Append("internal sealed class ").Append(adapterName).Append(" : global::MUI.UGUI.GeneratedPropertyAdapter<").Append(TypeName(componentType)).Append("> {\n")
                                    .Append("public ").Append(adapterName).Append("(global::MUI.UGUI.NativeControl owner) : base(owner) {").Append(listen).Append("}\n")
                                    .Append("public ").Append(TypeName(target.Type)).Append(' ').Append(Escape(property)).Append(" {\n");
                                if (target.GetMethod != null && target.GetMethod.DeclaredAccessibility == Accessibility.Public)
                                    adapters.Append("get => ").Append(access).Append(";\n");
                                adapters.Append("set { ");
                                if (Accessible(target.GetMethod)) adapters.Append("if (global::System.Collections.Generic.EqualityComparer<").Append(TypeName(target.Type)).Append(">.Default.Equals(").Append(access).Append(", value)) return;");
                                adapters.Append(write).Append(" base.Changed(").Append(Literal(property)).Append("); }\n}\n}\n");
                            }
                            body.Append("private ").Append(TypeName(target.Type)).Append(' ').Append(field).Append(InitialBindingValue(entry, target.Type)).Append(";\n[global::MUI.Bind(").Append(Literal(name)).Append(", ").Append(Literal(property)).Append(", bindingMode: global::MUI.BindingMode.").Append(mode).Append(", ElementType = typeof(").Append(TypeName(contract)).Append(")").Append(componentType == null ? "" : ", ComponentType = typeof(" + TypeName(componentType) + ")").Append(adapterName == null ? "" : ", PropertyAdapterType = typeof(" + adapterName + ")" + (entry.Attribute("notificationEvent") == null && !extensionNotifies ? "" : ", NativeNotifications = true")).Append(")]\npublic ").Append(TypeName(target.Type)).Append(' ').Append(Escape(sourceName)).Append(" { get => ").Append(field).Append("; set => SetProperty(ref ").Append(field).Append(", value, ").Append(Literal(sourceName)).Append("); }\n");
                        }
                    }
                    if (body.Length == 0) continue;
                    var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
                    var code = "// <auto-generated/>\n" + (ns == null ? "" : "namespace " + ns + " {\n") +
                        (type.DeclaredAccessibility == Accessibility.Public ? "public" : "internal") + " partial class " + Escape(type.Name) + " {\n" + body + "}\n" + adapters + (ns == null ? "" : "}\n");
                    var hint = Identifier(compilation.AssemblyName) + "." + Identifier(pair.Key) + ".AutoBindings.g.cs";
                    code = context.AddSource(hint, SourceText.From(code, Encoding.UTF8));
                    compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(code, (CSharpParseOptions)compilation.SyntaxTrees.First().Options, hint, Encoding.UTF8));
                }
                catch (ContractException error) { context.ReportDiagnostic(Diagnostic.Create(InvalidContract, error.Location, error.Message)); }
            }
            if (compilation == context.Compilation) return context;
            candidates = compilation.SyntaxTrees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().Select(node => compilation.GetSemanticModel(tree).GetDeclaredSymbol(node) as INamedTypeSymbol)).Where(type => type != null).ToImmutableArray();
            return context.WithCompilation(compilation);
        }
        private static bool TryBuiltInNotification(INamedTypeSymbol component, string property, out string setter)
        {
            setter = null;
            for (var type = component; type != null; type = type.BaseType)
            {
                string expected = null;
                switch (type.ToDisplayString())
                {
                    case "UnityEngine.UI.Slider":
                    case "UnityEngine.UI.Scrollbar":
                    case "UnityEngine.UI.Dropdown":
                    case "TMPro.TMP_Dropdown": expected = "value"; setter = "SetValueWithoutNotify"; break;
                    case "UnityEngine.UI.Toggle": expected = "isOn"; setter = "SetIsOnWithoutNotify"; break;
                    case "UnityEngine.UI.InputField":
                    case "TMPro.TMP_InputField": expected = "text"; setter = "SetTextWithoutNotify"; break;
                    case "UnityEngine.UI.ScrollRect": expected = "normalizedPosition"; setter = null; break;
                }
                if (expected != null && expected == property) return true;
            }
            setter = null;
            return false;
        }

        private static string CanonicalAutoProperty(string value) => value;

        private static string InitialBindingValue(XElement entry, ITypeSymbol type)
        {
            var initial = (string)entry.Attribute("initial");
            if (type.SpecialType == SpecialType.System_String && initial != null) return " = " + Literal(initial);
            if (type.SpecialType == SpecialType.System_Boolean && bool.TryParse(initial, out var flag)) return flag ? " = true" : " = false";
            if (type.SpecialType == SpecialType.System_Int32 && int.TryParse(initial, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) return " = " + integer.ToString(CultureInfo.InvariantCulture);
            if (type.SpecialType == SpecialType.System_Single && float.TryParse(initial, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return " = " + FloatLiteral(number);
            var name = type.ToDisplayString();
            var components = ((string)entry.Attribute(name == "UnityEngine.Color" ? "initialColor" : "initialVector2"))?.Split(',');
            var count = name == "UnityEngine.Color" ? 4 : name == "UnityEngine.Vector2" ? 2 : 0;
            if (count > 0 && components != null && components.Length == count)
            {
                var values = new List<string>();
                foreach (var component in components)
                {
                    if (!float.TryParse(component, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return "";
                    values.Add(FloatLiteral(value));
                }
                return " = new " + TypeName(type) + "(" + string.Join(", ", values) + ")";
            }
            return "";
        }

        private static string FloatLiteral(float value) => float.IsNaN(value) ? "float.NaN" : float.IsPositiveInfinity(value) ? "float.PositiveInfinity" : float.IsNegativeInfinity(value) ? "float.NegativeInfinity" : value.ToString("R", CultureInfo.InvariantCulture) + "f";
#pragma warning restore RS1035
    }
}
