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
            if (!inspectorReady) InitializeInspectorState();
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
            AddViewSection(configuration, "transitions", true, () =>
            {
                DrawViewField("enterFadeDuration");
                DrawViewField("exitFadeDuration");
            });
            AddViewSection(configuration, "interaction", true, () =>
            {
                DrawViewField("defaultSelection");
                DrawViewField("hiddenMode");
                if ((ViewHiddenMode)serializedObject.FindProperty("hiddenMode").enumValueIndex == ViewHiddenMode.DeactivateContent)
                {
                    DrawViewField("hiddenContentRoot");
                    var view = (View)target;
                    var content = view.HiddenContentRoot;
                    if (content == null || content == view.gameObject || !content.transform.IsChildOf(view.transform))
                        EditorGUILayout.HelpBox(L.Get("policy.hidden.invalid"), MessageType.Error);
                }
            });
            AddViewSection(configuration, "modal", false, () =>
            {
                DrawViewField("useGlobalModalBackdrop");
                if (serializedObject.FindProperty("useGlobalModalBackdrop").boolValue)
                {
                    if (!MUIProjectSettings.TryGet(out var settings, out var error))
                        EditorGUILayout.HelpBox(error, MessageType.Error);
                    else
                    {
                        using (new EditorGUI.DisabledScope(true))
                            EditorGUILayout.ObjectField(L.Get("property.defaultModalBackdropPrefab"),
                                settings == null ? null : settings.DefaultModalBackdropPrefab, typeof(RectTransform), false);
                        if (settings == null || settings.DefaultModalBackdropPrefab == null)
                            EditorGUILayout.LabelField(L.Get("viewPanel.modal.fallback"), EditorStyles.wordWrappedMiniLabel);
                        if (GUILayout.Button(L.Get("viewPanel.modal.openSettings"), EditorStyles.miniButton))
                            MUIProjectSettings.OpenOrCreate();
                    }
                    return;
                }
                DrawViewField("modalBackdropStyle");
                using (new EditorGUI.DisabledScope(serializedObject.FindProperty("modalBackdropStyle").objectReferenceValue != null))
                    DrawViewField("modalBarrierColor");
            });
            AddViewSection(configuration, "resources", false, () =>
            {
                DrawViewField("inheritParentResources");
                DrawViewField("waitForResourceSources");
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
                    if (binding.Key == null) continue;
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
                if (this == null || target == null || container.panel == null) return;
                if (!inspectorReady) return;
                var state = serializedObject;
                if (state == null || state.targetObject == null) return;
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
                if (this == null || target == null) return;
                if (!inspectorReady) InitializeInspectorState();
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
