using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;

namespace MUI.Editor
{
    public sealed partial class ViewInspector
    {
        private string associationError;
        private Type runtimeModelType;
        private string runtimeModelSourceError;

        public override bool RequiresConstantRepaint() => EditorApplication.isPlaying;

        private void DrawRuntimeModelAssociation(View view)
        {
            var model = view == null ? null : BindingContext.GetBoundModel(view);
            var type = model == null ? null : model.GetType();
            if (runtimeModelType != type)
            {
                runtimeModelType = type;
                runtimeModelSourceError = null;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("运行时 Model");
                if (type == null)
                    EditorGUILayout.LabelField("未绑定");
                else if (GUILayout.Button(new GUIContent(type.FullName,
                    type.Assembly.GetName().Name + "\n点击定位 Model 源码"), EditorStyles.linkLabel))
                    OpenRuntimeModelSource(type);
            }
            if (!string.IsNullOrEmpty(runtimeModelSourceError))
                EditorGUILayout.HelpBox(runtimeModelSourceError, MessageType.Warning);
        }

        private void OpenRuntimeModelSource(Type type)
        {
            runtimeModelSourceError = null;
            try
            {
                var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
                foreach (var script in MonoImporter.GetAllRuntimeMonoScripts())
                {
                    if (script == null || script.GetClass() != definition)
                        continue;
                    EditorGUIUtility.PingObject(script);
                    if (!AssetDatabase.OpenAsset(script))
                        runtimeModelSourceError = "无法打开 Model 源码：" + AssetDatabase.GetAssetPath(script);
                    return;
                }
                runtimeModelSourceError = "未找到 " + type.FullName + " 对应的 MonoScript；请确认源码已导入 Unity，且类名与文件名一致。";
            }
            catch (Exception error)
            {
                runtimeModelSourceError = "定位 Model 源码失败：" + error.Message;
            }
        }

        private void DrawModelAssociation()
        {
            var view = (View)target;
            if (EditorApplication.isPlaying && view != null && !EditorUtility.IsPersistent(view) &&
                UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(view.gameObject) == null)
            {
                DrawRuntimeModelAssociation(view);
                return;
            }
            XElement metadata = null;
            try
            {
                if (!string.IsNullOrEmpty(view.AutoBindingsMetadata))
                    metadata = XElement.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(view.AutoBindingsMetadata)));
            }
            catch (Exception error) { associationError = "ViewModel 关联数据损坏：" + error.Message; }
            var types = TypeCache.GetTypesDerivedFrom<ViewModel>().Where(type => !type.IsAbstract && !type.ContainsGenericParameters &&
                (type.GetCustomAttribute<UIPageAttribute>() != null || type.GetCustomAttribute<UIWidgetAttribute>() != null)).OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
            var index = Array.FindIndex(types, type => type.FullName == (string)metadata?.Attribute("model") &&
                type.Assembly.GetName().Name == (string)metadata?.Attribute("assembly"));
            var labels = new[] { metadata == null ? "未关联" : "关联失效（请重新选择）" }
                .Concat(types.Select(type => type.FullName + " (" + type.Assembly.GetName().Name + ")")).ToArray();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
            {
                var next = EditorGUILayout.Popup("关联 ViewModel", index + 1, labels) - 1;
                if (next != index && next >= 0)
                {
                    try
                    {
                        var path = CurrentPrefabPath();
                        if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("请先把 View 保存为 Prefab，再设置关联。");
                        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(view.gameObject);
                        if (stage == null && !EditorUtility.IsPersistent(view))
                            throw new InvalidOperationException("请打开 Prefab 资产或 Prefab 编辑模式修改关联。");
                        ValidateAutoBindingOwner(path, types[next]);
                        metadata = metadata ?? new XElement("bindings");
                        metadata.SetAttributeValue("model", types[next].FullName);
                        metadata.SetAttributeValue("assembly", types[next].Assembly.GetName().Name);
                        serializedObject.Update();
                        serializedObject.FindProperty("muiAutoBindings").stringValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(metadata.ToString(SaveOptions.DisableFormatting)));
                        serializedObject.ApplyModifiedProperties();
                        if (stage != null)
                        {
                            if (PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, path) == null)
                                throw new InvalidOperationException("保存 Prefab 关联失败。");
                        }
                        else if (EditorUtility.IsPersistent(view)) PrefabUtility.SavePrefabAsset(view.transform.root.gameObject);
                        else throw new InvalidOperationException("请在 Prefab 资产或 Prefab 编辑模式中保存关联。");
                        associationError = null;
                        InitializeInspectorState();
                        CompilerGeneratedSources.RequestProjectSync();
                        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                    }
                    catch (Exception error) { associationError = error.Message; }
                }
            }
            if (index < 0 && metadata != null)
                EditorGUILayout.HelpBox("关联的 ViewModel 已不存在或类型不可用，请重新选择；不会自动猜测替代类型。", MessageType.Warning);
            if (!string.IsNullOrEmpty(associationError)) EditorGUILayout.HelpBox(associationError, MessageType.Error);
        }
    }
}
