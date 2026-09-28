using MUI.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace MUI.CodeStyle;

internal static class Program
{
    private static int Main(string[] args)
    {
        var check = args.Contains("--check");
        var checkBraces = args.Contains("--braces");
        var root = args.FirstOrDefault(a => a != "--check" && a != "--braces") ?? throw new ArgumentException("需要仓库目录。");
        var changed = 0;
        var conflicts = 0;
        var braceViolations = 0;
        foreach (var folder in new[] { "Runtime", "Editor", "Samples~", "Generators~", "Tools~/CodeStyle" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
            {
                if (file.Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj"))
                {
                    continue;
                }

                var source = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(source);
                if (tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error))
                {
                    Console.Error.WriteLine("语法无法解析：" + file);
                    return 2;
                }

                if (checkBraces)
                {
                    foreach (var statement in tree.GetRoot().DescendantNodes().OfType<StatementSyntax>())
                    {
                        var body = GetControlBody(statement);
                        if (body == null || body is BlockSyntax ||
                            (statement is UsingStatementSyntax && body is UsingStatementSyntax))
                        {
                            continue;
                        }

                        var line = body.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        Console.Error.WriteLine($"控制流缺少大括号：{Path.GetRelativePath(root, file)}:{line}");
                        braceViolations++;
                    }

                    foreach (var clause in tree.GetRoot().DescendantNodes().OfType<ElseClauseSyntax>())
                    {
                        if (clause.Statement is BlockSyntax or IfStatementSyntax)
                        {
                            continue;
                        }

                        var line = clause.Statement.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        Console.Error.WriteLine($"else 缺少大括号：{Path.GetRelativePath(root, file)}:{line}");
                        braceViolations++;
                    }
                }

                var rewriter = new MemberLayout();
                var arranged = rewriter.Visit(tree.GetRoot())!;
                var rewritten = CollapseBlankLines(arranged.ToFullString());
                // 缩进交给现有 dotnet format；检查只比较去除行首缩进后的成员结构。
                var normalized = string.Join("\n", source.Split('\n').Select(line => line.TrimStart()));
                var nextNormalized = string.Join("\n", rewritten.Split('\n').Select(line => line.TrimStart()));
                foreach (var conflict in rewriter.Conflicts)
                {
                    Console.Error.WriteLine(Path.GetRelativePath(root, file) + "：" + conflict);
                    conflicts++;
                }

                if (normalized == nextNormalized)
                {
                    continue;
                }

                changed++;
                if (check)
                {
                    Console.Error.WriteLine("成员布局不符合规范：" + Path.GetRelativePath(root, file));
                }
                else
                {
                    File.WriteAllText(file, rewritten);
                }
            }
        }

        Console.WriteLine($"成员布局：{changed} 个文件{(check ? "需要调整" : "已调整")}，{conflicts} 个需人工处理项；控制流大括号：{braceViolations} 个违规项。");
        return conflicts > 0 || braceViolations > 0 || (check && changed > 0) ? 1 : 0;
    }

    private static StatementSyntax GetControlBody(StatementSyntax statement) => statement switch
    {
        IfStatementSyntax node => node.Statement,
        ForStatementSyntax node => node.Statement,
        ForEachStatementSyntax node => node.Statement,
        ForEachVariableStatementSyntax node => node.Statement,
        WhileStatementSyntax node => node.Statement,
        DoStatementSyntax node => node.Statement,
        UsingStatementSyntax node => node.Statement,
        LockStatementSyntax node => node.Statement,
        FixedStatementSyntax node => node.Statement,
        _ => null
    };

    private static string CollapseBlankLines(string source)
    {
        var text = SourceText.From(source);
        var root = CSharpSyntaxTree.ParseText(text).GetRoot();
        var changes = new List<TextChange>();
        var blankLines = 0;
        for (var lineIndex = 0; lineIndex < text.Lines.Count; lineIndex++)
        {
            var line = text.Lines[lineIndex];
            if (!string.IsNullOrWhiteSpace(text.ToString(line.Span)))
            {
                blankLines = 0;
                continue;
            }

            var position = line.Start;
            var token = root.FindToken(position, findInsideTrivia: true);
            var trivia = root.FindTrivia(position, findInsideTrivia: true);
            // 字符串和保留原文的注释/条件编译区可能含有有意的空行。
            if (token.Span.Contains(position) ||
                (trivia.Span.Contains(position) &&
                    (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) || trivia.IsKind(SyntaxKind.DisabledTextTrivia))))
            {
                blankLines = 0;
                continue;
            }

            var nextIndex = lineIndex + 1;
            while (nextIndex < text.Lines.Count && string.IsNullOrWhiteSpace(text.ToString(text.Lines[nextIndex].Span)))
            {
                nextIndex++;
            }

            var beforeClose = nextIndex < text.Lines.Count &&
                text.ToString(text.Lines[nextIndex].Span).TrimStart().StartsWith("}", StringComparison.Ordinal);
            if ((++blankLines > 1 || beforeClose) && line.SpanIncludingLineBreak.Length != 0)
            {
                changes.Add(new TextChange(line.SpanIncludingLineBreak, string.Empty));
            }
        }

        return changes.Count == 0 ? source : text.WithChanges(changes).ToString();
    }
}
