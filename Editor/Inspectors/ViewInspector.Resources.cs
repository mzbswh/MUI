using System.Collections.Generic;
using MUI.UGUI;
using UnityEditor;

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
                            $"控件：{entry.ElementType.Name}；资源：{assetType}；绑定方向：{entry.Mode}");
                    }
                }

                resourceBindingsTitle = $"资源键绑定（{resourceBindingRows.Count}）";
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
                "以下是所选契约声明的内置资源键绑定，不代表已经匹配此 View。" +
                "请运行绑定契约校验检查控件匹配、单向绑定和重复写入。",
                MessageType.Info);
            foreach (var row in resourceBindingRows)
            {
                EditorGUILayout.LabelField(row, EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space();
            }

            EditorGUILayout.HelpBox(
                "在激活前通过 View.ConfigureSynchronousResources 或 ConfigureResources 配置加载器，" +
                "也可单独配置控件。内置 Prefab 提供方的 configureView 或 UIHost 默认目录的 configureDefaultView 可统一接线。" +
                "此清单不验证运行时加载器、资源键是否存在或异步加载是否完成。",
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
                    "已启用首次显示前等待资源，但所选契约没有内置资源键绑定。" +
                    "此开关不会自动等待直接赋值的资源、手动资源槽或任意业务后台任务；" +
                    "运行时代码写入默认加载器的 Source 属性仍可参与准备。",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "已启用首次显示前等待资源。下方资源键绑定使用本 View 的默认加载器时，" +
                "初始加载会参与隐藏准备，失败将阻止此次显示；单控件显式配置不自动参与。" +
                "子 View 需单独设置，显示后的换图仍渐进加载。同步加载直接完成，不创建等待任务。" +
                "此处只展示配置，不代表资源已经就绪。",
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
                    "已关闭父加载器继承。请为此 View 或资源控件显式配置加载器。",
                    MessageType.Info);
                return;
            }

            var parent = view.transform.parent;
            var parentView = parent == null ? null : parent.GetComponentInParent<View>(true);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("当前层级的父 View", parentView, typeof(View), true);
            }

            EditorGUILayout.HelpBox(
                parentView == null
                    ? "当前层级没有父 View。显式配置加载器，或在运行时挂载到父 View 下后再激活；框架会在激活时重新查找。"
                    : "未显式配置时，仅尝试借用此父 View 的活动加载器，不越过它查找祖先。" +
                      "子 View 的资源由自身激活释放；纯同步激活要求继承的加载器支持同步。",
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
