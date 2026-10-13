using System;
using System.Collections.Generic;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class UIHost
    {
        private static UIHost current;
        private readonly Dictionary<Type, object> pageDefinitions = new Dictionary<Type, object>();
        private readonly Dictionary<Type, Delegate> pageFactories = new Dictionary<Type, Delegate>();
        private readonly HashSet<Type> creatingPages = new HashSet<Type>();

        /// <summary>唯一已初始化且仍运行的宿主；必须在主线程访问。</summary>
        public static UIHost Current
        {
            get
            {
                UnityMainThread.Require();
                if (current == null || current.destroyed || current.initializing || !current.IsInitialized || current.IsShutdown)
                    throw new InvalidOperationException("Initialize a UIHost before opening a page.");
                return current;
            }
        }

        private void ClaimGlobalHost()
        {
            if (current != null && current != this && !current.destroyed && !current.IsShutdown)
                throw new InvalidOperationException("Only one active UIHost is allowed.");
            current = this;
        }

        private void ReleaseGlobalHost()
        {
            if (current == this) current = null;
            pageDefinitions.Clear();
            pageFactories.Clear();
            creatingPages.Clear();
        }

        /// <summary>页面首次使用前配置工厂，配置仅属于当前宿主生命周期。</summary>
        public void ConfigurePageDefinition<TDefinition>(Type page, Func<TDefinition> factory) where TDefinition : class
        {
            RequirePageAccess();
            if (page == null) throw new ArgumentNullException(nameof(page));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (pageDefinitions.ContainsKey(page) || creatingPages.Contains(page))
                throw new InvalidOperationException("A page definition cannot be replaced after first use.");
            pageFactories[page] = factory;
        }

        /// <summary>供生成的页面入口使用；页面定义按宿主缓存，重建宿主后重新创建。</summary>
        public TDefinition GetPageDefinition<TDefinition>(Type page, Func<TDefinition> defaultFactory) where TDefinition : class
        {
            RequirePageAccess();
            if (page == null) throw new ArgumentNullException(nameof(page));
            if (pageDefinitions.TryGetValue(page, out var cached)) return (TDefinition)cached;
            var factory = pageFactories.TryGetValue(page, out var configured) ? (Func<TDefinition>)configured : defaultFactory;
            if (factory == null) throw new InvalidOperationException("Configure the page definition factory before opening: " + page.FullName);
            if (!creatingPages.Add(page)) throw new InvalidOperationException("Recursive page definition creation: " + page.FullName);
            try
            {
                var definition = factory();
                RequirePageAccess();
                if (definition == null) throw new InvalidOperationException("The page definition factory returned null: " + page.FullName);
                pageDefinitions.Add(page, definition);
                pageFactories.Remove(page);
                return definition;
            }
            finally
            {
                creatingPages.Remove(page);
            }
        }

        private void RequirePageAccess()
        {
            if (Current != this) throw new InvalidOperationException("This UIHost is not the active global host.");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetGlobalHost() => current = null;
    }
}
