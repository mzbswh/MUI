using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace MUI.Editor
{
    public sealed partial class PageWizard
    {
        private PageAssemblyInspection assemblyInspection;
        private (string Path, string Backend)? assemblyInspectionKey;

        private void OnEnable()
        {
            EditorApplication.projectChanged += InvalidateAssemblyInspection;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= InvalidateAssemblyInspection;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
        }

        private void OnCompilationFinished(object context) => InvalidateAssemblyInspection();

        private void InvalidateAssemblyInspection()
        {
            InvalidateExistingValidation();
            assemblyInspection = null;
            assemblyInspectionKey = null;
            Repaint();
        }

        private PageAssemblyInspection InspectAssembly(bool refresh = false)
        {
            var backend = PageTextBackend.Find(textBackendId);
            var scriptPath = destination + "/" + pageName + "/Scripts/" + pageName + "ViewModel.cs";
            var key = (scriptPath, textBackendId);
            if (refresh || assemblyInspection == null || !assemblyInspectionKey.HasValue || assemblyInspectionKey.Value != key)
            {
                assemblyInspection = PageAssemblyInspection.Capture(scriptPath, backend.ElementType);
                assemblyInspectionKey = key;
            }
            return assemblyInspection;
        }

        private string DrawAssemblyInspection()
        {
            var inspection = InspectAssembly();
            EditorGUILayout.LabelField("目标程序集", inspection.AssemblyName ?? "尚未确定");
            if (!string.IsNullOrEmpty(inspection.DefinitionPath))
            {
                EditorGUILayout.LabelField("程序集定义", inspection.DefinitionPath);
            }
            if (inspection.Error != null)
            {
                EditorGUILayout.HelpBox(inspection.Error, MessageType.Error);
            }
            else if (inspection.Warning != null)
            {
                EditorGUILayout.HelpBox(inspection.Warning, MessageType.Warning);
            }
            else if (inspection.Verified)
            {
                EditorGUILayout.HelpBox("当前编译快照包含所需引用和绑定生成器；生成源码仍需通过实际编译。", MessageType.Info);
            }
            if (GUILayout.Button("重新检查程序集配置"))
            {
                InvalidateAssemblyInspection();
            }
            return inspection.Error;
        }
    }
}
