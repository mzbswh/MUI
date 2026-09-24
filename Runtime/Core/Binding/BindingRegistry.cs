using System;
using System.Collections.Generic;

namespace MUI
{
    /// <summary>显式注册生成工厂，不扫描程序集或通过反射激活。</summary>
    public static class BindingRegistry
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<Type, Registration> Entries = new Dictionary<Type, Registration>();
        private static object generation = new object();

        /// <summary>注册内容代际；新增契约或重建注册表后使旧绑定缓存失效。</summary>
        internal static object Generation
        {
            get
            {
                lock (Gate)
                {
                    return generation;
                }
            }
        }

        public static void Register<TViewModel>(Func<IView, TViewModel, BindingContext<TViewModel>> factory, BindingManifest manifest)
                    where TViewModel : ViewModel
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            if (manifest.ViewModelType != typeof(TViewModel))
            {
                throw new ArgumentException("Manifest type mismatch.", nameof(manifest));
            }

            lock (Gate)
            {
                if (Entries.TryGetValue(typeof(TViewModel), out var existing))
                {
                    if (existing.Identity.Equals(factory))
                    {
                        return;
                    }

                    throw new InvalidOperationException($"Conflicting binding factory for {typeof(TViewModel).FullName}.");
                }

                Entries.Add(typeof(TViewModel), new Registration
                {
                    Identity = factory,
                    Factory = (view, model) => factory(view, (TViewModel)model),
                    Manifest = manifest
                });
                // 新增派生契约会改变原先回退基类的解析结果，旧界面缓存不能继续复用。
                generation = new object();
            }
        }

        /// <summary>优先使用实际类型契约，否则沿类继承链选择最近的已注册契约。</summary>
        public static BindingContext Create(IView view, ViewModel model)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            Registration registration;
            lock (Gate)
            {
                registration = Resolve(model.GetType());
            }

            return registration.Factory(view, model);
        }

        public static BindingManifest GetManifest(Type viewModelType)
        {
            if (viewModelType == null)
            {
                throw new ArgumentNullException(nameof(viewModelType));
            }

            lock (Gate)
            {
                return Resolve(viewModelType).Manifest;
            }
        }

        /// <summary>
        /// 按声明的模型类型创建强类型绑定，用于顶层 Route。
        /// 不回退基类或选择派生类，避免泛型 BindingContext 的无效转换。
        /// </summary>
        public static BindingContext<TViewModel> CreateExact<TViewModel>(IView view, TViewModel model)
            where TViewModel : ViewModel
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            Registration registration;
            lock (Gate)
            {
                if (!Entries.TryGetValue(typeof(TViewModel), out registration))
                {
                    throw new InvalidOperationException($"未注册类型 {typeof(TViewModel).FullName} 的绑定工厂；强类型路由需要该类型的契约或显式工厂。");
                }
            }

            // 锁内只读取注册数据；项目工厂在锁外执行。
            var factory = (Func<IView, TViewModel, BindingContext<TViewModel>>)registration.Identity;
            return factory(view, model);
        }

        /// <summary>调用方必须持有 Gate；清单和实例创建共用相同的解析规则。</summary>
        private static Registration Resolve(Type viewModelType)
        {
            if (!typeof(ViewModel).IsAssignableFrom(viewModelType))
            {
                throw new ArgumentException("绑定契约类型必须继承 ViewModel。", nameof(viewModelType));
            }

            for (var type = viewModelType; type != null && typeof(ViewModel).IsAssignableFrom(type); type = type.BaseType)
            {
                if (Entries.TryGetValue(type, out var registration))
                {
                    return registration;
                }
            }

            throw new InvalidOperationException($"类型 {viewModelType.FullName} 及其基类均未注册绑定工厂。");
        }

        /// <summary>禁用域重载时，由宿主启动流程在进入运行模式时调用。</summary>
        public static void Reset()
        {
            lock (Gate)
            {
                Entries.Clear();
                generation = new object();
            }
        }

        private sealed class Registration
        {
            public Delegate Identity;
            public Func<IView, ViewModel, BindingContext> Factory;
            public BindingManifest Manifest;
        }
    }
}
