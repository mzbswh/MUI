using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Component = UnityEngine.Component;

namespace MUI.UGUI
{
    /// <summary>由 View 持有的普通 C# 适配器，无需挂载 Element 组件。</summary>
    public abstract class NativeControl : IElement, IDisposable
    {
        private static readonly Dictionary<Type, Func<string, Component, NativeControl>> factories = new Dictionary<Type, Func<string, Component, NativeControl>>();
        private bool disposed;
        private Dictionary<Type, object> propertyAdapters;
        private readonly List<Action> cleanup = new List<Action>();
        protected NativeControl(string name, Component target)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Binding name is required.", nameof(name));
            if (target == null) throw new ArgumentNullException(nameof(target));
            Name = name; Target = target;
        }
        public string Name { get; }
        public Component Target { get; }
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

        public virtual void Dispose()
        {
            if (disposed) return;
            disposed = true;
            PropertyChanged = null;
            propertyAdapters?.Clear();
            List<Exception> errors = null;
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
            if (Target is Selectable selectable && !selectable.IsInteractable()) return false;
            var view = Target.GetComponentInParent<View>(true);
            if (view == null || !view.IsInputEnabled || !view.OwnsNativeControl(Target)) return false;
            var host = view.GetComponentInParent<UIHost>(true);
            return host == null || host.CanReceiveSharedPointerInput();
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
            if (target is ScrollRect scroll) return new NativeScrollView(name, scroll);
            if (target is RawImage raw) return new NativeRawImage(name, raw);
            if (target is InputField input) return new NativeInputField(name, input);
            if (target is Dropdown dropdown) return new NativeDropdown(name, dropdown);
            if (target is Button button) return new NativeButton(name, button);
            if (target is Text text) return new NativeText(name, text);
            if (target is Image image) return new NativeImage(name, image);
            if (target is MonoBehaviour && !(target is Element) && !(target is View)) return new NativeComponent(name, target);
            throw new ArgumentException("不支持的原生绑定组件：" + target.GetType().Name);
        }
    }

    /// <summary>未注册组件的最小引用适配器；属性可由生成器直接访问，附加语义通过属性扩展提供。</summary>
    public sealed class NativeComponent : NativeControl
    {
        public NativeComponent(string name, Component target) : base(name, target) { }
    }

    public sealed class NativeButton : NativeControl, IButtonElement
    {
        private readonly Button button;
        private readonly Dictionary<Action, UnityAction> listeners = new Dictionary<Action, UnityAction>();
        public NativeButton(string name, Button target) : base(name, target) { button = target; }
        public bool Interactable { get => button.interactable; set { if (button.interactable == value) return; button.interactable = value; Changed(); } }
        public event Action Clicked
        {
            add
            {
                UnityAction listener = () =>
                {
                    var owner = button == null ? null : button.GetComponentInParent<View>(true);
                    if (!IsAlive || owner == null || !owner.IsInputEnabled || !button.isActiveAndEnabled || !button.IsInteractable()) return;
                    var host = owner.GetComponentInParent<UIHost>(true);
                    if (host != null && !host.CanReceiveSharedPointerInput()) return;
                    var gesture = button.GetComponent<NativeInputGesture>();
                    if (gesture != null) gesture.CaptureClickForModal();
                    value();
                };
                listeners.Add(value, listener);
                button.onClick.AddListener(listener);
            }
            remove
            {
                if (!listeners.TryGetValue(value, out var listener)) return;
                if (button != null) button.onClick.RemoveListener(listener);
                listeners.Remove(value);
            }
        }
        public override void Dispose()
        {
            if (button != null) foreach (var listener in listeners.Values) button.onClick.RemoveListener(listener);
            listeners.Clear();
            base.Dispose();
        }
    }

    public sealed class NativeText : NativeControl, ITextStyleElement
    {
        private readonly Text text;
        public float FontSize { get => text.fontSize; set { var size = Mathf.RoundToInt(value); if (text.fontSize == size) return; text.fontSize = size; Changed(); } }
        public NativeText(string name, Text target) : base(name, target) { text = target; }
        public string Content { get => text.text; set { if (text.text == value) return; text.text = value; Changed(); } }
        public Color Color { get => text.color; set { if (text.color == value) return; text.color = value; Changed(); } }
    }

    public sealed class NativeImage : NativeControl, IImageElement
    {
        private readonly Image image;
        public NativeImage(string name, Image target) : base(name, target) { image = target; }
        public Sprite Sprite { get => image.sprite; set { if (image.sprite == value) return; image.sprite = value; Changed(); } }
        public Color Color { get => image.color; set { if (image.color == value) return; image.color = value; Changed(); } }
        public float FillAmount { get => image.fillAmount; set { if (image.fillAmount == value) return; image.fillAmount = value; Changed(); } }
    }
}
