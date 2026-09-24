using System;
using System.Collections.Generic;
using MUI.Navigation;
using UnityEngine;

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
                throw new InvalidOperationException("构建目录采集已经结束。");
            }
            if (route == null || prefab == null || manifest == null)
            {
                throw new ArgumentException("构建页面必须提供路由、Prefab 和绑定 Manifest。");
            }
            if (manifest.ViewModelType != typeof(TViewModel))
            {
                throw new ArgumentException("绑定 Manifest 的模型类型与路由不一致。", nameof(manifest));
            }
            if (pages.Count >= 2048)
            {
                throw new InvalidOperationException("单个 UI 构建目录最多登记 2048 个页面。");
            }
            if (!routes.Add(route))
            {
                throw new InvalidOperationException("同一路由不能重复登记到构建目录：" + route.Key);
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
