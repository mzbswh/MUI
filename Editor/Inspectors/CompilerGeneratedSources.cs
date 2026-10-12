using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;

namespace MUI.Editor
{
    [InitializeOnLoad]
    internal sealed class CompilerGeneratedSources : AssetPostprocessor
    {
        private const string OutputDirectory = "Library/MUI/CompilerGenerated";
        private const string GeneratorDirectory = "MUI.Generators/MUI.Generators.UIGenerator";
        private const string PendingProjectSync = "MUI.PendingProjectSync";
        private static double syncAfter;
        private static NamedBuildTarget Target => NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);

        static CompilerGeneratedSources()
        {
            CompilationPipeline.assemblyCompilationFinished += (path, messages) =>
            {
                var assemblyName = Path.GetFileNameWithoutExtension(path);
                var failed = messages.Any(message => message.type == CompilerMessageType.Error);
                SessionState.SetBool("MUI.GeneratedSourceFailure." + assemblyName, failed);
            };
            CompilationPipeline.compilationFinished += _ =>
            {
                if (ConfiguredDirectory() == null) return;
                RequestProjectSync();
            };
            // 编译完成后可能立即域重载，不能只依赖旧域里的 delayCall。
            if (SessionState.GetBool(PendingProjectSync, false)) RequestProjectSync();
            EditorApplication.projectChanged += RequestApplySettings;
            Undo.undoRedoPerformed += RequestApplySettings;
            RequestApplySettings();
        }

        internal static string AssemblyPrefix(string name) => "_" + string.Concat(name.Select(c => char.IsLetterOrDigit(c) ? c.ToString() : "_" + ((int)c).ToString("X4"))) + ".";

        internal static void RequestProjectSync()
        {
            SessionState.SetBool(PendingProjectSync, true);
            syncAfter = EditorApplication.timeSinceStartup + 0.5;
            EditorApplication.update -= SyncProjects;
            EditorApplication.update += SyncProjects;
        }

        private static void SyncProjects()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                syncAfter = EditorApplication.timeSinceStartup + 0.5;
                return;
            }
            if (EditorApplication.timeSinceStartup < syncAfter) return;
            EditorApplication.update -= SyncProjects;
            SessionState.SetBool(PendingProjectSync, false);
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
        }

        private static string ConfiguredDirectory()
        {
            var arguments = PlayerSettings.GetAdditionalCompilerArguments(Target) ?? Array.Empty<string>();
            var argument = arguments.LastOrDefault(value => value.StartsWith("-generatedfilesout:", StringComparison.OrdinalIgnoreCase) || value.StartsWith("/generatedfilesout:", StringComparison.OrdinalIgnoreCase));
            return argument == null ? null : Path.GetFullPath(argument.Substring(argument.IndexOf(':') + 1).Trim('"'));
        }

        internal static bool OutputEnabled => MUIProjectSettings.TryGet(out var settings, out _) &&
            (settings == null || settings.OutputCompilerGeneratedSources);

        internal static void RequestApplySettings()
        {
            EditorApplication.update -= ApplySettings;
            EditorApplication.update += ApplySettings;
        }

        private static void ApplySettings()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= ApplySettings;
            if (!MUIProjectSettings.TryGet(out var settings, out _)) return;
            var enabled = settings == null || settings.OutputCompilerGeneratedSources;
            var arguments = PlayerSettings.GetAdditionalCompilerArguments(Target) ?? Array.Empty<string>();
            var next = arguments;
            if (enabled)
            {
                if (ConfiguredDirectory() == null)
                    next = arguments.Concat(new[] { "-generatedfilesout:\"" + OutputDirectory + "\"" }).ToArray();
                Directory.CreateDirectory(ConfiguredDirectory() ?? Path.GetFullPath(OutputDirectory));
            }
            else
            {
                // 只移除 MUI 的输出参数，保留项目自行配置的其他生成器输出目录。
                next = arguments.Where(value => !IsMUIOutputArgument(value)).ToArray();
            }
            var stateKey = "MUI.GeneratedSources.Enabled." + Target.TargetName;
            var changed = !arguments.SequenceEqual(next);
            if (SessionState.GetBool(stateKey, !enabled) != enabled || changed) RequestProjectSync();
            SessionState.SetBool(stateKey, enabled);
            if (!changed) return;
            PlayerSettings.SetAdditionalCompilerArguments(Target, next);
            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
        }

        private static bool IsMUIOutputArgument(string value)
        {
            if (!value.StartsWith("-generatedfilesout:", StringComparison.OrdinalIgnoreCase) &&
                !value.StartsWith("/generatedfilesout:", StringComparison.OrdinalIgnoreCase)) return false;
            return string.Equals(Path.GetFullPath(value.Substring(value.IndexOf(':') + 1).Trim('"')),
                Path.GetFullPath(OutputDirectory), StringComparison.Ordinal);
        }

        internal static void Regenerate()
        {
            if (!OutputEnabled) return;
            RequestApplySettings();
            RequestProjectSync();
            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
        }

        internal static string Find(string sourcePath)
        {
            if (!OutputEnabled) return null;
            // Do not search old snapshot exports or guess among multiple assemblies.
            var directory = ConfiguredDirectory();
            if (directory == null) return null;
            var path = Path.Combine(directory, GeneratorDirectory, Path.GetFileName(sourcePath));
            return File.Exists(path) ? path : null;
        }

        // Standard Unity project generation callback, independent of the selected IDE.
        private static string OnGeneratedCSProject(string path, string content)
        {
            if (!OutputEnabled) return content;
            var directory = ConfiguredDirectory();
            if (directory == null) return content;
            var document = XDocument.Parse(content, LoadOptions.PreserveWhitespace);
            var root = document.Root;
            if (root == null) return content;
            var ns = root.Name.Namespace;
            var analyzers = root.Descendants(ns + "Analyzer").Where(item =>
                Path.GetFileName(((string)item.Attribute("Include") ?? "").Replace('\\', '/')) == "MUI.Generators.dll").ToArray();
            if (analyzers.Length == 0) return content;
            var assemblyName = root.Descendants(ns + "AssemblyName").Select(item => item.Value).FirstOrDefault() ?? Path.GetFileNameWithoutExtension(path);
            var output = Path.Combine(directory, GeneratorDirectory);
            if (SessionState.GetBool("MUI.GeneratedSourceFailure." + assemblyName, false)) return content;
            var index = Path.Combine(output, AssemblyPrefix(assemblyName) + "SourceIndex.g.cs");
            if (!File.Exists(index)) return content;
            const string marker = "// MUI-SOURCE: ";
            var files = File.ReadLines(index).Where(line => line.StartsWith(marker, StringComparison.Ordinal))
                .Select(line => line.Substring(marker.Length)).Where(name => Path.GetFileName(name) == name && name.StartsWith(AssemblyPrefix(assemblyName), StringComparison.Ordinal))
                .Select(name => Path.Combine(output, name)).Distinct().ToArray();
            if (files.Any(file => !File.Exists(file))) return content;
            // Keep IDE generation until the first successful Unity build has materialized its output.
            // Never remove another analyzer, and never compile these files twice in the IDE project.
            if (files.Length == 0) return content;
            foreach (var analyzer in analyzers) analyzer.Remove();
            var group = new XElement(ns + "ItemGroup");
            foreach (var file in files.OrderBy(value => value, StringComparer.Ordinal))
                group.Add(new XElement(ns + "Compile", new XAttribute("Include", file)));
            root.Add(group);
            return document.ToString(SaveOptions.DisableFormatting);
        }
    }
}
