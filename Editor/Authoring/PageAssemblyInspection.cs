using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    /// <summary>读取目标路径与 Unity 当前编译快照，不安装生成器、不修改项目程序集。</summary>
    internal sealed class PageAssemblyInspection
    {
        internal string AssemblyName
        {
            get; private set;
        }

        internal string DefinitionPath
        {
            get; private set;
        }

        internal string Error
        {
            get; private set;
        }

        internal string Warning
        {
            get; private set;
        }

        internal bool Verified
        {
            get; private set;
        }

        internal static PageAssemblyInspection Capture(string scriptPath, Type titleElementType)
        {
            var result = new PageAssemblyInspection();
            try
            {
                result.Read(scriptPath, titleElementType);
            }
            catch (Exception error)
            {
                result.Error = L.Get("editor.PageAssemblyInspection.11f11aca60") + error.Message;
            }
            return result;
        }

        private void Read(string scriptPath, Type titleElementType)
        {
            // 新页面目录尚未存在，从最近已有的父目录查找显式定义或引用。
            var directory = Path.GetDirectoryName(scriptPath);
            while (!string.IsNullOrEmpty(directory))
            {
                if (Directory.Exists(directory))
                {
                    var definitions = Directory.GetFiles(directory, "*.asmdef", SearchOption.TopDirectoryOnly);
                    var references = Directory.GetFiles(directory, "*.asmref", SearchOption.TopDirectoryOnly);
                    if (definitions.Length + references.Length > 1)
                    {
                        throw new InvalidOperationException(L.Get("editor.PageAssemblyInspection.0015a79466") + directory);
                    }
                    if (definitions.Length == 1)
                    {
                        DefinitionPath = definitions[0].Replace('\\', '/');
                        break;
                    }
                    if (references.Length == 1)
                    {
                        var reference = JsonUtility.FromJson<Definition>(File.ReadAllText(references[0]));
                        if (reference == null || string.IsNullOrEmpty(reference.reference))
                        {
                            throw new InvalidOperationException(L.Get("editor.PageAssemblyInspection.c754dd93d7"));
                        }
                        DefinitionPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyReference(reference.reference);
                        if (string.IsNullOrEmpty(DefinitionPath))
                        {
                            throw new InvalidOperationException(L.Get("editor.PageAssemblyInspection.840967a1d8") + references[0]);
                        }
                        break;
                    }
                }
                if (directory.Replace('\\', '/') == "Assets")
                {
                    break;
                }
                directory = Path.GetDirectoryName(directory);
            }

            if (!string.IsNullOrEmpty(DefinitionPath))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEditorInternal.AssemblyDefinitionAsset>(DefinitionPath);
                // 包资产不一定能通过工程相对路径直接读取，优先使用 AssetDatabase 的文本。
                var json = asset == null ? File.ReadAllText(DefinitionPath) : asset.text;
                var definition = JsonUtility.FromJson<Definition>(json);
                if (definition == null || string.IsNullOrWhiteSpace(definition.name))
                {
                    throw new InvalidOperationException(L.Get("editor.PageAssemblyInspection.e80c02e5ee"));
                }
                AssemblyName = definition.name;
                if (definition.noEngineReferences)
                {
                    Error = L.Get("editor.PageAssemblyInspection.c7e47c0af2");
                    return;
                }
                if (definition.includePlatforms != null && definition.includePlatforms.Length == 1 &&
                    definition.includePlatforms[0] == "Editor")
                {
                    Error = L.Get("editor.PageAssemblyInspection.38e6e62259");
                    return;
                }
            }
            else
            {
                AssemblyName = CompilationPipeline.GetAssemblyNameFromScriptPath(scriptPath);
                if (!string.IsNullOrEmpty(AssemblyName) && AssemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    AssemblyName = AssemblyName.Substring(0, AssemblyName.Length - 4);
                }
            }

            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
            var assembly = Array.Find(assemblies, item => item.name == AssemblyName);
            if (assembly == null)
            {
                Warning = L.Get("editor.PageAssemblyInspection.e652119992");
                return;
            }
            if ((assembly.flags & AssemblyFlags.EditorAssembly) != 0)
            {
                Error = L.Get("editor.PageAssemblyInspection.5d3a39daa3");
                return;
            }

            var available = new HashSet<string>(StringComparer.Ordinal) { assembly.name };
            foreach (var path in assembly.allReferences)
            {
                available.Add(Path.GetFileNameWithoutExtension(path));
            }
            var required = new HashSet<string>(StringComparer.Ordinal)
            {
                "MUI.Core", "MUI.Resources", "MUI.Navigation", "MUI.UGUI", titleElementType.Assembly.GetName().Name
            };
            var errors = new List<string>();
            foreach (var name in required)
            {
                if (!available.Contains(name))
                {
                    errors.Add(L.Get("editor.PageAssemblyInspection.210d456125") + name);
                }
            }

            var generatorFound = false;
            var analyzers = assembly.compilerOptions == null ? null : assembly.compilerOptions.RoslynAnalyzerDllPaths;
            if (analyzers != null)
            {
                foreach (var path in analyzers)
                {
                    if (string.Equals(Path.GetFileName(path), "MUI.Generators.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        generatorFound = true;
                        break;
                    }
                }
            }
            if (!generatorFound)
            {
                errors.Add(L.Get("editor.PageAssemblyInspection.2737b0424d"));
            }
            errors.Sort(StringComparer.Ordinal);
            Error = errors.Count == 0 ? null : string.Join("\n", errors);
            Verified = errors.Count == 0;
        }

        [Serializable]
        private sealed class Definition
        {
            public string name = null;
            public string reference = null;
            public string[] includePlatforms = null;
            public bool noEngineReferences = false;
        }
    }
}
