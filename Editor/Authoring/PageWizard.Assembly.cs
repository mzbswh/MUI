using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using L = MUI.Editor.Localization.MUIEditorLocalization;

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
            EditorGUILayout.LabelField(L.Get("editor.PageWizard.Assembly.aeb3fe5fac"), inspection.AssemblyName ?? L.Get("editor.PageWizard.Assembly.4eaf225048"));
            if (!string.IsNullOrEmpty(inspection.DefinitionPath))
            {
                EditorGUILayout.LabelField(L.Get("editor.PageWizard.Assembly.06a3c25a58"), inspection.DefinitionPath);
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
                EditorGUILayout.HelpBox(L.Get("editor.PageWizard.Assembly.b3f8667ef3"), MessageType.Info);
            }
            if (GUILayout.Button(L.Get("editor.PageWizard.Assembly.3a7158af88")))
            {
                InvalidateAssemblyInspection();
            }
            return inspection.Error;
        }
    }
}
