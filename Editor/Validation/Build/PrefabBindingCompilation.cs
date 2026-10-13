using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;

namespace MUI.Editor
{
    // Prefab declarations are compiler inputs in Unity as well as in command-line builds.
    [InitializeOnLoad]
    internal static class PrefabBindingCompilation
    {
        private const string PendingKey = "MUI.PrefabBindingCompilation.Pending";

        static PrefabBindingCompilation()
        {
            if (SessionState.GetBool(PendingKey, false)) Request();
        }

        internal static void Request()
        {
            SessionState.SetBool(PendingKey, true);
            EditorApplication.update -= CompileWhenIdle;
            EditorApplication.update += CompileWhenIdle;
        }

        internal static void Changed(string[] paths, bool deleted = false)
        {
            foreach (var path in paths.Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)))
            {
                var key = "MUI.PrefabBindingCompilation." + path;
                var previous = SessionState.GetString(key, "");
                var signature = "";
                if (!deleted && File.Exists(path))
                {
                    var content = File.ReadAllText(path);
                    if (content.Split('\n').Any(line => line.TrimStart().StartsWith("muiAutoBindings: ", StringComparison.Ordinal) &&
                        line.Trim().Length > "muiAutoBindings:".Length))
                        using (var hash = SHA256.Create())
                            signature = Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(content)));
                }
                if (signature == previous) continue;
                SessionState.SetString(key, signature);
                Request();
            }
        }

        private static void CompileWhenIdle()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= CompileWhenIdle;
            SessionState.SetBool(PendingKey, false);
            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
        }
    }
}
