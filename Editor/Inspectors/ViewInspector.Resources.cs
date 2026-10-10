using System.Collections.Generic;
using MUI.UGUI;
using UnityEditor;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public sealed partial class ViewInspector
    {
        private BindingManifest resourceManifest;
        private readonly List<string> resourceBindingRows = new List<string>();
        private bool showResourceBindings;
        private string resourceBindingsTitle;

        /// <summary>只读取所选生成契约，不初始化控件、不创建模型或试加载资源。</summary>
        private void DrawResourceBindings(BindingManifest manifest)
        {
            if (!ReferenceEquals(resourceManifest, manifest))
            {
                resourceManifest = manifest;
                resourceBindingRows.Clear();
                foreach (var entry in manifest.Entries)
                {
                    var assetType = GetResourceBindingType(entry);
                    if (assetType != null)
                    {
                        resourceBindingRows.Add(
                            $"{entry.Source} → {entry.ElementName}.{entry.TargetProperty}\n" +
                            L.Format("editor.ViewInspector.Resources.2b59743c8d", entry.ElementType.Name, assetType, entry.Mode));
                    }
                }

                resourceBindingsTitle = L.Format("editor.ViewInspector.Resources.a2ed0370b9", resourceBindingRows.Count);
            }

            DrawResourcePreparationPolicy();
            if (resourceBindingRows.Count == 0)
            {
                return;
            }

            showResourceBindings = EditorGUILayout.Foldout(showResourceBindings, resourceBindingsTitle, true);
            if (!showResourceBindings)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                L.Get("editor.ViewInspector.Resources.9de0d10b5a") +
                L.Get("editor.ViewInspector.Resources.e82b69fca1"),
                MessageType.Info);
            foreach (var row in resourceBindingRows)
            {
                EditorGUILayout.LabelField(row, EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space();
            }

            EditorGUILayout.HelpBox(
                L.Get("editor.ViewInspector.Resources.80333987cf") +
                L.Get("editor.ViewInspector.Resources.41d0c03c8d") +
                L.Get("editor.ViewInspector.Resources.7825ca1133"),
                MessageType.Info);

            DrawResourceInheritance();
        }

        /// <summary>描述首帧等待的声明范围，不通过扫描清单推断项目运行时的资源加载状态。</summary>
        private void DrawResourcePreparationPolicy()
        {
            var view = (View)target;
            if (view == null || !view.WaitForResourceSources)
            {
                return;
            }

            if (resourceBindingRows.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    L.Get("editor.ViewInspector.Resources.9bd119ee27") +
                    L.Get("editor.ViewInspector.Resources.5887219e26") +
                    L.Get("editor.ViewInspector.Resources.2473966443"),
                    MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                L.Get("editor.ViewInspector.Resources.ac5c1bb62a") +
                L.Get("editor.ViewInspector.Resources.15980a3c2d") +
                L.Get("editor.ViewInspector.Resources.537a6e78ff") +
                L.Get("editor.ViewInspector.Resources.e21b9e988b"),
                MessageType.Info);
        }

        /// <summary>展示当前层级的继承候选，不把编辑态父节点误报为已配置的运行时加载器。</summary>
        private void DrawResourceInheritance()
        {
            var view = (View)target;
            if (view == null)
            {
                return;
            }

            if (!view.InheritParentResources)
            {
                EditorGUILayout.HelpBox(
                    L.Get("editor.ViewInspector.Resources.16ef1cfe27"),
                    MessageType.Info);
                return;
            }

            var parent = view.transform.parent;
            var parentView = parent == null ? null : parent.GetComponentInParent<View>(true);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(L.Get("editor.ViewInspector.Resources.d822773387"), parentView, typeof(View), true);
            }

            EditorGUILayout.HelpBox(
                parentView == null
                    ? L.Get("editor.ViewInspector.Resources.f116785f60")
                    : L.Get("editor.ViewInspector.Resources.4e3e33fde6") +
                      L.Get("editor.ViewInspector.Resources.c678584105"),
                MessageType.Info);
        }

        private static string GetResourceBindingType(BindingEntry entry)
        {
            if (entry.Kind != BindingEntryKind.Property)
            {
                return null;
            }

            // 按控件类型及属性共同识别，避免把项目同名业务属性误认为框架资源槽。
            if (entry.TargetProperty == nameof(ImageElement.SpriteSource) &&
                typeof(ImageElement).IsAssignableFrom(entry.ElementType))
            {
                return "Sprite";
            }

            if (entry.TargetProperty == nameof(RawImageElement.TextureSource) &&
                typeof(RawImageElement).IsAssignableFrom(entry.ElementType))
            {
                return "Texture";
            }

            if (entry.TargetProperty == nameof(TextElement.FontSource) &&
                typeof(TextElement).IsAssignableFrom(entry.ElementType))
            {
                return "Font";
            }

            // TMP 等可选文本控件继承公共图形层，无需编辑器反向依赖 TMP。
            return entry.TargetProperty == nameof(GraphicElement.MaterialSource) &&
                typeof(GraphicElement).IsAssignableFrom(entry.ElementType) ? "Material" : null;
        }
    }
}
