using System;
using System.Collections.Generic;
using System.Text;

namespace MUI.Editor
{
    internal static class PageSourceTemplate
    {
        private static readonly HashSet<string> Keywords = new HashSet<string>(
            ("abstract as base bool break byte case catch char checked class const continue decimal default delegate " +
             "do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int " +
             "interface internal is lock long namespace new null object operator out override params private protected " +
             "public readonly ref return sbyte sealed short sizeof stackalloc static string struct switch this throw " +
             "true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while").Split(' '), StringComparer.Ordinal);

        internal static string EscapeQualifiedName(string value)
        {
            var parts = value.Split('.');
            for (var i = 0; i < parts.Length; i++)
                if (Keywords.Contains(parts[i]))
                    parts[i] = "@" + parts[i];
            return string.Join(".", parts);
        }

        internal static bool IsIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || !IsStart(value[0]))
            {
                return false;
            }

            foreach (var c in value)
            {
                if (!IsStart(c) && (c < '0' || c > '9'))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsStart(char c)
        {
            return c == '_' || c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z';
        }

        internal static bool IsNamespace(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            foreach (var part in value.Split('.'))
            {
                if (!IsIdentifier(part))
                {
                    return false;
                }
            }

            return true;
        }

        internal static string Policy(string preset)
        {
            return "global::MUI.UGUI.MUISettings.ResolveGlobalPolicy(" + StringLiteral(preset) + ")";
        }

        /// <summary>保留 Element 原名，转义为合法 C# 字符串，不将节点名称当作源码拼接。</summary>
        internal static string StringLiteral(string value)
        {
            var result = new StringBuilder("\"");
            foreach (var character in value)
            {
                if (character == '\\' || character == '\"')
                {
                    result.Append('\\').Append(character);
                }
                else if (char.IsControl(character) || char.IsSurrogate(character) || character == '\u2028' || character == '\u2029')
                {
                    result.Append("\\u").Append(((int)character).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                }
                else
                {
                    result.Append(character);
                }
            }
            return result.Append('\"').ToString();
        }

        /// <summary>生成归项目维护的页面源码；与绑定生成器输出的代码分开管理。</summary>
        internal static IReadOnlyDictionary<string, string> Create(
            string name,
            string ns,
            string preset,
            bool receivesInput,
            bool presenter,
            bool typed,
            Type titleElementType = null,
            string titleElementName = "Title", string closeElementName = "Close", string resourceKey = null, bool autoBindings = false, string defaultPreset = "Default")
        {
            if (!IsIdentifier(name) || !IsNamespace(ns))
            {
                throw new ArgumentException("Invalid page name or namespace.");
            }

            if (string.IsNullOrWhiteSpace(preset))
            {
                throw new ArgumentOutOfRangeException(nameof(preset));
            }

            titleElementType = titleElementType ?? typeof(MUI.UGUI.NativeText);
            PageTextBackend.RequireElementType(titleElementType);
            var titleContract = typeof(ITextElement).IsAssignableFrom(titleElementType)
                ? typeof(ITextElement) : titleElementType;
            var titleTypeName = "global::" + EscapeQualifiedName(titleContract.FullName);
            if (string.IsNullOrWhiteSpace(titleElementName) ||
                receivesInput && string.IsNullOrWhiteSpace(closeElementName))
            {
                throw new ArgumentException("页面骨架需要标题和关闭按钮的 Element 名称。");
            }
            var titleName = StringLiteral(titleElementName);
            var closeName = !receivesInput ? null : StringLiteral(closeElementName);
            var escapedNamespace = EscapeQualifiedName(ns);
            string Wrap(params string[] lines)
            {
                return "// 由 MUI 页面向导创建。此源码归项目所有，可以自由修改。\n" +
                    "namespace " + escapedNamespace + "\n{\n" + string.Join("\n", lines) + "\n}\n";
            }

            var buttonType = "global::MUI.IButtonElement";
            var args = typed ? name + "Args" : "global::MUI.Unit";
            var result = "global::MUI.Unit";
            var pageOptions = new List<string>();
            if (!presenter)
                pageOptions.Add($"typeof({args}), typeof({result})");
            if (resourceKey != null && resourceKey != (name.EndsWith("View", StringComparison.Ordinal) ? name : name + "View"))
                pageOptions.Add($"ResourceKey = {StringLiteral(resourceKey)}");
            if (preset != defaultPreset)
                pageOptions.Add($"PolicyPreset = {StringLiteral(preset)}");
            var pageDeclaration = pageOptions.Count == 0 ? "    [global::MUI.UIPage]"
                : "    [global::MUI.UIPage(" + string.Join(", ", pageOptions) + ")]";

            var files = new Dictionary<string, string>(StringComparer.Ordinal);
            var modelLines = new List<string>
            {
                "    /// <summary>页面表现状态，通过生成绑定连接到预制控件。</summary>",
                pageDeclaration,
                $"    public partial class {name}ViewModel : global::MUI.ViewModel",
                "    {"
            };
            if (!autoBindings)
                modelLines.AddRange(new[]
            {
                "        /// <summary>页面标题，由生成的 Title 属性发布变化。</summary>",
                "        [global::MUI.ObservableProperty]",
                $"        [global::MUI.Bind({titleName}, nameof({titleTypeName}.Content),",
                $"            ElementType = typeof({titleTypeName}))]",
                $"        private string title = \"{name}\";"
            });
            if (receivesInput)
            {
                modelLines.Add("        /// <summary>请求关闭本次激活，不等待自身命令清理。</summary>");
                if (autoBindings)
                {
                    modelLines.Add("        partial void OnClose(global::MUI.CommandContext context)");
                }
                else
                {
                    modelLines.Add("        [global::MUI.Command]");
                    modelLines.Add($"        [global::MUI.BindCommand({closeName}, nameof({buttonType}.Clicked),");
                    modelLines.Add($"            ElementType = typeof({buttonType}))]");
                    modelLines.Add("        private void Close(global::MUI.CommandContext context)");
                }
                modelLines.AddRange(new[]
                {
                    "        {",
                    "            context.RequestClose();",
                    "        }"
                });
            }
            modelLines.Add("    }");
            files.Add(name + "ViewModel.cs", Wrap(modelLines.ToArray()));

            if (typed)
            {
                if (!presenter)
                    files.Add(name + "Args.cs", Wrap(
                    "    /// <summary>本次打开页面的参数，按项目需求扩展。</summary>",
                    $"    public readonly struct {name}Args",
                    "    {",
                    $"        public {name}Args(string title)",
                    "        {",
                    "            Title = title;",
                    "        }",
                    "",
                    "        /// <summary>页面初始标题。</summary>",
                    "        public string Title { get; }",
                    "    }"));
            }

            if (presenter)
            {
                files.Add(name + "Presenter.cs", "using MUI;\n\n" + Wrap(
                    "    /// <summary>接入页面生命周期，业务数据写入 ViewModel。</summary>",
                    $"    public sealed partial class {name}Presenter",
                    "    {",
                    "        /// <summary>显示前初始化；需要等待加载时改为 async Task 或 async ValueTask，框架会等待完成再显示。</summary>",
                    "        [OpenEntry]",
                    typed
                        ? "        private void OnPrepareOpen(string title)"
                        : "        private void OnPrepareOpen()",
                    "        {",
                    typed
                        ? "            VM.Title = title ?? string.Empty;"
                        : "            // 需要等待数据加载时，可改为 async ValueTask 并声明 CancellationToken 参数。",
                    "        }",
                    "    }"));
            }

            return files;
        }
    }
}
