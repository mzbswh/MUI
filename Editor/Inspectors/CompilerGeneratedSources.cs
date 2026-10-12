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
        private static NamedBuildTarget Target => NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);

        static CompilerGeneratedSources()
        {
            CompilationPipeline.assemblyCompilationFinished += (path, messages) =>
                SessionState.SetBool("MUI.GeneratedSourceFailure." + Path.GetFileNameWithoutExtension(path), messages.Any(message => message.type == CompilerMessageType.Error));
            CompilationPipeline.compilationFinished += _ =>
            {
                if (ConfiguredDirectory() == null) return;
                EditorApplication.delayCall -= SyncProjects;
                EditorApplication.delayCall += SyncProjects;
            };
        }

        internal static string AssemblyPrefix(string name) => "_" + string.Concat(name.Select(c => char.IsLetterOrDigit(c) ? c.ToString() : "_" + ((int)c).ToString("X4"))) + ".";

        private static void SyncProjects() => Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();

        private static string ConfiguredDirectory()
        {
            var arguments = PlayerSettings.GetAdditionalCompilerArguments(Target) ?? Array.Empty<string>();
            var argument = arguments.LastOrDefault(value => value.StartsWith("-generatedfilesout:", StringComparison.OrdinalIgnoreCase) || value.StartsWith("/generatedfilesout:", StringComparison.OrdinalIgnoreCase));
            return argument == null ? null : Path.GetFullPath(argument.Substring(argument.IndexOf(':') + 1).Trim('"'));
        }

        [MenuItem("Tools/MUI/Enable Compiler Generated Sources")]
        internal static void Enable()
        {
            var directory = ConfiguredDirectory();
            if (directory == null)
            {
                directory = Path.GetFullPath(OutputDirectory);
                var arguments = PlayerSettings.GetAdditionalCompilerArguments(Target) ?? Array.Empty<string>();
                PlayerSettings.SetAdditionalCompilerArguments(Target, arguments.Concat(new[] { "-generatedfilesout:\"" + OutputDirectory + "\"" }).ToArray());
            }
            Directory.CreateDirectory(directory);
            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
        }

        internal static string Find(string sourcePath)
        {
            // Do not search old snapshot exports or guess among multiple assemblies.
            var directory = ConfiguredDirectory();
            if (directory == null) return null;
            var path = Path.Combine(directory, GeneratorDirectory, Path.GetFileName(sourcePath));
            return File.Exists(path) ? path : null;
        }

        // Standard Unity project generation callback, independent of the selected IDE.
        private static string OnGeneratedCSProject(string path, string content)
        {
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
