using System;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public sealed partial class ViewInspector
    {
        private bool showRaycastDiagnostics;
        private EventSystem raycastEventSystem;
        private Vector2 raycastPosition;
        private UIRaycastSnapshot raycastSnapshot;
        private string raycastError;
        private string raycastTime;

        private void DrawRaycastDiagnostics()
        {
            showRaycastDiagnostics = EditorGUILayout.Foldout(showRaycastDiagnostics, L.Get("editor.ViewInspector.Raycast.43ee9bc0cf"), true);
            if (!showRaycastDiagnostics)
            {
                return;
            }
            EditorGUILayout.HelpBox(L.Get("editor.ViewInspector.Raycast.189804d838"), MessageType.Info);
            raycastEventSystem = (EventSystem)EditorGUILayout.ObjectField(L.Get("editor.ViewInspector.Raycast.1fc3b8b8cd"),
                raycastEventSystem, typeof(EventSystem), true);
            raycastPosition = EditorGUILayout.Vector2Field(L.Get("editor.ViewInspector.Raycast.5a9535753a"), raycastPosition);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button(L.Get("editor.ViewInspector.Raycast.0bf5670e7f")))
                {
                    try
                    {
                        var view = target as View;
                        if (view == null)
                        {
                            throw new InvalidOperationException(L.Get("editor.ViewInspector.Raycast.7728ec29ef"));
                        }
                        var system = raycastEventSystem == null ? EventSystem.current : raycastEventSystem;
                        raycastSnapshot = UIRaycastDiagnostics.Capture(system, raycastPosition, view.gameObject, maxHits: 64);
                        raycastTime = DateTime.Now.ToString("HH:mm:ss");
                        raycastError = null;
                    }
                    catch (Exception error)
                    {
                        raycastSnapshot = null;
                        raycastError = error.Message;
                    }
                }
            }
            if (!string.IsNullOrEmpty(raycastError))
            {
                EditorGUILayout.HelpBox(L.Diagnostic(raycastError), MessageType.Error);
            }
            if (raycastSnapshot == null)
            {
                return;
            }
            EditorGUILayout.LabelField(L.Get("editor.ViewInspector.Raycast.06404dfdd1"), $"{raycastTime} / {raycastSnapshot.Position}");
            EditorGUILayout.LabelField(L.Get("editor.ViewInspector.Raycast.dc319e6c35"), $"{raycastSnapshot.TotalHits} / {raycastSnapshot.SkippedHits}");
            EditorGUILayout.LabelField(L.Get("editor.ViewInspector.Raycast.69f0f73432"), L.Value(raycastSnapshot.FirstTargetIndex));
            EditorGUILayout.LabelField(L.Get("editor.ViewInspector.Raycast.6db8767712"), L.Value(raycastSnapshot.TopHitBelongsToTarget));
            if (raycastSnapshot.IsTruncated)
            {
                EditorGUILayout.HelpBox(L.Get("editor.ViewInspector.Raycast.c2793a1eb9"), MessageType.Warning);
            }
            for (var i = 0; i < raycastSnapshot.Hits.Count; ++i)
            {
                var hit = raycastSnapshot.Hits[i];
                EditorGUILayout.LabelField($"[{i}] {hit.Path}（{hit.ObjectId}）", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField(L.Get("editor.ViewInspector.Raycast.8e0ad2ae93"), hit.ClickHandlerId == 0 ? L.Get("editor.ViewInspector.Raycast.484d556139") : $"{hit.ClickHandlerPath}（{hit.ClickHandlerId}）", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField(L.Get("editor.ViewInspector.Raycast.ce0cb78f83"), hit.RaycasterType, EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField(L.Get("editor.ViewInspector.Raycast.b6de5f51cf"), $"{hit.SortingLayer} / {hit.SortingOrder} / {hit.Depth} / {hit.Distance:0.###}");
            }
            EditorGUILayout.HelpBox(L.Get("editor.ViewInspector.Raycast.8d553514be"), MessageType.Info);
        }
    }
}
