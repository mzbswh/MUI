using MUI.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace MUI.CodeStyle;

internal static class Program
{
    private static int Main(string[] args)
    {
        var check = args.Contains("--check");
        var root = args.FirstOrDefault(a => a != "--check") ?? throw new ArgumentException("需要仓库目录。");
        var changed = 0;
        var conflicts = 0;
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

        Console.WriteLine($"成员布局：{changed} 个文件{(check ? "需要调整" : "已调整")}，{conflicts} 个需人工处理项。");
        return conflicts > 0 || (check && changed > 0) ? 1 : 0;
    }

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
