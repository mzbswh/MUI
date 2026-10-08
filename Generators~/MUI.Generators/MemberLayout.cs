using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MUI.Generators
{
    /// <summary>按语法节点整理成员，不使用正则表达式搬动源码或注释。</summary>
    internal sealed class MemberLayout : CSharpSyntaxRewriter
    {
        private readonly List<string> conflicts = new List<string>();

        public IReadOnlyList<string> Conflicts => conflicts;

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var visited = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
            return visited.WithMembers(Arrange(visited.Members, node.Identifier.Text));
        }

        public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax node)
        {
            var visited = (StructDeclarationSyntax)base.VisitStructDeclaration(node)!;
            return visited.WithMembers(Arrange(visited.Members, node.Identifier.Text));
        }

        public override SyntaxNode VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            var visited = (InterfaceDeclarationSyntax)base.VisitInterfaceDeclaration(node)!;
            return visited.WithMembers(Arrange(visited.Members, node.Identifier.Text));
        }

        private SyntaxList<MemberDeclarationSyntax> Arrange(SyntaxList<MemberDeclarationSyntax> members, string name)
        {
            var ordered = members.OrderBy(Rank).ToArray();
            // 字段之间保持原顺序；自动属性初始化器与字段初始化器也不得交换执行顺序。
            if (!members.Where(HasInitializer).SequenceEqual(ordered.Where(HasInitializer)))
            {
                conflicts.Add(name + "：成员重排会改变初始化顺序，需人工处理。");
                ordered = members.ToArray();
            }
            else if (!members.SequenceEqual(ordered) && members.Any(HasBoundaryDirective))
            {
                conflicts.Add(name + "：成员边界含条件编译或区域指令，需人工处理。");
                ordered = members.ToArray();
            }

            for (var i = 0; i < ordered.Length; i++)
            {
                var member = ordered[i];
                var leading = member.GetLeadingTrivia().ToList();
                var indentation = default(SyntaxTrivia);
                while (leading.Count > 0 && (leading[0].IsKind(SyntaxKind.WhitespaceTrivia) ||
                    leading[0].IsKind(SyntaxKind.EndOfLineTrivia)))
                {
                    indentation = leading[0].IsKind(SyntaxKind.WhitespaceTrivia) ? leading[0] : default;
                    leading.RemoveAt(0);
                }

                if (indentation.RawKind != 0)
                {
                    leading.Insert(0, indentation);
                }

                // 相邻字段同组，其余成员及组边界用一个空行隔开；保留附属于成员的注释。
                var blank = i > 0 && !(Rank(ordered[i - 1]) == 0 && Rank(member) == 0);
                if (blank)
                {
                    leading.Insert(0, SyntaxFactory.EndOfLine("\n"));
                }

                ordered[i] = member.WithLeadingTrivia(leading);
            }

            return SyntaxFactory.List(ordered);
        }

        private static int Rank(MemberDeclarationSyntax member)
        {
            if (member is FieldDeclarationSyntax)
            {
                return 0;
            }
            if (member is ConstructorDeclarationSyntax || member is DestructorDeclarationSyntax)
            {
                return 1;
            }
            if (member is EventFieldDeclarationSyntax || member is EventDeclarationSyntax)
            {
                return 2;
            }
            if (member is PropertyDeclarationSyntax || member is IndexerDeclarationSyntax)
            {
                return 3;
            }
            if (member is BaseTypeDeclarationSyntax || member is DelegateDeclarationSyntax)
            {
                return 5;
            }
            return 4;
        }

        private static bool HasInitializer(MemberDeclarationSyntax member)
        {
            if (member is FieldDeclarationSyntax field)
            {
                return field.Declaration.Variables.Any(v => v.Initializer != null);
            }
            if (member is EventFieldDeclarationSyntax eventField)
            {
                return eventField.Declaration.Variables.Any(v => v.Initializer != null);
            }
            return member is PropertyDeclarationSyntax property && property.Initializer != null;
        }

        private static bool HasBoundaryDirective(MemberDeclarationSyntax member) =>
            member.GetLeadingTrivia().Any(t => t.IsDirective) || member.GetTrailingTrivia().Any(t => t.IsDirective);
    }
}
