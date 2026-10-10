using System;
using System.Collections.Generic;
using MUI.Navigation;
using UnityEngine;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    /// <summary>一个独立导航配置的构建校验目录；登记元数据，不创建模型、Presenter 或资源实例。</summary>
    public sealed class UIBuildCatalog
    {
        private readonly List<Page> pages = new List<Page>();
        private readonly HashSet<Route> routes = new HashSet<Route>();
        private bool sealedCatalog;

        internal IReadOnlyList<Page> Pages => pages;

        /// <summary>显式提供生成工厂的 Manifest，类型必须与路由模型一致；依赖页面也须登记。</summary>
        public void AddPage<TViewModel, TArgs, TResult>(Route<TViewModel, TArgs, TResult> route,
            GameObject prefab, BindingManifest manifest) where TViewModel : ViewModel
        {
            if (sealedCatalog)
            {
                throw new InvalidOperationException(L.Get("editor.UIBuildCatalog.90e3c22510"));
            }
            if (route == null || prefab == null || manifest == null)
            {
                throw new ArgumentException(L.Get("editor.UIBuildCatalog.92487267c4"));
            }
            if (manifest.ViewModelType != typeof(TViewModel))
            {
                throw new ArgumentException(L.Get("editor.UIBuildCatalog.c378f5ca0a"), nameof(manifest));
            }
            if (pages.Count >= 2048)
            {
                throw new InvalidOperationException(L.Get("editor.UIBuildCatalog.3b1f870adc"));
            }
            if (!routes.Add(route))
            {
                throw new InvalidOperationException(L.Get("editor.UIBuildCatalog.b403f3aa8b") + route.Key);
            }
            pages.Add(new Page { Route = route, Prefab = prefab, Manifest = manifest });
        }

        internal void Seal() => sealedCatalog = true;

        internal void Release()
        {
            sealedCatalog = true;
            pages.Clear();
            routes.Clear();
        }

        internal sealed class Page
        {
            internal Route Route;
            internal GameObject Prefab;
            internal BindingManifest Manifest;
        }
    }
}
