using System;
using System.Collections.Generic;
using MUI.Navigation;
using MUI.Resources;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    /// <summary>项目通过编辑器初始化入口显式登记目录，手动检查与构建检查共用同一实现。</summary>
    public static class UIBuildValidation
    {
        private static readonly SortedDictionary<string, Action<UIBuildCatalog>> catalogs =
            new SortedDictionary<string, Action<UIBuildCatalog>>(StringComparer.Ordinal);
        private static bool validating;

        public static void RegisterCatalog(string id, Action<UIBuildCatalog> collect)
        {
            if (validating)
            {
                throw new InvalidOperationException(L.Get("editor.UIBuildValidation.4a2b85f80b"));
            }
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || collect == null)
            {
                throw new ArgumentException(L.Get("editor.UIBuildValidation.90508dc602"));
            }
            if (catalogs.Count >= 32)
            {
                throw new InvalidOperationException(L.Get("editor.UIBuildValidation.fcab6aa44c"));
            }
            catalogs.Add(id, collect);
            UIIncrementalValidation.RequestAll();
        }

        public static bool UnregisterCatalog(string id)
        {
            if (validating)
            {
                throw new InvalidOperationException(L.Get("editor.UIBuildValidation.4a2b85f80b"));
            }
            var removed = catalogs.Remove(id);
            if (removed)
            {
                UIIncrementalValidation.RequestAll();
            }
            return removed;
        }

        /// <summary>执行已登记采集回调并只读校验，未登记的页面不能被认定已覆盖。</summary>
        public static UIBuildValidationReport Validate() => ValidateAffected(null);

        /// <summary>每次重新采集目录；路径仅筛选受影响目录，不缓存校验结果或项目对象。</summary>
        internal static UIBuildValidationReport ValidateAffected(ISet<string> affectedPaths)
        {
            if (validating)
            {
                throw new InvalidOperationException(L.Get("editor.UIBuildValidation.20787e9cdf"));
            }
            validating = true;
            var report = new UIBuildValidationReport { CatalogCount = catalogs.Count };
            try
            {
                foreach (var registration in catalogs)
                {
                    var catalog = new UIBuildCatalog();
                    try
                    {
                        registration.Value(catalog);
                        catalog.Seal();
                        if (IsAffected(catalog, affectedPaths))
                        {
                            ValidateCatalog(registration.Key, catalog, report);
                        }
                    }
                    catch (Exception error)
                    {
                        report.Issue(registration.Key + L.Get("editor.UIBuildValidation.a776bc1b72") + error.Message);
                    }
                    finally
                    {
                        catalog.Release();
                    }
                }
                return report;
            }
            finally
            {
                validating = false;
            }
        }

        private static bool IsAffected(UIBuildCatalog catalog, ISet<string> affectedPaths)
        {
            if (affectedPaths == null || catalog.Pages.Count == 0)
            {
                return true;
            }
            foreach (var page in catalog.Pages)
            {
                var path = AssetDatabase.GetAssetPath(page.Prefab);
                if (string.IsNullOrEmpty(path))
                {
                    return true;
                }
                foreach (var dependency in AssetDatabase.GetDependencies(path, true))
                {
                    if (affectedPaths.Contains(dependency))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static void ValidateCatalog(string id, UIBuildCatalog catalog, UIBuildValidationReport report)
        {
            report.PageCount += catalog.Pages.Count;
            if (catalog.Pages.Count == 0)
            {
                report.Issue(id + L.Get("editor.UIBuildValidation.81196b916d"));
                return;
            }
            var registeredRoutes = new HashSet<Route>();
            foreach (var page in catalog.Pages)
            {
                registeredRoutes.Add(page.Route);
            }
            var completeGraph = true;
            var resources = new Dictionary<ViewResource, GameObject>();
            foreach (var page in catalog.Pages)
            {
                foreach (var dependency in page.Route.DependencyDescriptors)
                {
                    if (!registeredRoutes.Contains(dependency.Target))
                    {
                        completeGraph = false;
                        report.Issue(id + "/" + page.Route.Key + L.Get("editor.UIBuildValidation.061c7b88d2") + dependency.Target.Key);
                    }
                }
                if (resources.TryGetValue(page.Route.Resource, out var prefab) && prefab != page.Prefab)
                {
                    report.Issue(id + L.Get("editor.UIBuildValidation.99a69df3cd") + page.Route.Resource);
                }
                else
                {
                    resources[page.Route.Resource] = page.Prefab;
                }
            }
            // 先要求依赖闭合在有界目录内，避免从漏登记的根无限扩展未受控图。
            report.RouteCount += registeredRoutes.Count;
            var visited = new HashSet<Route>();
            var keys = new Dictionary<string, Route>(StringComparer.Ordinal);
            foreach (var page in catalog.Pages)
            {
                if (completeGraph && !visited.Contains(page.Route))
                {
                    var graph = RouteGraphValidator.Validate(page.Route, maxIssues: 256);
                    foreach (var issue in graph.Issues)
                    {
                        report.Issue(id + "/" + issue.RouteKey + "：" + issue.Message);
                    }
                    if (graph.IsTruncated)
                    {
                        report.Issue(id + L.Get("editor.UIBuildValidation.83ee63aae8"));
                    }
                    foreach (var route in graph.Routes)
                    {
                        if (!visited.Add(route))
                        {
                            continue;
                        }
                        if (keys.TryGetValue(route.Key, out var previous) && !ReferenceEquals(previous, route))
                        {
                            report.Issue(id + L.Get("editor.UIBuildValidation.e213655721") + route.Key);
                        }
                        else
                        {
                            keys[route.Key] = route;
                        }
                    }
                }
                ValidatePage(id, page, report);
            }
        }

        private static void ValidatePage(string id, UIBuildCatalog.Page page, UIBuildValidationReport report)
        {
            var prefix = id + "/" + page.Route.Key + "：";
            var path = AssetDatabase.GetAssetPath(page.Prefab);
            if (page.Prefab == null || string.IsNullOrEmpty(path) ||
                !PrefabUtility.IsPartOfPrefabAsset(page.Prefab) ||
                PrefabUtility.GetPrefabAssetType(page.Prefab) == PrefabAssetType.Model ||
                AssetDatabase.LoadAssetAtPath<GameObject>(path) != page.Prefab)
            {
                report.Issue(prefix + L.Get("editor.UIBuildValidation.c935d0e33a"));
                return;
            }
            var view = page.Prefab.GetComponent<View>();
            if (view == null || page.Prefab.GetComponent<RectTransform>() == null)
            {
                report.Issue(prefix + L.Get("editor.UIBuildValidation.06e2bd7c7c"));
                return;
            }
            foreach (var error in ViewContractValidator.Validate(view, page.Manifest))
            {
                report.Issue(prefix + path + "：" + error);
            }
        }

        [MenuItem("Tools/MUI/校验构建目录 (Validate Build Catalogs)")]
        private static void ValidateFromMenu()
        {
            var result = Validate();
            if (result.IsValid)
            {
                Debug.Log(result.ExportText());
            }
            else
            {
                Debug.LogWarning(result.ExportText());
            }
        }
    }
}
