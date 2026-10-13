using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Text.RegularExpressions;
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
                if (string.IsNullOrEmpty(tree.FilePath))
                    continue;
                var path = Path.GetFullPath(tree.FilePath).Replace('\\', '/');
                var index = path.IndexOf("/Assets/", StringComparison.Ordinal);
                if (index < 0)
                    index = path.IndexOf("/Packages/", StringComparison.Ordinal);
                if (index >= 0 && Directory.Exists(path.Substring(0, index) + "/Assets"))
                    roots.Add(path.Substring(0, index) + "/Assets");
            }
            var inputs = context.Prefabs.ToDictionary(file => Path.GetFullPath(file.Path), file => file, StringComparer.Ordinal);
            var paths = new HashSet<string>(inputs.Keys, StringComparer.Ordinal);
            foreach (var root in roots)
                paths.UnionWith(Directory.EnumerateFiles(root, "*.prefab", SearchOption.AllDirectories));
            var definitions = new Dictionary<string, XElement>(StringComparer.Ordinal);
            foreach (var path in paths.OrderBy(value => value, StringComparer.Ordinal))
                {
                    try
                    {
                        var document = inputs.TryGetValue(path, out var input) ? input.GetText()?.ToString() ?? "" : File.ReadAllText(path);
                        var section = new StringBuilder();
                        foreach (var line in document.Split('\n'))
                        {
                            if (line.StartsWith("--- !u!", StringComparison.Ordinal)) section.Clear();
                            section.AppendLine(line);
                            const string marker = "muiAutoBindings: ";
                            var text = line.Trim();
                            if (!text.StartsWith(marker, StringComparison.Ordinal))
                                continue;
                            var data = text.Substring(marker.Length).Trim().Trim('"', '\'');
                            if (data.Length == 0)
                                continue;
                            var metadata = XElement.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(data)));
                            if ((string)metadata.Attribute("assembly") != context.Compilation.AssemblyName)
                                continue;
                            var model = (string)metadata.Attribute("model");
                            if (string.IsNullOrEmpty(model))
                                throw new InvalidOperationException("Missing ViewModel type.");
                            if (definitions.ContainsKey(model))
                                throw new InvalidOperationException("Multiple Prefabs generate bindings for " + model);
                            ValidatePrefabReferences(metadata, section.ToString(), document);
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
                var type = compilation.GetTypeByMetadataName(pair.Key);
                if (type == null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(InvalidContract, Location.None, "Prefab ViewModel type unavailable: " + pair.Key));
                    continue;
                }
                try
                {
                    if (type.Arity != 0 || type.ContainingType != null)
                        Fail(type, "Automatic Prefab bindings require a non-nested, non-generic ViewModel.");
                    var names = CollectReservedMemberNames(context.WithCompilation(compilation), type);
                    var members = CollectBindingMembers(type).ToArray();
                    var presenter = AutoBindingPresenter(context.Compilation.GetTypeByMetadataName(pair.Key), candidates);
                    var authoredMembers = presenter == null ? members : members.Concat(CollectBindingMembers(presenter)).ToArray();
                    foreach (var member in members)
                    {
                        var observable = Attribute(member, "MUI.ObservablePropertyAttribute");
                        if (member is IFieldSymbol field && observable != null)
                            names.Add(ObservablePropertyName(field, observable));
                        var commandAttribute = Attribute(member, "MUI.CommandAttribute");
                        if (member is IMethodSymbol method && commandAttribute != null)
                            names.Add(CommandPropertyName(method, commandAttribute));
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
                        var lookupName = (string)entry.Attribute("targetComponent") ?? (string)entry.Attribute("component");
                        var lookupType = lookupName == null ? null : compilation.GetTypeByMetadataName(lookupName);
                        if (lookupName != null && lookupType == null) Fail(type, "Binding component type unavailable: " + lookupName);
                        // An authored binding owns this target member, not the whole control.
                        if (authoredMembers.Any(member => member.GetAttributes().Any(attribute =>
                            (attribute.AttributeClass?.ToDisplayString() == "MUI.BindAttribute" || attribute.AttributeClass?.ToDisplayString() == "MUI.BindCommandAttribute") &&
                            attribute.ConstructorArguments.Length > 1 && attribute.ConstructorArguments[0].Value as string == name && CanonicalAutoProperty(attribute.ConstructorArguments[1].Value as string) == CanonicalAutoProperty(property) && MatchesAutoComponent(context.WithCompilation(compilation), attribute, lookupType, contractName))))
                            continue;
                        if (!writes.Add(name + "\0" + (lookupName ?? contractName) + "\0" + CanonicalAutoProperty(property)))
                            Fail(type, "Duplicate automatic target binding: " + name + "." + property);
                        var sourceName = (string)entry.Attribute("source") ?? (command ? name + "Command" : name);
                        var existingCommand = command ? members.OfType<IMethodSymbol>().FirstOrDefault(method =>
                            Attribute(method, "MUI.CommandAttribute") is AttributeData declaration && CommandPropertyName(method, declaration) == sourceName) : null;
                        if (!SyntaxFacts.IsValidIdentifier(name) || !SyntaxFacts.IsValidIdentifier(sourceName) || sourceName == type.Name || existingCommand == null && !names.Add(sourceName))
                            Fail(type, "Automatic binding name conflicts: " + sourceName);
                        var contract = compilation.GetTypeByMetadataName(contractName);
                        if (contract == null)
                            Fail(type, "Binding adapter contract unavailable: " + contractName);
                        var componentName = (string)entry.Attribute("component");
                        var componentType = componentName == null ? null : compilation.GetTypeByMetadataName(componentName);
                        if (componentName != null && componentType == null)
                            Fail(type, "Native component type unavailable: " + componentName);
                        var extensionName = (string)entry.Attribute("extension");
                        var extension = extensionName == null ? null : compilation.GetTypeByMetadataName(extensionName);
                        if (extensionName != null && (extension == null || extension.IsAbstract || extension.ContainingType != null || extension.Arity != 0 ||
                            !extension.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "MUI.Unity.ControlPropertyExtensionAttribute") ||
                            !extension.InstanceConstructors.Any(c => c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length == 1 && c.Parameters[0].Type.ToDisplayString() == "MUI.Unity.NativeControl")))
                            Fail(type, "Invalid property extension: " + extensionName);
                        var target = FindElementMember<IPropertySymbol>(extension ?? componentType ?? contract, property);
                        var extensionNotifies = extension != null && target != null && target.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "MUI.Unity.NotifiesChangeAttribute");
                        if (!command && target != null && (target.SetMethod == null || !Accessible(target.SetMethod)))
                        {
                            if (!Accessible(target.GetMethod) || target.IsStatic || target.IsIndexer || target.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "System.ObsoleteAttribute"))
                                Fail(type, "Property cannot be exposed for read access: " + property);
                            var readField = "__muiRead_" + sourceName;
                            if (!names.Add(readField))
                                Fail(type, "Read accessor field conflicts: " + readField);
                            body.Append("internal global::System.Func<").Append(TypeName(target.Type)).Append("> ").Append(readField).Append(";\n")
                                .Append("[global::MUI.Bind(").Append(Literal(name)).Append(", ").Append(Literal(property)).Append(", ElementType = typeof(").Append(TypeName(contract)).Append(")")
                                .Append(lookupType == null ? "" : ", BindingComponentType = typeof(" + TypeName(lookupType) + ")").Append(componentType == null ? "" : ", ComponentType = typeof(" + TypeName(componentType) + ")")
                                .Append(extension == null ? "" : ", PropertyAdapterType = typeof(" + TypeName(extension) + ")")
                                .Append(", ReadAccessorField = ").Append(Literal(readField)).Append(")]\npublic ").Append(TypeName(target.Type)).Append(' ').Append(Escape(sourceName))
                                .Append(" => ").Append(readField).Append(" == null ? throw new global::System.InvalidOperationException(\"View read access requires an active binding session.\") : ").Append(readField).Append("();\n");
                            continue;
                        }
                        if (command)
                        {
                            if (componentType != null)
                                ValidateNativeCommand(type, componentType, property, "interactable");
                            else if (FindElementMember<IEventSymbol>(contract, property) == null)
                                Fail(type, "Binding event unavailable: " + property);
                            var commandElement = componentType ?? contract;
                            if (existingCommand != null)
                            {
                                var proxy = "__muiAutoBinding" + sourceName;
                                if (!names.Add(proxy)) Fail(type, "Automatic command binding conflicts: " + proxy);
                                body.Append("[global::MUI.BindCommand(").Append(Literal(name)).Append(", ").Append(Literal(property))
                                    .Append(", ElementType = typeof(").Append(TypeName(commandElement)).Append(")")
                                    .Append(lookupType == null ? "" : ", ComponentType = typeof(" + TypeName(lookupType) + ")")
                                    .Append(")]\npublic global::MUI.IUICommand ").Append(proxy).Append(" => ").Append(Escape(sourceName)).Append(";\n");
                                continue;
                            }
                            var hook = "On" + (sourceName.EndsWith("Command", StringComparison.Ordinal) ? sourceName.Substring(0, sourceName.Length - 7) : sourceName);
                            var method = "__muiAutoInvoke" + sourceName;
                            if (!names.Add(method))
                                Fail(type, "Automatic command conflicts: " + method);
                            var hookMembers = type.GetMembers(hook);
                            if (hookMembers.Any(m => !(m is IMethodSymbol h) || h.PartialDefinitionPart == null && !h.IsPartialDefinition && !h.DeclaringSyntaxReferences.Any(r => r.GetSyntax() is Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax d && d.Modifiers.Any(SyntaxKind.PartialKeyword))))
                                Fail(type, "Automatic command hook conflicts: " + hook);
                            body.Append("[global::MUI.Command(").Append(Literal(sourceName)).Append(")]\n[global::MUI.BindCommand(").Append(Literal(name)).Append(", ").Append(Literal(property)).Append(", ElementType = typeof(").Append(TypeName(commandElement)).Append(")").Append(lookupType == null ? "" : ", ComponentType = typeof(" + TypeName(lookupType) + ")").Append(")]\nprivate void ").Append(method).Append("(global::MUI.CommandContext context) => ").Append(Escape(hook)).Append("(context);\npartial void ").Append(Escape(hook)).Append("(global::MUI.CommandContext context);\n");
                        }
                        else
                        {
                            if (target == null || target.SetMethod == null || target.SetMethod.DeclaredAccessibility != Accessibility.Public || target.IsIndexer || target.IsStatic)
                                Fail(type, "Binding property must be readable and writable: " + property);
                            var field = "__muiAuto_" + sourceName;
                            if (!names.Add(field))
                                Fail(type, "Automatic backing field conflicts: " + field);
                            var mode = (string)entry.Attribute("mode") ?? "OneWay";
                            if (!new[] { "OneWay", "TwoWay", "OneWayToSource", "OneTime" }.Contains(mode))
                                Fail(type, "Invalid automatic binding direction.");
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
                                if (target.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "System.ObsoleteAttribute"))
                                    Fail(type, "Native property is obsolete: " + property);
                                adapterName = Identifier(pair.Key + "." + sourceName) + "PropertyAdapter";
                                if (compilation.GetTypeByMetadataName((type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString() + ".") + adapterName) != null)
                                    Fail(type, "Generated property adapter name conflicts: " + adapterName);
                                adapters.Append(BuildNativePropertyAdapter(context, type, componentType, target, adapterName, signalName, setterName));
                            }
                            var sourceTypeName = (string)entry.Attribute("sourceType");
                            var sourceType = sourceTypeName == null ? target.Type : compilation.GetTypeByMetadataName(sourceTypeName);
                            if (sourceType == null || !compilation.ClassifyConversion(sourceType, target.Type).IsImplicit)
                                Fail(type, "Widget source type is unavailable or incompatible: " + sourceTypeName);
                            var createVM = (string)entry.Attribute("createVM") == "true";
                            var childRegistration = sourceTypeName == null ? "" : FactoryQualifiedName((INamedTypeSymbol)sourceType, sourceType.Name + "BindingFactory") + ".Register(); ";
                            if (createVM && (!(sourceType is INamedTypeSymbol childType) || childType.IsAbstract ||
                                !childType.InstanceConstructors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public)))
                                Fail(type, "Automatic Widget VM requires a public parameterless constructor: " + sourceTypeName);
                            if (createVM && !names.Add(field + "Initialized"))
                                Fail(type, "Automatic Widget initialization field conflicts: " + field);
                            if (createVM)
                                body.Append("private bool ").Append(field).Append("Initialized;\n");
                            body.Append("private ").Append(TypeName(sourceType)).Append(' ').Append(field).Append(InitialBindingValue(entry, sourceType)).Append(";\n[global::MUI.Bind(").Append(Literal(name)).Append(", ").Append(Literal(property)).Append(", bindingMode: global::MUI.BindingMode.").Append(mode).Append(", ElementType = typeof(").Append(TypeName(contract)).Append(")").Append(lookupType == null ? "" : ", BindingComponentType = typeof(" + TypeName(lookupType) + ")").Append(componentType == null ? "" : ", ComponentType = typeof(" + TypeName(componentType) + ")").Append(adapterName == null ? "" : ", PropertyAdapterType = typeof(" + adapterName + ")" + (entry.Attribute("notificationEvent") == null && !extensionNotifies ? "" : ", NativeNotifications = true")).Append(")]\npublic ").Append(TypeName(sourceType)).Append(' ').Append(Escape(sourceName))
                                .Append(" { get { ").Append(childRegistration).Append(createVM ? "if (!" + field + "Initialized) { " + field + " = CreateOwnedWidgetVM(() => new " + TypeName(sourceType) + "()); " + field + "Initialized = true; } " : "")
                                .Append("return ").Append(field).Append("; } set { ").Append(childRegistration).Append(createVM ? field + "Initialized = true; " : "")
                                .Append("var previous = ").Append(field).Append("; if (!SetProperty(ref ").Append(field).Append(", value, ").Append(Literal(sourceName)).Append(")) return;\n")
                                .Append("On").Append(sourceName).Append("Changed(previous, value);\n");
                            foreach (var dependent in type.GetMembers().OfType<IPropertySymbol>().Where(member => member.GetAttributes().Any(attribute =>
                                attribute.AttributeClass?.ToDisplayString() == "MUI.DependsOnAttribute" && attribute.ConstructorArguments[0].Value as string == sourceName)))
                                body.Append("OnPropertyChanged(").Append(Literal(dependent.Name)).Append(");\n");
                            body.Append("} }\npartial void On").Append(sourceName).Append("Changed(").Append(TypeName(sourceType)).Append(" previous, ").Append(TypeName(sourceType)).Append(" value);\n");
                        }
                    }
                    if (body.Length == 0)
                        continue;
                    var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
                    var code = "// <auto-generated/>\n" + (ns == null ? "" : "namespace " + ns + " {\n") +
                        (type.DeclaredAccessibility == Accessibility.Public ? "public" : "internal") + " partial class " + Escape(type.Name) + " {\n" + body + "}\n" + adapters + (ns == null ? "" : "}\n");
                    var hint = Identifier(compilation.AssemblyName) + "." + Identifier(pair.Key) + ".AutoBindings.g.cs";
                    code = context.AddSource(hint, SourceText.From(code, Encoding.UTF8));
                    compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(code, (CSharpParseOptions)compilation.SyntaxTrees.First().Options, hint, Encoding.UTF8));
                }
                catch (ContractException error) { context.ReportDiagnostic(Diagnostic.Create(InvalidContract, error.Location, error.Message)); }
            }
            if (compilation == context.Compilation)
                return context;
            candidates = compilation.SyntaxTrees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().Select(node => compilation.GetSemanticModel(tree).GetDeclaredSymbol(node) as INamedTypeSymbol)).Where(type => type != null).ToImmutableArray();
            return context.WithCompilation(compilation);
        }

        private static bool MatchesAutoComponent(GenerationContext context, AttributeData attribute, INamedTypeSymbol lookup, string contractName)
        {
            var explicitType = attribute.NamedArguments.FirstOrDefault(p => p.Key == "BindingComponentType").Value.Value as INamedTypeSymbol
                ?? attribute.NamedArguments.FirstOrDefault(p => p.Key == "ComponentType").Value.Value as INamedTypeSymbol;
            var resolved = ResolveElement(context, attribute);
            if (explicitType == null && resolved != null && DerivesFrom(resolved, "UnityEngine.Component")) explicitType = resolved;
            if (explicitType == null && resolved != null)
                for (var parent = resolved.BaseType; parent != null; parent = parent.BaseType)
                    if (parent.OriginalDefinition.ToDisplayString() == "MUI.Unity.ControlPropertyExtension<TComponent>")
                        explicitType = parent.TypeArguments[0] as INamedTypeSymbol;
            if (explicitType != null) return SymbolEqualityComparer.Default.Equals(explicitType, lookup);
            var contract = context.Compilation.GetTypeByMetadataName(contractName);
            return resolved != null && contract != null &&
                (SymbolEqualityComparer.Default.Equals(resolved, contract) || contract.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, resolved)));
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
                    case "TMPro.TMP_Dropdown":
                        expected = "value";
                        setter = "SetValueWithoutNotify";
                        break;
                    case "UnityEngine.UI.Toggle":
                        expected = "isOn";
                        setter = "SetIsOnWithoutNotify";
                        break;
                    case "UnityEngine.UI.InputField":
                    case "TMPro.TMP_InputField":
                        expected = "text";
                        setter = "SetTextWithoutNotify";
                        break;
                    case "UnityEngine.UI.ScrollRect":
                        expected = "normalizedPosition";
                        setter = null;
                        break;
                }
                if (expected != null && expected == property)
                    return true;
            }
            setter = null;
            return false;
        }

        private static string CanonicalAutoProperty(string value) => value;

        private static void ValidatePrefabReferences(XElement metadata, string section, string document)
        {
            // Only inspect this View's serialized reference table, never similarly named child Views.
            var table = Regex.Match(section, @"(?ms)^  nativeBindings:(.*?)(?=^  muiAutoBindings:)").Groups[1].Value;
            var references = Regex.Matches(table, @"(?ms)^  - Name: ([^\r\n]+)\r?\n(.*?)(?=^  - Name:|\z)").Cast<Match>().ToArray();
            foreach (var entry in metadata.Elements("binding"))
            {
                var component = (string)entry.Attribute("targetComponent") ?? (string)entry.Attribute("component");
                if (component == null) continue; // Widget / authored Element bindings have separate ownership rules.
                var name = (string)entry.Attribute("name");
                var matches = references.Where(reference =>
                {
                    if (reference.Groups[1].Value.Trim().Trim('"', '\'') != name) return false;
                    var selector = Regex.Match(reference.Groups[2].Value, @"(?m)^    ComponentType: ([^\r\n]+)");
                    return !selector.Success || selector.Groups[1].Value.Trim().Trim('"', '\'').Split(',')[0] == component;
                }).ToArray();
                var exact = matches.Where(reference => Regex.Match(reference.Groups[2].Value,
                    @"(?m)^    ComponentType: ([^\r\n]+)").Groups[1].Value.Trim().Trim('"', '\'').Split(',')[0] == component).ToArray();
                if (exact.Length != 0) matches = exact;
                if (matches.Length != 1)
                    throw new InvalidOperationException("Prefab reference must uniquely match " + name + " (" + component + "). Save its component reference in the View.");
                var target = Regex.Match(matches[0].Groups[2].Value, @"Target: \{fileID: (-?\d+)([^}]*)\}");
                if (!target.Success || target.Groups[1].Value == "0" ||
                    !target.Groups[2].Value.Contains("guid:") && !Regex.IsMatch(document, @"(?m)^--- !u!\d+ &" + target.Groups[1].Value + @"(?:\s|$)"))
                    throw new InvalidOperationException("Missing Prefab component reference: " + name + " (" + component + ").");
            }
        }

        private static string InitialBindingValue(XElement entry, ITypeSymbol type)
        {
            var initial = (string)entry.Attribute("initial");
            if (type.SpecialType == SpecialType.System_String && initial != null)
                return " = " + Literal(initial);
            if (type.SpecialType == SpecialType.System_Boolean && bool.TryParse(initial, out var flag))
                return flag ? " = true" : " = false";
            if (type.SpecialType == SpecialType.System_Int32 && int.TryParse(initial, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                return " = " + integer.ToString(CultureInfo.InvariantCulture);
            if (type.SpecialType == SpecialType.System_Single && float.TryParse(initial, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return " = " + FloatLiteral(number);
            var name = type.ToDisplayString();
            var components = ((string)entry.Attribute(name == "UnityEngine.Color" ? "initialColor" : "initialVector2"))?.Split(',');
            var count = name == "UnityEngine.Color" ? 4 : name == "UnityEngine.Vector2" ? 2 : 0;
            if (count > 0 && components != null && components.Length == count)
            {
                var values = new List<string>();
                foreach (var component in components)
                {
                    if (!float.TryParse(component, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                        return "";
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
