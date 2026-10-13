using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using Component = UnityEngine.Component;

namespace MUI.Unity
{
    /// <summary>由 View 持有的普通 C# 适配器，无需挂载 Element 组件。</summary>
    public abstract class NativeControl : IComponentElement, IBindingPropertyPolicy, IDisposable
    {
        private static readonly Dictionary<Type, Func<string, Component, NativeControl>> factories = new Dictionary<Type, Func<string, Component, NativeControl>>();
        private readonly Dictionary<(UnityEventBase Signal, Action Handler), Action> commandListeners =
            new Dictionary<(UnityEventBase, Action), Action>();

        private readonly Dictionary<string, string> writeAliases = new Dictionary<string, string>(StringComparer.Ordinal);
        internal void RegisterWriteAlias(string property, string target) => writeAliases[property] = target;
        public string GetBindingWriteTarget(string property, BindingMode mode)
        {
            if (!writeAliases.TryGetValue(property, out var target)) return property;
            if (mode != BindingMode.OneWay && mode != BindingMode.OneTime)
                throw new InvalidOperationException("资源键仅支持 OneWay 或 OneTime 绑定。");
            return target;
        }
        private Func<bool> inputAllowed;
        private Action capturePointer;
        public void ConfigureInput(Func<bool> allowed, Action capture = null)
        {
            if (disposed) throw new ObjectDisposedException(Name);
            inputAllowed = allowed ?? throw new ArgumentNullException(nameof(allowed));
            capturePointer = capture;
        }
        private bool disposed;
        private Dictionary<Type, object> propertyAdapters;
        private readonly List<Action> cleanup = new List<Action>();
        protected NativeControl(string name, Component target)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Binding name is required.", nameof(name));
            if (target == null) throw new ArgumentNullException(nameof(target));
            Name = name; Target = target; ComponentType = target.GetType();
        }
        public string Name { get; }
        public Component Target { get; }
        public Type ComponentType { get; internal set; }
        object IComponentElement.ComponentTarget => Target;
        public bool IsAlive => !disposed && Target != null;
        public event PropertyChangedEventHandler PropertyChanged;
        public bool Visible { get => Target.gameObject.activeSelf; set { if (Target.gameObject.activeSelf == value) return; Target.gameObject.SetActive(value); Changed(); } }
        protected void Changed([CallerMemberName] string property = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        /// <summary>缓存编译器生成的属性包装；每个控件实例只初始化一次。</summary>
        public T GetPropertyAdapter<T>(Func<NativeControl, T> create) where T : class
        {
            if (!IsAlive) throw new ObjectDisposedException(Name);
            if (propertyAdapters == null) propertyAdapters = new Dictionary<Type, object>();
            if (!propertyAdapters.TryGetValue(typeof(T), out var value))
            {
                var cleanupStart = cleanup.Count;
                try
                {
                    value = create(this) ?? throw new InvalidOperationException("Generated property adapter factory returned null.");
                    propertyAdapters.Add(typeof(T), value);
                }
                catch
                {
                    for (var i = cleanup.Count - 1; i >= cleanupStart; i--)
                    {
                        try { cleanup[i](); } catch { /* Preserve initialization failure. */ }
                        cleanup.RemoveAt(i);
                    }
                    throw;
                }
            }
            return (T)value;
        }
        internal void PublishGeneratedPropertyChange(string name) { if (IsAlive) Changed(name); }
        internal bool CanReceiveExtensionInput() => CanReceiveInput();
        internal void TrackExtensionCleanup(Action action) => TrackCleanup(action);
        internal void ListenExtension(UnityEvent signal, UnityAction callback) => Listen(signal, callback);
        internal void ListenExtension<T>(UnityEvent<T> signal, UnityAction<T> callback) => Listen(signal, callback);

        private Action CommandDispatch(Action handler, bool capturePointer)
        {
            return () =>
            {
                if (!CanReceiveInput()) return;
                if (capturePointer)
                {
                    this.capturePointer?.Invoke();
                }
                handler();
            };
        }

        private void RegisterCommandListener(UnityEventBase signal, Action handler, Action subscribe, Action unsubscribe)
        {
            if (signal == null || handler == null) throw new ArgumentNullException();
            if (!IsAlive) throw new ObjectDisposedException(Name);
            commandListeners.Add((signal, handler), unsubscribe);
            try { subscribe(); }
            catch { commandListeners.Remove((signal, handler)); throw; }
        }

        public void AddCommandListener(UnityEvent signal, Action handler)
        {
            var dispatch = CommandDispatch(handler, capturePointer: true);
            UnityAction listener = () => dispatch();
            RegisterCommandListener(signal, handler, () => signal.AddListener(listener), () => signal.RemoveListener(listener));
        }

        public void AddCommandListener<T0>(UnityEvent<T0> signal, Action handler)
        {
            var dispatch = CommandDispatch(handler, capturePointer: false);
            UnityAction<T0> listener = (arg0) => dispatch();
            RegisterCommandListener(signal, handler, () => signal.AddListener(listener), () => signal.RemoveListener(listener));
        }

        public void AddCommandListener<T0, T1>(UnityEvent<T0, T1> signal, Action handler)
        {
            var dispatch = CommandDispatch(handler, capturePointer: false);
            UnityAction<T0, T1> listener = (arg0, arg1) => dispatch();
            RegisterCommandListener(signal, handler, () => signal.AddListener(listener), () => signal.RemoveListener(listener));
        }

        public void AddCommandListener<T0, T1, T2>(UnityEvent<T0, T1, T2> signal, Action handler)
        {
            var dispatch = CommandDispatch(handler, capturePointer: false);
            UnityAction<T0, T1, T2> listener = (arg0, arg1, arg2) => dispatch();
            RegisterCommandListener(signal, handler, () => signal.AddListener(listener), () => signal.RemoveListener(listener));
        }

        public void AddCommandListener<T0, T1, T2, T3>(UnityEvent<T0, T1, T2, T3> signal, Action handler)
        {
            var dispatch = CommandDispatch(handler, capturePointer: false);
            UnityAction<T0, T1, T2, T3> listener = (arg0, arg1, arg2, arg3) => dispatch();
            RegisterCommandListener(signal, handler, () => signal.AddListener(listener), () => signal.RemoveListener(listener));
        }

        public void RemoveCommandListener(UnityEventBase signal, Action handler)
        {
            if (!commandListeners.TryGetValue((signal, handler), out var unsubscribe)) return;
            unsubscribe();
            commandListeners.Remove((signal, handler));
        }

        public virtual void Dispose()
        {
            if (disposed) return;
            disposed = true;
            List<Exception> errors = null;
            foreach (var unsubscribe in commandListeners.Values)
                try { unsubscribe(); } catch (Exception error) { (errors ?? (errors = new List<Exception>())).Add(error); }
            commandListeners.Clear();
            PropertyChanged = null;
            inputAllowed = null;
            capturePointer = null;
            propertyAdapters?.Clear();
            for (var i = cleanup.Count - 1; i >= 0; i--)
                try { cleanup[i](); } catch (Exception error) { (errors ?? (errors = new List<Exception>())).Add(error); }
            cleanup.Clear();
            if (errors != null) throw new AggregateException(errors);
        }
        protected void TrackCleanup(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (disposed) throw new ObjectDisposedException(GetType().Name);
            cleanup.Add(action);
        }
        protected bool CanReceiveInput()
        {
            if (!IsAlive || !Target.gameObject.activeInHierarchy) return false;
            if (Target is Behaviour behaviour && !behaviour.isActiveAndEnabled) return false;
            return inputAllowed == null || inputAllowed();
        }

        protected void Listen<T>(UnityEvent<T> signal, UnityAction<T> callback)
        {
            if (signal == null || callback == null) throw new ArgumentNullException();
            UnityAction<T> listener = value => { if (CanReceiveInput()) callback(value); };
            TrackCleanup(() => signal.RemoveListener(listener));
            signal.AddListener(listener);
        }
        protected void Listen(UnityEvent signal, UnityAction callback)
        {
            if (signal == null || callback == null) throw new ArgumentNullException();
            UnityAction listener = () => { if (CanReceiveInput()) callback(); };
            TrackCleanup(() => signal.RemoveListener(listener));
            signal.AddListener(listener);
        }
        public static void Register<T>(Func<string, T, NativeControl> factory) where T : Component
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            factories[typeof(T)] = (name, target) => factory(name, (T)target);
        }
        public static bool Unregister<T>() where T : Component => factories.Remove(typeof(T));
        public static NativeControl Create(string name, Component target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            // Exact type first, then nearest registered base; registration order never determines subtype matching.
            for (var type = target.GetType(); type != null && typeof(Component).IsAssignableFrom(type); type = type.BaseType)
                if (factories.TryGetValue(type, out var factory))
                {
                    var adapter = factory(name, target);
                    if (adapter == null) throw new InvalidOperationException("Adapter factory returned null: " + type.FullName);
                    if (!adapter.IsAlive || adapter.Target != target || adapter.Name != name)
                    { adapter.Dispose(); throw new InvalidOperationException("Adapter factory changed the binding identity."); }
                    return adapter;
                }
            return new NativeComponent(name, target);
        }
    }

    /// <summary>未注册组件的最小引用适配器；属性可由生成器直接访问，附加语义通过属性扩展提供。</summary>
    public sealed class NativeComponent : NativeControl
    {
        public NativeComponent(string name, Component target) : base(name, target) { }
    }

}
