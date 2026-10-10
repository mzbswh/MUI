using System;
using System.Collections.Generic;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public sealed partial class PageWizard
    {
        [SerializeField] private GameObject existingPrefab;
        [SerializeField] private string titleElementName = "Title";
        [SerializeField] private string closeElementName = "Close";
        private (int Instance, string Path, string Backend, string Title, string Close, PagePreset Preset)? existingValidationKey;
        private string existingValidationError;

        private string ExistingPrefabGuid => existingPrefab == null ? null :
                    AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(existingPrefab));

        private void DrawExistingView()
        {
            existingPrefab = (GameObject)EditorGUILayout.ObjectField(L.Get("editor.PageWizard.ExistingView.593ff81861"), existingPrefab, typeof(GameObject), false);
            if (existingPrefab != null)
            {
                titleElementName = EditorGUILayout.TextField(L.Get("editor.PageWizard.ExistingView.f7f2eaad00"), titleElementName);
                if (preset != PagePreset.Notice)
                {
                    closeElementName = EditorGUILayout.TextField(L.Get("editor.PageWizard.ExistingView.9267775c67"), closeElementName);
                }
                EditorGUILayout.HelpBox(L.Get("editor.PageWizard.ExistingView.358fc7efeb"), MessageType.Info);
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
            var key = (existingPrefab.GetInstanceID(), path, backend.Id, titleElementName, closeElementName, preset);
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
                existingValidationError = L.Get("editor.PageWizard.ExistingView.b0b19dece0") + error.Message;
            }
            return existingValidationError;
        }

        private string InspectExistingView(string path, PageTextBackend backend)
        {
            if (string.IsNullOrEmpty(path) || !PrefabUtility.IsPartOfPrefabAsset(existingPrefab) ||
                PrefabUtility.GetPrefabAssetType(existingPrefab) == PrefabAssetType.Model ||
                AssetDatabase.LoadAssetAtPath<GameObject>(path) != existingPrefab)
            {
                return L.Get("editor.PageWizard.ExistingView.d95a7db4f5");
            }
            var view = existingPrefab.GetComponent<View>();
            if (view == null || existingPrefab.GetComponent<RectTransform>() == null)
            {
                return L.Get("editor.PageWizard.ExistingView.b5f1d7bd40");
            }
            if (string.IsNullOrWhiteSpace(titleElementName) ||
                preset != PagePreset.Notice && string.IsNullOrWhiteSpace(closeElementName))
            {
                return L.Get("editor.PageWizard.ExistingView.36ea1f9e66");
            }
            var entries = new List<BindingEntry>
            {
                new BindingEntry("Title", titleElementName, backend.ElementType, "Content", BindingMode.OneWay)
            };
            if (preset == PagePreset.Notice)
            {
                if (existingPrefab.GetComponentsInChildren<UnityEngine.UI.Selectable>(true).Length != 0)
                {
                    return L.Get("editor.PageWizard.ExistingView.885a18c4c8");
                }
                foreach (var graphic in existingPrefab.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                {
                    if (graphic.raycastTarget)
                    {
                        return L.Get("editor.PageWizard.ExistingView.2585437490");
                    }
                }
            }
            else
            {
                entries.Add(new BindingEntry("Close", closeElementName, typeof(ButtonElement), nameof(ButtonElement.Clicked),
                    BindingMode.OneWay, BindingEntryKind.Command));
            }
            var manifest = new BindingManifest(typeof(ViewModel), entries);
            var errors = ViewContractValidator.Validate(view, manifest);
            return errors.Count == 0 ? null : L.Get("editor.PageWizard.ExistingView.e7310d8579") + string.Join("\n", errors);
        }
    }
}
