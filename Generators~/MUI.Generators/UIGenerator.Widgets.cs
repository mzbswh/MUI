using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace MUI.Generators
{
    public sealed partial class UIGenerator
    {
        private static string WidgetRegistration(GenerationContext context, INamedTypeSymbol model, ImmutableArray<INamedTypeSymbol> candidates)
        {
            var widget = Attribute(model, "MUI.UIWidgetAttribute");
            if (widget == null || context.Compilation.GetTypeByMetadataName("MUI.Widgets.WidgetRegistry") == null)
                return "";
            var unit = context.Compilation.GetTypeByMetadataName("MUI.Unit");
            INamedTypeSymbol presenter = null;
            foreach (var option in widget.NamedArguments)
                if (option.Key == "PresenterType")
                    presenter = option.Value.Value as INamedTypeSymbol;
            var matches = candidates.Where(type => type != null && !type.IsAbstract && PresenterBase(type) != null &&
                SymbolEqualityComparer.Default.Equals(PresenterBase(type).TypeArguments[0], model) &&
                SymbolEqualityComparer.Default.Equals(PresenterBase(type).TypeArguments[2], unit) && context.Compilation.IsSymbolAccessibleWithin(type, model))
                .Distinct(SymbolEqualityComparer.Default).Cast<INamedTypeSymbol>().ToArray();
            // Existing binding-only widgets may intentionally have several manually selected presenters.
            // Direct references require an explicit factory; legacy manual definitions remain valid.
            if (presenter == null && matches.Length > 1)
                return "";
            if (presenter == null && matches.Length == 1)
                presenter = matches[0];
            var args = presenter == null ? unit : PresenterBase(presenter)?.TypeArguments[1];
            if (presenter != null && (args == null || !IsCompatiblePresenter(presenter, model, args, unit)))
                Fail(model, "Widget Presenter 必须匹配模型，且结果类型为 Unit。");
            var factory = "_ => new global::MUI.EmptyPresenter<" + TypeName(model) + ", " + TypeName(args) + ", global::MUI.Unit>()";
            if (presenter != null)
            {
                if (presenter.InstanceConstructors.Any(ctor => ctor.Parameters.Length == 0 && Accessible(ctor)))
                    factory = "_ => new " + TypeName(presenter) + "()";
                else if (presenter.InstanceConstructors.Any(ctor => ctor.Parameters.Length == 1 && Accessible(ctor) && SymbolEqualityComparer.Default.Equals(ctor.Parameters[0].Type, model)))
                    factory = "vm => new " + TypeName(presenter) + "(vm)";
                else
                    factory = "_ => throw new global::System.InvalidOperationException(\"Register a Widget presenter factory before activation: " + presenter.ToDisplayString() + "\")";
            }
            return " global::MUI.Widgets.WidgetRegistry.Register<" + TypeName(model) + ", " + TypeName(args) + ">(" + factory + ", Create);";
        }
    }
}
