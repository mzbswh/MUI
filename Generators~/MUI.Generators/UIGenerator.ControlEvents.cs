using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MUI.Generators
{
    public sealed partial class UIGenerator
    {
        private static void AppendControlEvents(GenerationContext context, INamedTypeSymbol type, StringBuilder body, HashSet<string> names)
        {
            var declarations = type.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == "MUI.GenerateControlEventAttribute").ToArray();
            if (declarations.Length == 0) return;
            if (type.GetMembers("InitializeGeneratedControlBindings").Length != 0)
                Fail(type, "Generated control initialization conflicts with an existing member.");
            var initialize = new StringBuilder("protected override void InitializeGeneratedControlBindings() {\nbase.InitializeGeneratedControlBindings();\n");
            for (var index = 0; index < declarations.Length; index++)
            {
                var declaration = declarations[index];
                var accessor = declaration.ConstructorArguments[0].Value as string;
                var signalName = declaration.ConstructorArguments[1].Value as string;
                var name = declaration.ConstructorArguments.Length > 2 ? declaration.ConstructorArguments[2].Value as string : null;
                var owner = FindElementMember<IPropertySymbol>(type, accessor ?? "");
                var component = owner?.Type as INamedTypeSymbol;
                if (owner == null || owner.IsStatic || owner.IsIndexer || owner.GetMethod == null || component == null ||
                    !context.Compilation.IsSymbolAccessibleWithin(owner.GetMethod, type) || !DerivesFrom(component, "UnityEngine.Component"))
                    Fail(type, "Event target must be an accessible Unity component getter: " + accessor);
                var property = FindElementMember<IPropertySymbol>(component, signalName ?? "");
                var field = FindElementMember<IFieldSymbol>(component, signalName ?? "");
                var signalType = property != null && !property.IsStatic && !property.IsIndexer && Accessible(property.GetMethod) ? property.Type :
                    field != null && !field.IsStatic && field.DeclaredAccessibility == Accessibility.Public ? field.Type : null;
                INamedTypeSymbol unityEvent = null;
                for (var current = signalType as INamedTypeSymbol; current != null; current = current.BaseType)
                    if (current.ContainingNamespace.ToDisplayString() == "UnityEngine.Events" && current.Name == "UnityEvent" && current.Arity <= 4)
                    { unityEvent = current; break; }
                if (unityEvent == null) Fail(type, "Event must be a public UnityEvent: " + signalName);
                var notify = declaration.NamedArguments.FirstOrDefault(p => p.Key == "NotifyProperty").Value.Value as string;
                var callback = declaration.NamedArguments.FirstOrDefault(p => p.Key == "Handler").Value.Value as string;
                if (callback != null)
                {
                    var method = FindElementMember<IMethodSymbol>(type, callback);
                    if (method == null || method.IsStatic || method.IsGenericMethod || method.IsAsync || !method.ReturnsVoid || method.Parameters.Length != 0 ||
                        !context.Compilation.IsSymbolAccessibleWithin(method, type))
                        Fail(type, "Event handler must be an accessible synchronous void instance method without arguments: " + callback);
                }
                var condition = declaration.NamedArguments.FirstOrDefault(p => p.Key == "Condition").Value.Value as string;
                if (condition != null)
                {
                    var gate = FindElementMember<IPropertySymbol>(type, condition);
                    if (gate == null || gate.IsStatic || gate.IsIndexer || gate.Type.SpecialType != SpecialType.System_Boolean || gate.GetMethod == null ||
                        !context.Compilation.IsSymbolAccessibleWithin(gate.GetMethod, type)) Fail(type, "Event condition must be an accessible bool getter: " + condition);
                }
                var inheritedProperty = false;
                for (var current = type.BaseType; current != null; current = current.BaseType)
                    inheritedProperty |= current.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "MUI.GenerateControlPropertyAttribute" &&
                        attribute.ConstructorArguments.Length == 3 && attribute.ConstructorArguments[2].Value as string == notify);
                if (notify != null && notify.Length != 0 && !names.Contains(notify) && !inheritedProperty && FindElementMember<IPropertySymbol>(type, notify) == null)
                    Fail(type, "Event notification property unavailable: " + notify);
                if (name == null && string.IsNullOrEmpty(notify) && callback == null)
                    Fail(type, "A generated control event requires a public event name or a notification property.");
                var forward = declaration.NamedArguments.Any(p => p.Key == "ForwardArguments" && p.Value.Value is bool value && value);
                var arguments = string.Join(", ", Enumerable.Range(0, unityEvent.Arity).Select(i => "arg" + i));
                var generic = unityEvent.Arity == 0 ? "" : "<" + string.Join(", ", unityEvent.TypeArguments.Select(TypeName)) + ">";
                if (name != null)
                {
                    if (!SyntaxFacts.IsValidIdentifier(name) || !names.Add(name) || FindElementMember<ISymbol>(type, name) != null)
                        Fail(type, "Generated control event conflicts: " + name);
                    body.Append("public event global::System.Action").Append(forward ? generic : "").Append(' ').Append(Escape(name)).Append(";\n")
                        .Append("partial void On").Append(name).Append("Invoking();\n");
                }
                var optional = declaration.NamedArguments.Any(p => p.Key == "Optional" && p.Value.Value is bool allowMissing && allowMissing);
                var target = "control" + index;
                var signal = "signal" + index;
                var handler = "handler" + index;
                var gateExpression = "CanReceiveGeneratedInput(" + target + ")" + (condition == null ? "" : " && " + Escape(condition));
                initialize.Append("var ").Append(target).Append(" = ").Append(Escape(accessor)).Append(";\n")
                    .Append(optional ? "if (" + target + " != null) {\n" : "if (" + target + " == null) throw new global::System.InvalidOperationException(" + Literal("Missing native event target: " + accessor) + ");\n")
                    .Append("var ").Append(signal).Append(" = ")
                    .Append(target).Append('.').Append(Escape(signalName)).Append(";\nif (").Append(signal).Append(" == null) throw new global::System.InvalidOperationException(")
                    .Append(Literal("Missing native event: " + signalName)).Append(");\nglobal::UnityEngine.Events.UnityAction").Append(generic).Append(' ').Append(handler)
                    .Append(" = (").Append(arguments).Append(") => {\nif (!( ").Append(gateExpression).Append(")) return;\n");
                if (callback != null) initialize.Append(Escape(callback)).Append("();\nif (!( ").Append(gateExpression).Append(")) return;\n");
                if (notify != null) initialize.Append("NotifyChanged(").Append(Literal(notify)).Append(");\n");
                if (name != null)
                    initialize.Append("if (!( ").Append(gateExpression).Append(")) return;\nOn").Append(name).Append("Invoking();\nif (").Append(gateExpression).Append(") ")
                        .Append(Escape(name)).Append("?.Invoke(").Append(forward ? arguments : "").Append(");\n");
                initialize.Append("};\nOnDispose(() => { ").Append(signal).Append(".RemoveListener(").Append(handler).Append("); ")
                    .Append(name == null ? "" : Escape(name) + " = null;").Append(" });\n").Append(signal).Append(".AddListener(").Append(handler).Append(");\n");
                if (optional) initialize.Append("}\n");
            }
            body.Append(initialize).Append("}\n");
        }
    }
}
