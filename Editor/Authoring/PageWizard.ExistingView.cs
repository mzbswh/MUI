using System;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;

namespace MUI.Editor
{
    public sealed partial class PageWizard
    {
        [SerializeField] private GameObject existingPrefab;
        [SerializeField] private string titleElementName = "Title";
        [SerializeField] private string closeElementName = "Close";
        private (int Instance, string Path, string Backend, string Title, string Close)? existingValidationKey;
        private string existingValidationError;

        private string ExistingPrefabGuid => existingPrefab == null ? null :
                    AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(existingPrefab));

        private void DrawExistingView()
        {
            existingPrefab = (GameObject)EditorGUILayout.ObjectField("关联已有 View（可选）", existingPrefab, typeof(GameObject), false);
            if (existingPrefab != null)
            {
                titleElementName = EditorGUILayout.TextField("标题 Element 名称", titleElementName);
                closeElementName = EditorGUILayout.TextField("关闭按钮 Element 名称", closeElementName);
                EditorGUILayout.HelpBox("复用原 Prefab，仅在新页面目录生成源码。请按原控件选择文本组件；原资产不修改。创建后仍需将页面资源键映射到该 Prefab。", MessageType.Info);
            }
        }

        private void InvalidateExistingValidation()
        {
            existingValidationKey = null;
            existingValidationError = null;
        }

        private string ValidateExistingView(PageTextBackend backend)
        {
            if (existingPrefab == null)
            {
                return null;
            }
            var path = AssetDatabase.GetAssetPath(existingPrefab);
            var key = (existingPrefab.GetInstanceID(), path, backend.Id, titleElementName, closeElementName);
            if (existingValidationKey.HasValue && key == existingValidationKey.Value)
            {
                return existingValidationError;
            }
            existingValidationKey = key;
            try
            {
                existingValidationError = InspectExistingView(path, backend);
            }
            catch (Exception error)
            {
                existingValidationError = "已有 View 校验失败：" + error.Message;
            }
            return existingValidationError;
        }

        private string InspectExistingView(string path, PageTextBackend backend)
        {
            if (string.IsNullOrEmpty(path) || !PrefabUtility.IsPartOfPrefabAsset(existingPrefab) ||
                PrefabUtility.GetPrefabAssetType(existingPrefab) == PrefabAssetType.Model ||
                AssetDatabase.LoadAssetAtPath<GameObject>(path) != existingPrefab)
            {
                return "请选择项目中的普通 Prefab 或 Variant 根资产，不接受场景对象、模型或 Prefab 子节点。";
            }
            var view = existingPrefab.GetComponent<View>();
            if (view == null || existingPrefab.GetComponent<RectTransform>() == null)
            {
                return "已有 Prefab 根节点必须包含 View 和 RectTransform。";
            }
            if (string.IsNullOrWhiteSpace(titleElementName) || string.IsNullOrWhiteSpace(closeElementName))
            {
                return "请填写原 Prefab 中的标题和关闭按钮 Element 名称。";
            }
            var manifest = new BindingManifest(typeof(ViewModel), new[]
            {
                new BindingEntry("Title", titleElementName, backend.ElementType, "Content", BindingMode.OneWay),
                new BindingEntry("Close", closeElementName, typeof(ButtonElement), nameof(ButtonElement.Clicked),
                    BindingMode.OneWay, BindingEntryKind.Command)
            });
            var errors = ViewContractValidator.Validate(view, manifest);
            return errors.Count == 0 ? null : "已有 Prefab 不符合页面骨架契约：\n" + string.Join("\n", errors);
        }
    }
}
