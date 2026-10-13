using System;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public sealed partial class ViewInspector
    {
        private bool showInputDiagnostics;
        private ViewInputSnapshot inputSnapshot;
        private string inputSnapshotError;
        private string inputSnapshotTime;
        private static readonly (ViewInputRestriction restriction, string label)[] InputRestrictionLabels =
        {
            (ViewInputRestriction.ViewUnavailable, "editor.ViewInspector.Input.d5269a8a96"),
            (ViewInputRestriction.Disposed, "editor.ViewInspector.Input.86ddfd16e6"),
            (ViewInputRestriction.NotInitialized, "editor.ViewInspector.Input.df0f92ba65"),
            (ViewInputRestriction.InactiveHierarchy, "editor.ViewInspector.Input.1fdfe0bd28"),
            (ViewInputRestriction.HiddenByHost, "editor.ViewInspector.Input.37cec66cad"),
            (ViewInputRestriction.HiddenLocally, "editor.ViewInspector.Input.df734ade65"),
            (ViewInputRestriction.HostInputDisabled, "editor.ViewInspector.Input.c1335a5110"),
            (ViewInputRestriction.LocalInputDisabled, "editor.ViewInspector.Input.f344af45ed"),
            (ViewInputRestriction.LocalGateBlocked, "editor.ViewInspector.Input.1242d676fa"),
            (ViewInputRestriction.LocalGateDisposed, "editor.ViewInspector.Input.08332dda56"),
            (ViewInputRestriction.RetainingVisuals, "editor.ViewInspector.Input.51e0bd7434"),
            (ViewInputRestriction.MissingCanvasGroup, "editor.ViewInspector.Input.18cd6c3bdd"),
            (ViewInputRestriction.CanvasGroupInputDisabled, "editor.ViewInspector.Input.364755a73e"),
            (ViewInputRestriction.CanvasGroupRaycastsDisabled, "editor.ViewInspector.Input.ef99d4b265"),
        };

        private void DrawInputDiagnostics()
        {
            EditorGUILayout.Space();
            showInputDiagnostics = EditorGUILayout.Foldout(showInputDiagnostics, L.Get("editor.ViewInspector.Input.7f70336887"), true);
            if (!showInputDiagnostics)
            {
                return;
            }
            if (GUILayout.Button(L.Get("editor.ViewInspector.Input.b52c53ec93")))
            {
                var view = target as View;
                try
                {
                    inputSnapshot = view == null ? null : view.CaptureInputSnapshot(maxBlockerReasons: 64);
                    inputSnapshotError = view == null ? L.Get("editor.ViewInspector.Input.7728ec29ef") : null;
                    inputSnapshotTime = DateTime.Now.ToString("HH:mm:ss");
                }
                catch (Exception error)
                {
                    inputSnapshot = null;
                    inputSnapshotError = error.Message;
                }
            }
            if (!string.IsNullOrEmpty(inputSnapshotError))
            {
                EditorGUILayout.HelpBox(L.Diagnostic(inputSnapshotError), MessageType.Error);
            }
            if (inputSnapshot == null)
            {
                EditorGUILayout.HelpBox(L.Get("editor.ViewInspector.Input.cbead0ea59"), MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField(L.Get("editor.ViewInspector.Input.fbfb60e549"), inputSnapshotTime);
            EditorGUILayout.LabelField(L.Get("editor.ViewInspector.Input.badd13f4e1"), $"{L.Value(inputSnapshot.ViewAlive)} / {L.Value(inputSnapshot.Initialized)}");
            EditorGUILayout.LabelField(L.Get("editor.ViewInspector.Input.fb254a3bb8"), L.Value(inputSnapshot.ActiveInHierarchy));
            EditorGUILayout.LabelField(L.Get("editor.ViewInspector.Input.233555df54"), $"{L.Value(inputSnapshot.HostVisible)} / {L.Value(inputSnapshot.LocalVisible)}");
            EditorGUILayout.LabelField(L.Get("editor.ViewInspector.Input.ac57ab417f"), $"{L.Value(inputSnapshot.HostInteractable)} / {L.Value(inputSnapshot.LocalInteractable)}");
            EditorGUILayout.LabelField(L.Get("editor.ViewInspector.Input.f3c337bf0c"), $"{L.Value(inputSnapshot.ViewVisible)} / {L.Value(inputSnapshot.ViewInputEnabled)}");
            EditorGUILayout.LabelField(L.Get("editor.ViewInspector.Input.f62cc4c61e"), L.Value(inputSnapshot.RetainingVisuals));
            EditorGUILayout.LabelField(L.Get("editor.ViewInspector.Input.bebdcdf08d"), EditorStyles.boldLabel);
            if (inputSnapshot.Restrictions == ViewInputRestriction.None)
            {
                EditorGUILayout.LabelField(L.Get("editor.ViewInspector.Input.608a5c4956"));
            }
            foreach (var item in InputRestrictionLabels)
            {
                if ((inputSnapshot.Restrictions & item.restriction) != 0)
                {
                    EditorGUILayout.LabelField("• " + L.Get(item.label), EditorStyles.wordWrappedLabel);
                }
            }
            var gate = inputSnapshot.InputGate;
            if (gate == null)
            {
                EditorGUILayout.LabelField(L.Get("editor.ViewInspector.Input.dbcbb1d915"), L.Get("editor.ViewInspector.Input.afff7f01c3"));
            }
            else
            {
                EditorGUILayout.LabelField(L.Get("editor.ViewInspector.Input.3349a0214e"), $"{gate.BlockerCount} / {L.Value(gate.IsDisposed)}");
                foreach (var reason in gate.Reasons)
                {
                    EditorGUILayout.LabelField("• " + reason, EditorStyles.wordWrappedLabel);
                }
                if (gate.IsTruncated)
                {
                    EditorGUILayout.HelpBox(L.Format("editor.ViewInspector.Input.d3cb8ab8ca", gate.Reasons.Count, gate.BlockerCount), MessageType.Warning);
                }
            }
            var group = inputSnapshot.CanvasGroup;
            EditorGUILayout.LabelField(L.Get("editor.ViewInspector.Input.b244abc9ee"), group.Exists ? L.Get("editor.ViewInspector.Input.4a558371f9") : L.Get("editor.ViewInspector.Input.65364e6fd8"));
            if (group.Exists)
            {
                EditorGUILayout.LabelField(L.Get("editor.ViewInspector.Input.23f2db09ee"), $"{L.Value(group.Enabled)} / {group.Alpha:0.###}");
                EditorGUILayout.LabelField(L.Get("editor.ViewInspector.Input.8ed1685df2"), $"{L.Value(group.Interactable)} / {L.Value(group.BlocksRaycasts)}");
                EditorGUILayout.LabelField(L.Get("editor.ViewInspector.Input.e7d20c7470"), L.Value(group.IgnoreParentGroups));
            }
            EditorGUILayout.HelpBox(L.Get("editor.ViewInspector.Input.37cdf97b81"), MessageType.Info);
        }
    }
}
