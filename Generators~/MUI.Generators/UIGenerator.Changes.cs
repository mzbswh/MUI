using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace MUI.Generators
{
    public sealed partial class UIGenerator
    {
        /// <summary>变化回调只关联当前模型生成的属性，避免对手写 setter 作出无法兑现的承诺。</summary>
        private static Dictionary<string, List<IMethodSymbol>> CollectChangeHandlers(INamedTypeSymbol type)
        {
            var fields = new Dictionary<string, IFieldSymbol>(StringComparer.Ordinal);
            foreach (var field in type.GetMembers().OfType<IFieldSymbol>())
            {
                var observable = Attribute(field, "MUI.ObservablePropertyAttribute");
                if (observable != null)
                {
                    var name = ObservablePropertyName(field, observable);
                    if (fields.ContainsKey(name))
                    {
                        Fail(field, "多个字段生成了相同的属性：" + name);
                    }

                    fields.Add(name, field);
                }
            }

            var handlers = new Dictionary<string, List<IMethodSymbol>>(StringComparer.Ordinal);
            foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
            {
                foreach (var attribute in method.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == "MUI.OnChangedAttribute"))
                {
                    var name = attribute.ConstructorArguments.Length == 0 ? null : attribute.ConstructorArguments[0].Value as string;
                    if (string.IsNullOrWhiteSpace(name) || !fields.TryGetValue(name, out var field))
                    {
                        Fail(method, "OnChanged 必须指定当前模型中由 ObservableProperty 生成的属性名称。");
                        continue;
                    }

                    if (method.IsStatic || method.IsAbstract || method.IsGenericMethod || method.IsAsync ||
                        !method.ReturnsVoid || method.MethodKind != MethodKind.Ordinary ||
                        (method.IsPartialDefinition && method.PartialImplementationPart == null) ||
                        method.Parameters.Length > 2 || method.Parameters.Any(p => p.RefKind != RefKind.None ||
                            !SymbolEqualityComparer.Default.Equals(p.Type, field.Type)))
                    {
                        Fail(method, "OnChanged 方法必须是已实现的同步实例 void 方法，接收零个参数、新值，或旧值与新值；参数类型须与属性一致。");
                    }

                    if (!handlers.TryGetValue(name, out var list))
                    {
                        list = new List<IMethodSymbol>();
                        handlers.Add(name, list);
                    }

                    if (list.Contains(method))
                    {
                        Fail(method, "同一方法不能重复监听同一属性：" + name);
                    }

                    list.Add(method);
                }
            }

            return handlers;
        }

        private static string ObservablePropertyName(IFieldSymbol field, AttributeData observable)
        {
            var name = observable.ConstructorArguments.Length > 0 ? observable.ConstructorArguments[0].Value as string : null;
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }

            var trimmed = field.Name.TrimStart('_');
            if (trimmed.Length == 0)
            {
                Fail(field, "Cannot derive a property name from this field.");
            }

            return char.ToUpperInvariant(trimmed[0]) + trimmed.Substring(1);
        }

        /// <summary>依赖通知只来自生成字段，目标必须是模型上可读取的实例属性。</summary>
        private static void ValidateDependentNotifications(INamedTypeSymbol type)
        {
            foreach (var field in type.GetMembers().OfType<IFieldSymbol>())
            {
                var targets = GetDependentNotifications(field);
                if (targets.Length == 0)
                {
                    continue;
                }

                var observable = Attribute(field, "MUI.ObservablePropertyAttribute");
                if (observable == null)
                {
                    Fail(field, "NotifyPropertyChangedFor 必须与 ObservableProperty 一起使用。");
                }

                var sourceName = ObservablePropertyName(field, observable);
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var name in targets)
                {
                    if (string.IsNullOrWhiteSpace(name) || name == sourceName || !names.Add(name))
                    {
                        Fail(field, "依赖通知目标不能为空、重复或指向自身：" + name);
                    }

                    ISymbol target = null;
                    for (var current = type; current != null; current = current.BaseType)
                    {
                        var members = current.GetMembers(name);
                        if (members.Length != 0)
                        {
                            target = members.Length == 1 ? members[0] : null;
                            break;
                        }
                    }

                    if (!(target is IPropertySymbol property) || property.IsStatic || property.IsIndexer ||
                        !Accessible(property.GetMethod))
                    {
                        Fail(field, "依赖通知目标必须是已有的公共可读实例属性：" + name);
                    }
                }
            }
        }

        private static string[] GetDependentNotifications(IFieldSymbol field)
        {
            return field.GetAttributes()
                .Where(a => a.AttributeClass?.ToDisplayString() == "MUI.NotifyPropertyChangedForAttribute")
                .Select(a => a.ConstructorArguments.Length == 0 ? null : a.ConstructorArguments[0].Value as string)
                .ToArray();
        }

        /// <summary>保留 SetProperty 的相等判断与通知顺序；回调异常直接传给赋值调用者。</summary>
        private static void AppendObservableSetter(StringBuilder output, IFieldSymbol field, string name,
            Dictionary<string, List<IMethodSymbol>> handlers)
        {
            handlers.TryGetValue(name, out var callbacks);
            var notifications = GetDependentNotifications(field);
            if (callbacks == null && notifications.Length == 0)
            {
                output.Append("            set => SetProperty(ref this.").Append(Escape(field.Name))
                    .Append(", value, ").Append(Literal(name)).Append(");\n");
                return;
            }

            output.Append("            set\n            {\n");
            if (callbacks != null && callbacks.Any(method => method.Parameters.Length == 2))
            {
                output.Append("                var oldValue = this.").Append(Escape(field.Name)).Append(";\n");
            }

            output.Append("                if (!SetProperty(ref this.").Append(Escape(field.Name))
                .Append(", value, ").Append(Literal(name)).Append("))\n                {\n                    return;\n                }\n");
            foreach (var method in callbacks ?? Enumerable.Empty<IMethodSymbol>())
            {
                var arguments = method.Parameters.Length == 0 ? "" : method.Parameters.Length == 1 ? "value" : "oldValue, value";
                output.Append("                this.").Append(Escape(method.Name)).Append('(').Append(arguments).Append(");\n");
            }

            foreach (var target in notifications)
            {
                output.Append("                OnPropertyChanged(").Append(Literal(target)).Append(");\n");
            }

            output.Append("            }\n");
        }
    }
}
