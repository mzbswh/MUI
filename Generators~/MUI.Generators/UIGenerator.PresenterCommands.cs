using System;
using System.Collections.Generic;
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
        private static INamedTypeSymbol PresenterBase(INamedTypeSymbol type)
        {
            for (var current = type.BaseType; current != null; current = current.BaseType)
            {
                if (current.OriginalDefinition.ToDisplayString() == "MUI.Presenter<TViewModel, TArgs, TResult>")
                {
                    return current;
                }
            }

            return null;
        }

        /// <summary>Prefab 自动命令让位于明确选择或唯一匹配的 Presenter 声明。</summary>
        private static INamedTypeSymbol AutoBindingPresenter(INamedTypeSymbol model, IEnumerable<INamedTypeSymbol> candidates)
        {
            var route = Attribute(model, "MUI.Navigation.ViewRouteAttribute");
            if (route != null)
            {
                foreach (var option in route.NamedArguments)
                {
                    if (option.Key == "PresenterType")
                    {
                        return option.Value.Value as INamedTypeSymbol;
                    }
                }
            }

            var matches = candidates.Where(type => type != null && !type.IsAbstract && PresenterBase(type) != null &&
                SymbolEqualityComparer.Default.Equals(PresenterBase(type).TypeArguments[0], model) &&
                (route == null || IsCompatiblePresenter(type, model,
                    route.ConstructorArguments[0].Value as ITypeSymbol, route.ConstructorArguments[1].Value as ITypeSymbol)))
                .Distinct(SymbolEqualityComparer.Default).ToArray();
            return matches.Length == 1 ? (INamedTypeSymbol)matches[0] : null;
        }

        /// <summary>命令实例留在 Presenter，绑定追加到宿主会话，不向共享模型注入委托。</summary>
        private static void GeneratePresenterCommands(GenerationContext context, INamedTypeSymbol type)
        {
            ValidateTypeScope(context, type);
            foreach (var reference in type.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax() is ClassDeclarationSyntax declaration &&
                    !declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
                {
                    Fail(type, "Presenter declarations with commands must be partial.");
                }
            }

            if (!(PresenterBase(type).TypeArguments[0] is INamedTypeSymbol model))
            {
                Fail(type, "Presenter commands require a named ViewModel type.");
                return;
            }

            foreach (var name in new[] { "BuildCommandBindings", "CommandManifest" })
            {
                if (type.GetMembers(name).Length != 0)
                {
                    Fail(type, "Presenter member conflicts with generated command infrastructure: " + name);
                }
            }

            if (Attribute(type, "MUI.ViewContractAttribute") != null || Attribute(type, "MUI.Navigation.ViewRouteAttribute") != null)
            {
                Fail(type, "ViewContract and ViewRoute belong on the ViewModel, not the Presenter.");
            }

            var writers = new HashSet<string>(StringComparer.Ordinal);
            var events = new HashSet<string>(StringComparer.Ordinal);
            // 使用与模型生成相同的目标键，提前检测跨 VM / Presenter 冲突。
            foreach (var member in CollectBindingMembers(model))
            {
                AppendCommand(context, member.ContainingType, member, new StringBuilder(), new StringBuilder(),
                    new StringBuilder(), CollectReservedMemberNames(context, member.ContainingType), writers, events, "model");
                foreach (var attribute in member.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == "MUI.BindAttribute"))
                {
                    var mode = attribute.ConstructorArguments.Length > 3 ? (int)attribute.ConstructorArguments[3].Value : 0;
                    var element = ResolveElement(context, attribute);
                    if (mode != 2 && element != null)
                    {
                        writers.Add((string)attribute.ConstructorArguments[0].Value + "\0" + TypeName(element) + "\0" +
                            (string)attribute.ConstructorArguments[1].Value);
                    }
                }
            }

            var properties = new StringBuilder();
            var bindings = new StringBuilder();
            var manifest = new StringBuilder();
            var names = CollectReservedMemberNames(context, type);
            foreach (var member in CollectBindingMembers(type))
            {
                if (Attribute(member, "MUI.ObservablePropertyAttribute") != null ||
                    Attribute(member, "MUI.BindAttribute") != null ||
                    Attribute(member, "MUI.OnChangedAttribute") != null ||
                    Attribute(member, "MUI.NotifyPropertyChangedForAttribute") != null)
                {
                    Fail(member, "Presenter generation supports commands only; observable state belongs on the ViewModel.");
                }

                var own = SymbolEqualityComparer.Default.Equals(member.ContainingType, type);
                // 基类负责建立自己的绑定；这里仍收集其契约以检测派生层冲突。
                AppendCommand(context, member.ContainingType, member, own ? properties : new StringBuilder(),
                    own ? bindings : new StringBuilder(), manifest,
                    own ? names : CollectReservedMemberNames(context, member.ContainingType), writers, events, "this");
            }

            var output = new StringBuilder();
            output.Append("    [global::MUI.GeneratedBindingMetadata(1)]\n    partial class ")
                .Append(Escape(type.Name)).Append(TypeParameters(type)).Append(TypeConstraints(type)).Append("\n    {\n")
                .Append(properties)
                .Append("        public override global::MUI.BindingManifest CommandManifest { get; } = new global::MUI.BindingManifest(typeof(")
                .Append(TypeName(model)).Append("), new global::MUI.BindingEntry[]\n        {\n")
                .Append(manifest).Append("        });\n")
                .Append("        protected override void BuildCommandBindings(global::MUI.BindingBuilder<")
                .Append(TypeName(model)).Append("> builder)\n        {\n            base.BuildCommandBindings(builder);\n")
                .Append(bindings).Append("        }\n    }\n");
            context.AddSource(Identifier(type.ToDisplayString()) + ".PresenterCommands.g.cs",
                SourceText.From(WrapTypeScope(type, output), Encoding.UTF8));
        }
    }
}
