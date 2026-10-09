using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MUI.Generators
{
    public sealed partial class UIGenerator
    {
        /// <summary>输出命令及事件绑定；同步方法直接生成同步命令，不包装为异步任务。</summary>
        private static bool AppendCommand(GenerationContext context, INamedTypeSymbol type, ISymbol member,
            StringBuilder properties, StringBuilder bindings, StringBuilder manifest, HashSet<string> names,
            HashSet<string> targetWriters, HashSet<string> commandEvents, string receiver)
        {
            var command = Attribute(member, "MUI.CommandAttribute");
            var attributes = member.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == "MUI.BindCommandAttribute").ToArray();
            if (command == null && attributes.Length == 0)
            {
                return false;
            }

            string propertyName;
            if (member is IMethodSymbol method)
            {
                if (command == null)
                {
                    Fail(member, "A command method requires CommandAttribute.");
                }

                if (method.IsStatic || method.IsGenericMethod || method.MethodKind != MethodKind.Ordinary || method.IsAbstract ||
                    (method.IsPartialDefinition && method.PartialImplementationPart == null))
                {
                    Fail(member, "Commands require implemented non-generic instance methods.");
                }

                var args = new List<string>();
                var kinds = new HashSet<string>();
                foreach (var parameter in method.Parameters)
                {
                    var parameterType = parameter.Type.ToDisplayString();
                    if (parameter.RefKind != RefKind.None || !kinds.Add(parameterType))
                    {
                        Fail(member, "Command parameters must be distinct context/token values.");
                    }

                    if (parameterType == "System.Threading.CancellationToken")
                    {
                        args.Add("context.Token");
                    }
                    else if (parameterType == "MUI.CommandContext")
                    {
                        args.Add("context");
                    }
                    else
                    {
                        Fail(member, "Command methods accept only CommandContext and/or CancellationToken.");
                    }
                }
                var result = method.ReturnType.ToDisplayString();
                if (result != "void" && result != "System.Threading.Tasks.Task" && result != "System.Threading.Tasks.ValueTask")
                {
                    Fail(member, "Command methods return void, Task or ValueTask.");
                }

                if (method.IsAsync && method.ReturnsVoid)
                {
                    Fail(member, "Async void commands cannot be tracked. Return Task or ValueTask.");
                }

                propertyName = CommandPropertyName(method, command);
                if (!SyntaxFacts.IsValidIdentifier(propertyName) || propertyName == type.Name || !names.Add(propertyName))
                {
                    Fail(member, "Generated command name is invalid or conflicts with a member: " + propertyName);
                }

                var fieldName = "__mui_" + propertyName;
                if (!names.Add(fieldName))
                {
                    Fail(member, "Generated command backing field conflicts: " + fieldName);
                }

                var canExecute = "null";
                var concurrency = 0;
                var capacity = 32;
                foreach (var argument in command.NamedArguments)
                {
                    if (argument.Key == "Concurrency")
                    {
                        concurrency = (int)argument.Value.Value;
                    }

                    if (argument.Key == "Capacity")
                    {
                        capacity = (int)argument.Value.Value;
                    }

                    if (argument.Key == "CanExecute")
                    {
                        var name = argument.Value.Value as string;
                        canExecute = ResolveCanExecute(context, type, member, name);
                    }
                }
                if (capacity < 1 || concurrency < 0 || concurrency > 3)
                {
                    Fail(member, "Invalid command concurrency or capacity.");
                }

                var invocation = "this." + Escape(method.Name) + "(" + string.Join(", ", args) + ")";
                var body = result == "void"
                    ? "context => { " + invocation + "; return default(global::System.Threading.Tasks.ValueTask); }"
                    : result == "System.Threading.Tasks.Task"
                        ? "context => new global::System.Threading.Tasks.ValueTask(" + invocation + ")" : "context => " + invocation;
                properties.Append("        private global::MUI.AsyncCommand ").Append(fieldName).Append(";\n");
                AppendBindingMetadata(context, properties, member, attributes, true);
                properties.Append("        public global::MUI.AsyncCommand ").Append(Escape(propertyName)).Append(" => ").Append(fieldName)
                    .Append(" ?? (").Append(fieldName).Append(" = new global::MUI.AsyncCommand(\n            ").Append(body).Append(", ").Append(canExecute)
                    .Append(", (global::MUI.CommandConcurrency)").Append(concurrency).Append(", ").Append(capacity).Append("));\n");
            }
            else if (member is IPropertySymbol property)
            {
                if (command != null || property.IsStatic || property.IsIndexer || !Accessible(property.GetMethod))
                {
                    Fail(member, "A bound command property must be a public readable instance property.");
                }

                if (property.Type.ToDisplayString() != "MUI.IUICommand"
                    && !property.Type.AllInterfaces.Any(i => i.ToDisplayString() == "MUI.IUICommand"))
                {
                    Fail(member, "A bound command property must implement IUICommand.");
                }

                propertyName = property.Name;
            }
            else
            {
                Fail(member, "Commands bind methods or command properties.");
                return false;
            }

            foreach (var attribute in attributes)
            {
                var targetName = attribute.ConstructorArguments[0].Value as string;
                var eventName = attribute.ConstructorArguments[1].Value as string;
                if (string.IsNullOrWhiteSpace(targetName) || string.IsNullOrWhiteSpace(eventName))
                {
                    Fail(member, "Command binding names must not be empty.");
                }

                var element = ResolveElement(context, attribute);
                if (element == null || !element.AllInterfaces.Any(i => i.ToDisplayString() == "MUI.IElement"))
                {
                    Fail(member, "Use nameof(Element.Event) or explicitly specify an IElement type.");
                }

                ValidateElementType(context, member, element);
                var targetEvent = FindElementMember<IEventSymbol>(element, eventName);
                if (targetEvent == null || targetEvent.IsStatic || targetEvent.Type.ToDisplayString() != "System.Action" ||
                    !Accessible(targetEvent.AddMethod) || !Accessible(targetEvent.RemoveMethod))
                {
                    Fail(member, "Command target must be a public instance event of type System.Action.");
                }

                var interactableName = "Interactable";
                foreach (var named in attribute.NamedArguments)
                {
                    if (named.Key == "InteractableProperty")
                    {
                        interactableName = named.Value.Value as string;
                    }
                }

                var interactable = FindElementMember<IPropertySymbol>(element, interactableName ?? "");
                if (interactable == null || interactable.IsStatic || interactable.IsIndexer ||
                    interactable.Type.SpecialType != SpecialType.System_Boolean || !Accessible(interactable.SetMethod))
                {
                    Fail(member, "Command target requires a writable bool Interactable property (or configured equivalent).");
                }

                if (!commandEvents.Add(targetName + "\0" + TypeName(element) + "\0" + eventName))
                {
                    Fail(member, "Multiple commands bind the same Element event: " + targetName + "." + eventName);
                }

                if (!targetWriters.Add(targetName + "\0" + TypeName(element) + "\0" + interactableName))
                {
                    Fail(member, "Command CanExecute conflicts with another writer to " + targetName + "." + interactableName);
                }

                bindings.Append("            builder.Command<").Append(TypeName(element)).Append(">(\n                ").Append(Literal(targetName))
                    .Append(", model => ").Append(receiver).Append('.').Append(Escape(propertyName)).Append(",\n                (element, handler) => element.").Append(Escape(eventName))
                    .Append(" += handler, (element, handler) => element.").Append(Escape(eventName)).Append(" -= handler,\n                (element, value) => element.")
                    .Append(Escape(interactableName)).Append(" = value, ").Append(Literal(interactableName)).Append(");\n");
                manifest.Append("                new global::MUI.BindingEntry(").Append(Literal(propertyName)).Append(", ").Append(Literal(targetName))
                    .Append(", typeof(").Append(TypeName(element)).Append("), ").Append(Literal(eventName))
                    .Append(", global::MUI.BindingMode.OneWay, global::MUI.BindingEntryKind.Command, ").Append(Literal(interactableName));
                AppendSourceLocation(manifest, attribute, member);
                manifest.Append("),\n");
            }
            return true;
        }

        /// <summary>生成和基类名称预留使用同一命名规则，避免首次编译时漏掉尚未生成的成员。</summary>
        private static string CommandPropertyName(IMethodSymbol method, AttributeData command)
        {
            var name = command.ConstructorArguments.Length > 0 ? command.ConstructorArguments[0].Value as string : null;
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            return (method.Name.EndsWith("Async", System.StringComparison.Ordinal)
                ? method.Name.Substring(0, method.Name.Length - 5) : method.Name) + "Command";
        }
    }
}
