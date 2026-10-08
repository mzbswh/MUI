using System;
using System.Collections.Generic;
using MUI.Navigation;
using MUI.Resources;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;

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
                throw new InvalidOperationException("校验期间不能修改 UI 构建目录登记。");
            }
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || collect == null)
            {
                throw new ArgumentException("目录需要不超过 128 字符的标识和采集回调。");
            }
            if (catalogs.Count >= 32)
            {
                throw new InvalidOperationException("UI 构建目录数量超过 32。");
            }
            catalogs.Add(id, collect);
        }

        public static bool UnregisterCatalog(string id)
        {
            if (validating)
            {
                throw new InvalidOperationException("校验期间不能修改 UI 构建目录登记。");
            }
            return catalogs.Remove(id);
        }

        /// <summary>执行已登记采集回调并只读校验，未登记的页面不能被认定已覆盖。</summary>
        public static UIBuildValidationReport Validate()
        {
            if (validating)
            {
                throw new InvalidOperationException("UI 构建校验不能重入。");
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
                        ValidateCatalog(registration.Key, catalog, report);
                    }
                    catch (Exception error)
                    {
                        report.Issue(registration.Key + "：目录采集或校验失败：" + error.Message);
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

        private static void ValidateCatalog(string id, UIBuildCatalog catalog, UIBuildValidationReport report)
        {
            report.PageCount += catalog.Pages.Count;
            if (catalog.Pages.Count == 0)
            {
                report.Issue(id + "：已登记目录未提供任何页面。");
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
                        report.Issue(id + "/" + page.Route.Key + "：依赖路由尚未登记 Prefab/Manifest：" + dependency.Target.Key);
                    }
                }
                if (resources.TryGetValue(page.Route.Resource, out var prefab) && prefab != page.Prefab)
                {
                    report.Issue(id + "：同一资源键和版本关联到不同 Prefab：" + page.Route.Resource);
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
                        report.Issue(id + "：路由图问题已截断，请先修复现有问题后重新校验。");
                    }
                    foreach (var route in graph.Routes)
                    {
                        if (!visited.Add(route))
                        {
                            continue;
                        }
                        if (keys.TryGetValue(route.Key, out var previous) && !ReferenceEquals(previous, route))
                        {
                            report.Issue(id + "：多个不同路由使用同一键：" + route.Key);
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
                report.Issue(prefix + "页面必须关联普通或 Variant Prefab 根资产。");
                return;
            }
            var view = page.Prefab.GetComponent<View>();
            if (view == null || page.Prefab.GetComponent<RectTransform>() == null)
            {
                report.Issue(prefix + "Prefab 根缺少 View 或 RectTransform。");
                return;
            }
            foreach (var error in ViewContractValidator.Validate(view, page.Manifest))
            {
                report.Issue(prefix + path + "：" + error);
            }
        }

        [MenuItem("Tools/MUI/Validate Build Catalogs")]
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
