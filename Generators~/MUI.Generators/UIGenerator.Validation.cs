using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace MUI.Generators
{
    public sealed partial class UIGenerator
    {
        /// <summary>校验声明的状态写入目标，与其他反向绑定共用单写入者约束。</summary>
        private static string BuildValidationWriter(INamedTypeSymbol type, ISymbol member,
            AttributeData attribute, string sourceName, int mode, Dictionary<string, string> writers)
        {
            var name = attribute.NamedArguments.FirstOrDefault(argument => argument.Key == "ValidationProperty").Value.Value as string;
            if (name == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(name) || (mode != 1 && mode != 2) || name == sourceName)
            {
                Fail(member, "ValidationProperty requires a distinct writable BindingValidationState property and a reverse binding.");
            }

            ITypeSymbol stateType = null;
            ISymbol stateMember = null;
            for (var current = type; current != null && stateMember == null; current = current.BaseType)
            {
                foreach (var candidate in current.GetMembers())
                {
                    if (candidate is IPropertySymbol property && property.Name == name &&
                        !property.IsStatic && !property.IsIndexer && Accessible(property.SetMethod))
                    {
                        stateType = property.Type;
                        stateMember = property;
                        break;
                    }

                    if (candidate is IFieldSymbol field)
                    {
                        var observable = Attribute(field, "MUI.ObservablePropertyAttribute");
                        if (observable != null && ObservablePropertyName(field, observable) == name &&
                            !field.IsStatic && !field.IsReadOnly && !field.IsConst)
                        {
                            stateType = field.Type;
                            stateMember = field;
                            break;
                        }
                    }
                }
            }

            if (stateType?.ToDisplayString() != "MUI.BindingValidationState")
            {
                Fail(member, "ValidationProperty must name a public writable BindingValidationState property or an ObservableProperty field.");
            }

            ValidateInheritedSource(type, stateMember, name);
            if (writers.TryGetValue(name, out var previous))
            {
                Fail(member, "Multiple reverse writers for validation property " + name + ": " + previous + ", " + sourceName);
            }

            writers.Add(name, sourceName + " validation");
            var receiver = SymbolEqualityComparer.Default.Equals(type, stateMember.ContainingType)
                ? "model" : "((" + TypeName(stateMember.ContainingType) + ")model)";
            return "(model, validation) => " + receiver + "." + Escape(name) + " = validation";
        }
    }
}
