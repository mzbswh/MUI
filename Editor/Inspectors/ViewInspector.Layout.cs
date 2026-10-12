using System;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public sealed partial class ViewInspector
    {
        public override VisualElement CreateInspectorGUI()
        {
            if (!inspectorReady)
                InitializeInspectorState();
            var root = new VisualElement();
            root.AddToClassList("mui-inspector");
            root.AddToClassList("mui-view-inspector");
            MUIEditorControls.Style(root, this);

            var tabs = new VisualElement();
            tabs.AddToClassList("mui-view-tabs");
            root.Add(tabs);
            var keys = new[] { "bindings", "configuration", "diagnostics" };
            var panels = new VisualElement[keys.Length];
            var buttons = new Button[keys.Length];
            void Select(int index)
            {
                for (var i = 0; i < panels.Length; i++)
                {
                    panels[i].style.display = i == index ? DisplayStyle.Flex : DisplayStyle.None;
                    buttons[i].EnableInClassList("mui-view-tab-active", i == index);
                }
            }
            for (var i = 0; i < keys.Length; i++)
            {
                var index = i;
                var key = "viewPanel." + keys[i];
                var button = new Button(() => Select(index));
                button.AddToClassList("mui-view-tab");
                L.Track(button, () => { button.text = L.Get(key); button.tooltip = L.Get(key + ".help"); });
                buttons[i] = button;
                tabs.Add(button);
                panels[i] = new VisualElement();
                root.Add(panels[i]);
            }

            var configuration = panels[1];
            AddViewSection(configuration, "policy", true, DrawPagePolicySource);
            AddViewSection(configuration, "transitions", true, () =>
            {
                DrawViewField("transitionMode");
                DrawViewField("transitionTarget");
                if (serializedObject.FindProperty("transitionMode").enumValueIndex == (int)ViewTransitionMode.Override)
                {
                    DrawViewField("enterTransition");
                    DrawViewField("exitTransition");
                    DrawViewField("enterTransitionBehaviour");
                    DrawViewField("exitTransitionBehaviour");
                }
                DrawViewField("enterFadeDuration");
                DrawViewField("exitFadeDuration");
                DrawConfigurationHint("viewPanel.transitions.help");
            });
            AddViewSection(configuration, "interaction", true, () =>
            {
                DrawViewField("defaultSelection");
                DrawConfigurationHint("viewPanel.focus.order");
                var selection = serializedObject.FindProperty("defaultSelection").objectReferenceValue as UnityEngine.UI.Selectable;
                var view = (View)target;
                if (selection != null && !selection.transform.IsChildOf(view.transform))
                    EditorGUILayout.HelpBox(L.Get("viewPanel.focus.outside"), MessageType.Warning);
                else if (selection != null && (!selection.isActiveAndEnabled || !selection.IsInteractable()))
                    DrawConfigurationHint("viewPanel.focus.unavailable");
            });
            AddViewSection(configuration, "hidden", true, () =>
            {
                DrawViewField("hiddenMode");
                DrawConfigurationHint("viewPanel.hidden.help");
                if ((ViewHiddenMode)serializedObject.FindProperty("hiddenMode").enumValueIndex == ViewHiddenMode.DeactivateContent)
                {
                    DrawViewField("hiddenContentRoot");
                    var view = (View)target;
                    var content = view.HiddenContentRoot;
                    if (content == null || content == view.gameObject || !content.transform.IsChildOf(view.transform))
                        EditorGUILayout.HelpBox(L.Get("policy.hidden.invalid"), MessageType.Error);
                }
            });
            AddViewSection(configuration, "modal", false, DrawModalConfiguration);
            AddViewSection(configuration, "resources", false, () =>
            {
                DrawViewField("inheritParentResources");
                DrawConfigurationHint("viewPanel.resources.inheritance");
                DrawViewField("waitForResourceSources");
                DrawConfigurationHint("viewPanel.resources.waitScope");
            });

            panels[0].Add(MUIEditorControls.Text("viewPanel.bindings.help"));
            var bindingCard = new VisualElement();
            bindingCard.AddToClassList("mui-card");
            bindingCard.Add(InspectorBlock(DrawBindings));
            panels[0].Add(bindingCard);
            panels[2].Add(MUIEditorControls.Text("viewPanel.diagnostics.help"));
            var diagnosticsCard = new VisualElement();
            diagnosticsCard.AddToClassList("mui-card");
            diagnosticsCard.Add(InspectorBlock(() =>
            {
                DrawRenderOrderDiagnostics();
                GUILayout.Space(8);
                DrawInputDiagnostics();
                GUILayout.Space(8);
                DrawResourcePreparationDiagnostics();
                GUILayout.Space(8);
                DrawRaycastDiagnostics();
            }));
            panels[2].Add(diagnosticsCard);
            Select(0);
            return root;
        }

        private static void DrawConfigurationHint(string key)
        {
            EditorGUILayout.LabelField(L.Get(key), EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawModalConfiguration()
        {
            DrawConfigurationHint("viewPanel.modal.help");
            var global = serializedObject.FindProperty("useGlobalModalBackdrop");
            var rect = EditorGUILayout.GetControlRect();
            var label = EditorGUI.BeginProperty(rect, new GUIContent(L.Get("viewPanel.modal.source")), global);
            EditorGUI.BeginChangeCheck();
            var source = EditorGUI.Popup(rect, label, global.boolValue ? 0 : 1,
                new[] { new GUIContent(L.Get("viewPanel.modal.global")), new GUIContent(L.Get("viewPanel.modal.local")) });
            if (EditorGUI.EndChangeCheck())
            {
                global.boolValue = source == 0;
                serializedObject.ApplyModifiedProperties();
            }
            EditorGUI.EndProperty();
            if (global.boolValue)
            {
                if (!RuntimeSettingsAsset.TryGet(out var settings, out var error))
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                else
                {
                    DrawResolvedBackdrop(settings == null ? null : settings.DefaultModalBackdropPrefab,
                        settings == null ? new Color(0, 0, 0, 0.45f) : settings.DefaultModalBackdropColor);
                    if (GUILayout.Button(L.Get("viewPanel.modal.openSettings"), EditorStyles.miniButton))
                        RuntimeSettingsAsset.OpenOrCreate();
                }
                return;
            }
            DrawViewField("modalBackdropStyle");
            var style = serializedObject.FindProperty("modalBackdropStyle").objectReferenceValue as ModalBackdropStyle;
            if (style == null)
            {
                DrawViewField("modalBarrierColor");
                DrawConfigurationHint("viewPanel.modal.fallback");
            }
            else
                DrawResolvedBackdrop(style.VisualPrefab, style.Tint);
        }

        private static void DrawResolvedBackdrop(RectTransform prefab, Color tint)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(L.Get("viewPanel.modal.resolvedPrefab"), prefab, typeof(RectTransform), false);
                EditorGUILayout.ColorField(L.Get("viewPanel.modal.resolvedColor"), tint);
            }
            if (prefab == null)
                DrawConfigurationHint("viewPanel.modal.fallback");
        }

        private void DrawRenderOrderDiagnostics()
        {
            var view = (View)target;
            EditorGUILayout.LabelField(L.Get("viewPanel.sorting"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(L.Get("viewPanel.sorting.help"), EditorStyles.wordWrappedMiniLabel);
            if (!view.HasRenderOrder)
            {
                EditorGUILayout.LabelField(L.Get("viewPanel.sorting.inactive"), EditorStyles.wordWrappedMiniLabel);
                return;
            }
            EditorGUILayout.LabelField(L.Get("viewPanel.sorting.range"),
                view.RenderOrderStart + " – " + (view.RenderOrderStart + view.RenderOrderSpan - 1));
            using (new EditorGUI.DisabledScope(true))
            {
                foreach (var binding in view.ResolvedRenderOrderTargets)
                {
                    if (binding.Key == null)
                        continue;
                    EditorGUILayout.ObjectField("Order " + (view.RenderOrderStart + binding.Value), binding.Key, typeof(Component), true);
                }
            }
        }

        private void AddViewSection(VisualElement parent, string name, bool expanded, Action draw)
        {
            var section = MUIEditorControls.Section("viewPanel." + name, expanded, "MUI.ViewInspector." + name);
            L.Track(section, () => section.tooltip = L.Get("viewPanel." + name + ".help"));
            section.Add(InspectorBlock(draw));
            parent.Add(section);
        }

        private IMGUIContainer InspectorBlock(Action draw)
        {
            // Each visual tree owns its callback. Creating another Inspector must not
            // invalidate a still-attached tree using the same Editor instance.
            var container = new IMGUIContainer();
            Action handler = () =>
            {
                if (this == null || target == null || container.panel == null)
                    return;
                if (!inspectorReady)
                    return;
                var state = serializedObject;
                if (state == null || state.targetObject == null)
                    return;
                var previous = EditorGUIUtility.labelWidth;
                try
                {
                    EditorGUIUtility.labelWidth = Mathf.Clamp(EditorGUIUtility.currentViewWidth * 0.43f, 110, 200);
                    state.UpdateIfRequiredOrScript();
                    draw();
                }
                finally { EditorGUIUtility.labelWidth = previous; }
            };
            container.onGUIHandler = handler;
            container.RegisterCallback<DetachFromPanelEvent>(_ => container.onGUIHandler = null);
            container.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                if (this == null || target == null)
                    return;
                if (!inspectorReady)
                    InitializeInspectorState();
                container.onGUIHandler = handler;
                container.MarkDirtyLayout();
            });
            return container;
        }

        private void DrawViewField(string name)
        {
            Localization.MUIEditorInspector.DrawField(serializedObject, name, "viewPanel.field." + name);
            GUILayout.Space(1);
        }
    }
}
