using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.Widgets
{
    /// <summary>生成代码登记类型化 Widget 工厂；渲染适配器不使用反射创建业务对象。</summary>
    public static class WidgetRegistry
    {
        private static readonly Dictionary<Type, IFactory> factories = new Dictionary<Type, IFactory>();
        private static readonly object gate = new object();

        static WidgetRegistry()
        {
            BindingRegistry.ResetCompleted += Reset;
        }

        public static void Register<TVM, TArgs>(Func<TVM, Presenter<TVM, TArgs, Unit>> presenterFactory,
            Func<IView, TVM, BindingContext> bindingFactory, bool replace = false) where TVM : ViewModel
        {
            if (presenterFactory == null)
                throw new ArgumentNullException(nameof(presenterFactory));
            if (bindingFactory == null)
                throw new ArgumentNullException(nameof(bindingFactory));
            lock (gate)
            {
                if (!replace && factories.ContainsKey(typeof(TVM)))
                    return;
                factories[typeof(TVM)] = new Factory<TVM, TArgs>(presenterFactory, bindingFactory);
            }
        }

        public static bool IsRegistered(Type model)
        {
            lock (gate)
                return factories.ContainsKey(model);
        }

        public static void Reset()
        {
            lock (gate)
                factories.Clear();
        }

        public static ValueTask<WidgetHandle> PrepareAsync(WidgetScope scope, IViewProvider provider,
            ViewResource resource, ViewModel vm, object args, CancellationToken token = default)
        {
            if (vm == null)
                throw new ArgumentNullException(nameof(vm));
            IFactory factory;
            lock (gate)
                factories.TryGetValue(vm.GetType(), out factory);
            return factory == null ? PrepareLegacyAsync(scope, provider, resource, vm, args, token)
                : factory.PrepareAsync(scope, provider, resource, vm, args, token);
        }

        private static async ValueTask<WidgetHandle> PrepareLegacyAsync(WidgetScope scope, IViewProvider provider,
            ViewResource resource, ViewModel vm, object args, CancellationToken token)
        {
            if (args != null && !(args is Unit))
                throw new InvalidOperationException("Register the typed Widget factory before passing Widget args.");
            var definition = new WidgetDefinition<ViewModel, Unit>(resource,
                () => throw new InvalidOperationException("An assigned Widget VM is required."), BindingRegistry.Create);
            return await scope.PrepareAsync(definition, provider, Unit.Value, vm, token);
        }

        private interface IFactory
        {
            ValueTask<WidgetHandle> PrepareAsync(WidgetScope scope, IViewProvider provider,
                ViewResource resource, ViewModel vm, object args, CancellationToken token);
        }

        private sealed class Factory<TVM, TArgs> : IFactory where TVM : ViewModel
        {
            private readonly Func<TVM, Presenter<TVM, TArgs, Unit>> presenter;
            private readonly Func<IView, TVM, BindingContext> binding;

            internal Factory(Func<TVM, Presenter<TVM, TArgs, Unit>> presenter, Func<IView, TVM, BindingContext> binding)
            {
                this.presenter = presenter;
                this.binding = binding;
            }

            public async ValueTask<WidgetHandle> PrepareAsync(WidgetScope scope, IViewProvider provider,
                ViewResource resource, ViewModel vm, object args, CancellationToken token)
            {
                if (args != null && !(args is TArgs))
                    throw new ArgumentException("Widget args must be " + typeof(TArgs).FullName);
                var definition = new WidgetDefinition<TVM, TArgs>(resource,
                    () => throw new InvalidOperationException("An assigned Widget VM is required."), binding, presenter);
                return await scope.PrepareAsync(definition, provider, args == null ? default : (TArgs)args, (TVM)vm, token);
            }
        }
    }
}
