using System;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public sealed partial class ViewInspector
    {
        private bool showResourcePreparation;
        private ViewResourcePreparationSnapshot resourcePreparationSnapshot;
        private string resourcePreparationError;
        private string resourcePreparationTime;

        private void DrawResourcePreparationDiagnostics()
        {
            showResourcePreparation = EditorGUILayout.Foldout(showResourcePreparation, L.Get("editor.ViewInspector.ResourcePreparation.cc887fc419"), true);
            if (!showResourcePreparation)
            {
                return;
            }

            if (GUILayout.Button(L.Get("editor.ViewInspector.ResourcePreparation.8ef0bbbc4e")))
            {
                resourcePreparationSnapshot = null;
                resourcePreparationError = null;
                try
                {
                    var view = target as View;
                    if (view == null)
                    {
                        throw new InvalidOperationException(L.Get("editor.ViewInspector.ResourcePreparation.7728ec29ef"));
                    }

                    resourcePreparationSnapshot = view.CaptureResourcePreparationSnapshot(64);
                    resourcePreparationTime = DateTime.Now.ToString("HH:mm:ss");
                }
                catch (Exception error)
                {
                    resourcePreparationError = error.Message;
                }
            }

            if (resourcePreparationError != null)
            {
                EditorGUILayout.HelpBox(L.Diagnostic(resourcePreparationError), MessageType.Error);
            }

            var snapshot = resourcePreparationSnapshot;
            if (snapshot == null)
            {
                EditorGUILayout.HelpBox(L.Get("editor.ViewInspector.ResourcePreparation.3385504be2"), MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(L.Get("editor.ViewInspector.ResourcePreparation.fbfb60e549"), resourcePreparationTime);
            EditorGUILayout.LabelField(L.Get("editor.ViewInspector.ResourcePreparation.700c1c683f"), L.Value(snapshot.Enabled));
            if (!snapshot.HasContext)
            {
                EditorGUILayout.HelpBox(L.Get("editor.ViewInspector.ResourcePreparation.e3acab17a5"), MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(L.Get("editor.ViewInspector.ResourcePreparation.c8875ed413"), $"{L.Value(snapshot.ActivationActive)} / {L.Value(snapshot.Committed)}");
            EditorGUILayout.LabelField(L.Get("editor.ViewInspector.ResourcePreparation.f1ad7e1a84"), $"{snapshot.TotalCount} / {snapshot.PendingCount}");
            EditorGUILayout.LabelField(L.Get("editor.ViewInspector.ResourcePreparation.800ab3c05f"), $"{snapshot.FailedCount} / {snapshot.CancelledCount} / {snapshot.NotAppliedCount}");
            foreach (var entry in snapshot.Entries)
            {
                EditorGUILayout.LabelField(entry.Target, StateLabel(entry.State));
            }

            if (snapshot.IsTruncated)
            {
                EditorGUILayout.HelpBox(L.Format("editor.ViewInspector.ResourcePreparation.f2ccc0a120", snapshot.Entries.Count, snapshot.TotalCount), MessageType.Info);
            }

            EditorGUILayout.HelpBox(L.Get("editor.ViewInspector.ResourcePreparation.02bafe3885"), MessageType.Info);
        }

        private static string StateLabel(ResourcePreparationState state)
        {
            switch (state)
            {
                case ResourcePreparationState.Loading:
                    return L.Get("editor.ViewInspector.ResourcePreparation.d04fcbda73");
                case ResourcePreparationState.Applied:
                    return L.Get("editor.ViewInspector.ResourcePreparation.f585743460");
                case ResourcePreparationState.NotApplied:
                    return L.Get("editor.ViewInspector.ResourcePreparation.8032af4a59");
                case ResourcePreparationState.Cancelled:
                    return L.Get("editor.ViewInspector.ResourcePreparation.a37778f17c");
                case ResourcePreparationState.Failed:
                    return L.Get("editor.ViewInspector.ResourcePreparation.28384d7afd");
                default:
                    return L.Get("editor.ViewInspector.ResourcePreparation.4d8c1c5b42");
            }
        }
    }
}
