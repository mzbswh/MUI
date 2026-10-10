using System;
using System.Collections.Generic;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public sealed class ElementAuthoringWindow : EditorWindow
    {
        private readonly List<Candidate> candidates = new List<Candidate>();
        private GameObject root;
        private Vector2 scroll;
        private string message;

        [MenuItem("Tools/MUI/控件创建 (Element Authoring)")]
        private static void Open()
        {
            var window = GetWindow<ElementAuthoringWindow>(L.Get("editor.ElementAuthoringWindow.f27fa1ad5c"));
            window.root = Selection.activeGameObject;
            window.Scan();
        }

        private void OnGUI()
        {
            titleContent.text = L.Get("editor.ElementAuthoringWindow.f27fa1ad5c");
            var next = (GameObject)EditorGUILayout.ObjectField(L.Get("editor.ElementAuthoringWindow.44cb005ee2"), root, typeof(GameObject), true);
            if (next != root)
            {
                root = next;
                candidates.Clear();
                message = null;
            }

            EditorGUILayout.HelpBox(L.Get("editor.ElementAuthoringWindow.4c72361bea"), MessageType.Info);
            if (GUILayout.Button(L.Get("editor.ElementAuthoringWindow.ea1c510d1d")))
            {
                Scan();
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var candidate in candidates)
            {
                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(candidate.Adapter == null || candidate.Target == null))
                {
                    candidate.Selected = EditorGUILayout.Toggle(candidate.Selected, GUILayout.Width(20));
                }

                EditorGUILayout.ObjectField(candidate.Target, typeof(GameObject), true);
                EditorGUILayout.LabelField(candidate.Adapter == null ? L.Diagnostic(candidate.Note) : candidate.Adapter.Name);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            using (new EditorGUI.DisabledScope(root == null || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button(L.Get("editor.ElementAuthoringWindow.38399863b6")))
                {
                    Apply();
                }
            }

            if (!string.IsNullOrEmpty(message))
            {
                EditorGUILayout.HelpBox(L.Diagnostic(message), MessageType.Info);
            }
        }

        private void Scan()
        {
            candidates.Clear();
            message = null;
            if (root == null)
            {
                return;
            }

            if (EditorUtility.IsPersistent(root))
            {
                message = L.Get("editor.ElementAuthoringWindow.beb38a6ce0");
                return;
            }

            Visit(root.transform, true);
        }

        private void Visit(Transform node, bool isRoot)
        {
            if (!isRoot && (node.GetComponent<View>() != null || PrefabUtility.IsAnyPrefabInstanceRoot(node.gameObject)))
            {
                return;
            }

            foreach (var component in node.GetComponents<MonoBehaviour>())
            {
                if (component is IElementBoundary)
                {
                    return;
                }
            }

            var adapter = Resolve(node.gameObject, out var note);
            if (adapter != null || note != null)
            {
                candidates.Add(new Candidate { Target = node.gameObject, Adapter = adapter, Note = note, Selected = adapter != null });
            }

            for (var i = 0; i < node.childCount; ++i)
            {
                Visit(node.GetChild(i), false);
            }
        }

        private static Type Resolve(GameObject target, out string note)
        {
            note = null;
            if (target.GetComponent<Element>() != null)
            {
                return null;
            }

            var components = target.GetComponents<MonoBehaviour>();
            foreach (var component in components)
            {
                if (component == null)
                {
                    note = L.Get("editor.ElementAuthoringWindow.e623e26a68");
                    return null;
                }
            }

            // 交互适配器的识别优先于其背景 Image。
            var types = new List<Type>();
            if (target.GetComponent<Button>() != null)
            {
                types.Add(typeof(ButtonElement));
            }

            if (target.GetComponent<Toggle>() != null)
            {
                types.Add(typeof(ToggleElement));
            }

            if (target.GetComponent<Slider>() != null)
            {
                types.Add(typeof(SliderElement));
            }

            if (target.GetComponent<Scrollbar>() != null)
            {
                types.Add(typeof(ScrollbarElement));
            }

            if (target.GetComponent<InputField>() != null)
            {
                types.Add(typeof(InputFieldElement));
            }

            if (target.GetComponent<Dropdown>() != null)
            {
                types.Add(typeof(DropdownElement));
            }

            if (target.GetComponent<ScrollRect>() != null)
            {
                types.Add(typeof(ScrollRectElement));
            }

            if (target.GetComponent<Mask>() != null)
            {
                types.Add(typeof(MaskElement));
            }

            if (target.GetComponent<RectMask2D>() != null)
            {
                types.Add(typeof(RectMaskElement));
            }

            foreach (var component in components)
            {
                for (var type = component.GetType(); type != null; type = type.BaseType)
                {
                    if (type.FullName == "TMPro.TMP_InputField" || type.FullName == "TMPro.TMP_Dropdown")
                    {
                        var adapter = FindTMPAdapter(type.FullName == "TMPro.TMP_InputField" ? "TMPInputFieldElement" : "TMPDropdownElement");
                        if (adapter == null)
                        {
                            note = L.Get("editor.ElementAuthoringWindow.e52e414963");
                            return null;
                        }

                        types.Add(adapter);
                        break;
                    }
                }
            }

            if (types.Count > 1)
            {
                note = L.Get("editor.ElementAuthoringWindow.57ec66460c");
                return null;
            }

            if (types.Count == 1)
            {
                return types[0];
            }

            foreach (var component in components)
            {
                for (var type = component.GetType(); type != null; type = type.BaseType)
                {
                    if (type.Namespace == "TMPro" && type.FullName != "TMPro.TextMeshProUGUI" && type.FullName != "TMPro.TMP_Text")
                    {
                        note = L.Get("editor.ElementAuthoringWindow.dd39801df2");
                        return null;
                    }
                }
            }

            foreach (var component in components)
            {
                for (var type = component.GetType(); type != null; type = type.BaseType)
                {
                    if (type.FullName == "TMPro.TextMeshProUGUI")
                    {
                        // 仅编辑器中的可选发现流程；运行时绑定使用具体适配器类型。
                        var adapter = FindTMPAdapter("TMPTextElement");
                        if (adapter != null)
                        {
                            return adapter;
                        }

                        note = L.Get("editor.ElementAuthoringWindow.0ae06aecdf");
                        return null;
                    }
                }
            }

            if (target.GetComponent<Text>() != null)
            {
                return typeof(TextElement);
            }

            if (target.GetComponent<Image>() != null)
            {
                return typeof(ImageElement);
            }

            if (target.GetComponent<UnityEngine.UI.RawImage>() != null)
            {
                return typeof(RawImageElement);
            }

            // 附属布局组件不抢占主控件适配；已有主控件时可由作者手动添加布局 Element。
            var hasLayout = target.GetComponent<UnityEngine.UI.LayoutElement>() != null;
            var hasAspect = target.GetComponent<AspectRatioFitter>() != null;
            if (hasLayout && hasAspect)
            {
                note = L.Get("editor.ElementAuthoringWindow.49f6375235");
                return null;
            }

            if (hasLayout)
            {
                return typeof(LayoutSizeElement);
            }

            if (hasAspect)
            {
                return typeof(AspectRatioElement);
            }

            return null;
        }

        private static Type FindTMPAdapter(string name)
        {
            var adapter = Type.GetType("MUI.TMP." + name + ", MUI.TMP", false);
            return adapter != null && !adapter.IsAbstract && typeof(Element).IsAssignableFrom(adapter) ? adapter : null;
        }

        private void Apply()
        {
            if (root == null || EditorUtility.IsPersistent(root) || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var selected = new Dictionary<GameObject, Type>();
            foreach (var candidate in candidates)
            {
                if (candidate.Selected && candidate.Adapter != null && candidate.Target != null)
                {
                    selected[candidate.Target] = candidate.Adapter;
                }
            }

            // 修改前重新扫描，层级或组件变化会使旧候选失效。
            Scan();
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(L.Get("editor.ElementAuthoringWindow.26e0256cd8"));
            var count = 0;
            try
            {
                foreach (var candidate in candidates)
                {
                    if (candidate.Adapter != null && selected.TryGetValue(candidate.Target, out var type) && type == candidate.Adapter)
                    {
                        if (Undo.AddComponent(candidate.Target, type) == null)
                        {
                            throw new InvalidOperationException(L.Get("editor.ElementAuthoringWindow.742f9c0260") + type.Name);
                        }
                        ++count;
                    }
                }
                Undo.CollapseUndoOperations(group);
            }
            catch (Exception error)
            {
                // 与命名工具保持一致：失败不能留下半批组件或报告部分成功。
                Undo.RevertAllDownToGroup(group);
                Debug.LogException(error);
                Scan();
                message = L.Get("editor.ElementAuthoringWindow.cf61a83125");
                return;
            }

            Scan();
            message = L.Format("editor.ElementAuthoringWindow.29e3c4643d", count);
        }

        private sealed class Candidate
        {
            public GameObject Target;
            public Type Adapter;
            public string Note;
            public bool Selected;
        }
    }
}
