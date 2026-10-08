using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MUI.Generators
{
    public sealed partial class UIGenerator
    {
        /// <summary>编译期解析路径并输出访问器；运行时只接收属性名与委托，不使用反射。</summary>
        private static PathSource ResolveSourcePath(INamedTypeSymbol root, ISymbol declaration,
            string name, ITypeSymbol sourceType, bool readable, bool writable, string receiver, AttributeData attribute)
        {
            var argument = attribute.NamedArguments.FirstOrDefault(value => value.Key == "SourcePath");
            var path = argument.Value.Value as string;
            var expression = receiver + "." + Escape(name);
            if (argument.Key == null)
            {
                return new PathSource(name, sourceType, readable, writable, expression, null);
            }

            if (string.IsNullOrWhiteSpace(path) || !readable)
            {
                Fail(declaration, "SourcePath requires a readable root model property and a non-empty relative property path.");
            }

            var nodes = new List<string> { PathMember(root, name, "model") };
            var ownerStatements = new List<string>();
            string leafExpression = null;
            string leafAccess = null;
            var segments = path.Split('.');
            for (var index = 0; index < segments.Length; ++index)
            {
                var segment = segments[index];
                if (string.IsNullOrWhiteSpace(segment) || (!SyntaxFacts.IsValidIdentifier(segment) && SyntaxFacts.GetKeywordKind(segment) == SyntaxKind.None))
                {
                    Fail(declaration, "SourcePath contains an invalid property name: " + path);
                }

                if (!(sourceType is INamedTypeSymbol owner) || !DerivesFrom(owner, "MUI.ViewModel"))
                {
                    Fail(declaration, "Each SourcePath property owner must derive from ViewModel: " + name);
                }

                var ownerLocal = "owner" + index;
                var ownerRead = index == 0 ? expression : leafExpression;
                ownerStatements.Add("var " + ownerLocal + " = " + ownerRead + "; if (" + ownerLocal + " == null) { return null; }");
                nodes.Add(PathMember(root, segment, "{ " + string.Join(" ", ownerStatements) + " return " + ownerLocal + "; }"));
                var property = FindPathProperty((INamedTypeSymbol)sourceType, segment, declaration);
                if (property is IPropertySymbol declared)
                {
                    sourceType = declared.Type;
                    readable = Accessible(declared.GetMethod);
                    writable = Accessible(declared.SetMethod);
                }
                else
                {
                    sourceType = ((IFieldSymbol)property).Type;
                    readable = writable = true;
                }

                // 强制声明类型接收者，保证继承与同名成员不会改变已校验的路径。
                expression = "((" + TypeName(property.ContainingType) + ")(" + expression + "))." + Escape(segment);
                leafExpression = "((" + TypeName(property.ContainingType) + ")" + ownerLocal + ")." + Escape(segment);
                leafAccess = "((" + TypeName(property.ContainingType) + ")owner)." + Escape(segment);
                name += "." + segment;
                if (index < segments.Length - 1 && !readable)
                {
                    Fail(declaration, "Intermediate SourcePath properties must have public getters: " + name);
                }
            }

            // 叶属性读写直接使用运行时捕获的拥有者，禁止在反向提交时再次读取中间模型。
            var definition = "new global::MUI.BindingSourcePath<" + TypeName(root) + ", " + TypeName(sourceType) + ">(" +
                (readable ? "owner => " + leafAccess : "null") + ", " +
                (writable ? "(owner, value) => " + leafAccess + " = value" : "null") + ", " + string.Join(", ", nodes) + ")";
            return new PathSource(name, sourceType, readable, writable, expression, definition);
        }

        private static string PathMember(INamedTypeSymbol root, string name, string owner) =>
            "new global::MUI.BindingSourceMember<" + TypeName(root) + ">(" + Literal(name) + ", model => " + owner + ")";

        private static ISymbol FindPathProperty(INamedTypeSymbol owner, string name, ISymbol declaration)
        {
            for (var current = owner; current != null; current = current.BaseType)
            {
                var named = current.GetMembers(name);
                if (named.Length != 0)
                {
                    if (named.Length == 1 && named[0] is IPropertySymbol property && !property.IsStatic && !property.IsIndexer)
                    {
                        return property;
                    }

                    Fail(declaration, "SourcePath requires an instance property: " + owner.Name + "." + name);
                }

                foreach (var member in current.GetMembers().OfType<IFieldSymbol>())
                {
                    var observable = Attribute(member, "MUI.ObservablePropertyAttribute");
                    if (observable != null && ObservablePropertyName(member, observable) == name &&
                        !member.IsStatic && !member.IsReadOnly && !member.IsConst)
                    {
                        return member;
                    }
                }
            }

            Fail(declaration, "SourcePath property does not exist: " + owner.Name + "." + name);
            return null;
        }

        private static string PathNullValue(GenerationContext context, ISymbol member, AttributeData attribute,
            ITypeSymbol target, PathSource source, int mode)
        {
            var argument = attribute.NamedArguments.FirstOrDefault(value => value.Key == "NullValue");
            if (argument.Key == null)
            {
                return "default(" + TypeName(target) + ")";
            }

            if (source.Definition == null || mode == 2)
            {
                Fail(member, "NullValue requires a forward SourcePath binding.");
            }

            var constant = argument.Value;
            if (constant.IsNull)
            {
                if (!target.IsReferenceType && !(target is INamedTypeSymbol nullable && nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T))
                {
                    Fail(member, "NullValue cannot be null for a non-nullable target.");
                }

                return "default(" + TypeName(target) + ")";
            }

            var destination = target is INamedTypeSymbol optional && optional.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
                ? optional.TypeArguments[0] : target;
            var conversion = ((CSharpCompilation)context.Compilation).ClassifyConversion(constant.Type, target);
            if (!conversion.IsImplicit && !FitsIntegralConstant(constant, destination))
            {
                Fail(member, "NullValue must be implicitly assignable to the Element target type.");
            }

            return "(" + TypeName(target) + ")(" + AttributeConstant(constant, member) + ")";
        }

        private static bool FitsIntegralConstant(TypedConstant constant, ITypeSymbol target)
        {
            if (constant.Type.SpecialType != SpecialType.System_Int32 && constant.Type.SpecialType != SpecialType.System_Int64)
            {
                return false;
            }

            var number = Convert.ToDecimal(constant.Value, CultureInfo.InvariantCulture);
            if (target.TypeKind == TypeKind.Enum)
            {
                return number == 0;
            }

            if (constant.Type.SpecialType == SpecialType.System_Int64)
            {
                return target.SpecialType == SpecialType.System_UInt64 && number >= 0;
            }

            switch (target.SpecialType)
            {
                case SpecialType.System_SByte:
                    return number >= sbyte.MinValue && number <= sbyte.MaxValue;
                case SpecialType.System_Byte:
                    return number >= byte.MinValue && number <= byte.MaxValue;
                case SpecialType.System_Int16:
                    return number >= short.MinValue && number <= short.MaxValue;
                case SpecialType.System_UInt16:
                    return number >= ushort.MinValue && number <= ushort.MaxValue;
                case SpecialType.System_UInt32:
                    return number >= 0;
                case SpecialType.System_UInt64:
                    return number >= 0;
                default:
                    return false;
            }
        }

        private static string AttributeConstant(TypedConstant constant, ISymbol declaration)
        {
            if (constant.IsNull)
            {
                return "null";
            }

            if (constant.Kind != TypedConstantKind.Primitive && constant.Kind != TypedConstantKind.Enum)
            {
                Fail(declaration, "NullValue must be a primitive, enum or null constant.");
            }

            if (constant.Value is float single && (float.IsNaN(single) || float.IsInfinity(single)))
            {
                return "global::System.Single." + (float.IsNaN(single) ? "NaN" : single > 0 ? "PositiveInfinity" : "NegativeInfinity");
            }

            if (constant.Value is double number && (double.IsNaN(number) || double.IsInfinity(number)))
            {
                return "global::System.Double." + (double.IsNaN(number) ? "NaN" : number > 0 ? "PositiveInfinity" : "NegativeInfinity");
            }

            return "(" + TypeName(constant.Type) + ")(" + Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatPrimitive(constant.Value, true, false) + ")";
        }

        private readonly struct PathSource
        {
            internal PathSource(string name, ITypeSymbol type, bool readable, bool writable, string expression, string definition)
            {
                Name = name;
                Type = type;
                Readable = readable;
                Writable = writable;
                Expression = expression;
                Definition = definition;
            }

            internal string Name
            {
                get;
            }

            internal ITypeSymbol Type
            {
                get;
            }

            internal bool Readable
            {
                get;
            }

            internal bool Writable
            {
                get;
            }

            internal string Expression
            {
                get;
            }

            internal string Definition
            {
                get;
            }
        }
    }
}
