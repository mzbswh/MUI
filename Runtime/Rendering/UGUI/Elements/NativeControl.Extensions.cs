using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>自定义适配器基类；监听自动随适配器释放，交互受 View 和 Host 门控。</summary>
    public abstract class NativeControl<TComponent> : NativeControl where TComponent : Component
    {
        protected NativeControl(string name, TComponent target) : base(name, target) { Component = target; }
        protected TComponent Component { get; }
    }

    /// <summary>生成属性包装的公共基类；通知转发到原控件，沿用绑定会话生命周期。</summary>
    public abstract class GeneratedPropertyAdapter<TComponent> where TComponent : Component
    {
        private readonly NativeControl owner;
        protected GeneratedPropertyAdapter(NativeControl owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            if (!(owner.Target is TComponent)) throw new ArgumentException("Generated adapter component type mismatch.");
        }
        protected TComponent Component
        {
            get
            {
                if (!owner.IsAlive) throw new ObjectDisposedException(owner.Name);
                return (TComponent)owner.Target;
            }
        }
        protected NativeControl Owner => owner;
        protected void ForwardChanges(string source, string target)
        {
            owner.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == source) Changed(target);
            };
        }
        protected void Changed(string property) => owner.PublishGeneratedPropertyChange(property);
        protected void Listen(UnityEvent signal, UnityAction callback) => owner.ListenExtension(signal, callback);
        protected void Listen<T>(UnityEvent<T> signal, UnityAction<T> callback) => owner.ListenExtension(signal, callback);
    }

    /// <summary>标记可在 Inspector 中发现的附加属性类；不替换控件适配器。</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ControlPropertyExtensionAttribute : Attribute { }

    /// <summary>属性实现会在变化时调用 Changed；允许双向及界面回写绑定。</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class NotifiesChangeAttribute : Attribute { }

    /// <summary>按需实例化的附加属性；通过 Listen 订阅输入，TrackCleanup 管理外部订阅。</summary>
    public abstract class ControlPropertyExtension<TComponent> : GeneratedPropertyAdapter<TComponent> where TComponent : Component
    {
        protected ControlPropertyExtension(NativeControl owner) : base(owner) { }
        protected new void ForwardChanges(string source, string target)
        {
            System.ComponentModel.PropertyChangedEventHandler listener = (sender, args) => { if (args.PropertyName == source) Changed(target); };
            Owner.PropertyChanged += listener;
            TrackCleanup(() => Owner.PropertyChanged -= listener);
        }
        protected new void Changed(string property) => Owner.PublishGeneratedPropertyChange(GetType().FullName + "." + property);
        protected bool CanReceiveInput() => Owner.CanReceiveExtensionInput();
        protected void TrackCleanup(Action cleanup) => Owner.TrackExtensionCleanup(cleanup);
        protected new void Listen(UnityEvent signal, UnityAction callback) => Owner.ListenExtension(signal, callback);
        protected new void Listen<T>(UnityEvent<T> signal, UnityAction<T> callback) => Owner.ListenExtension(signal, callback);
    }

    public interface ITextStyleElement : ITextElement
    {
        Color Color { get; set; }
        float FontSize { get; set; }
    }

    public interface IScrollElement : IElement
    {
        Vector2 NormalizedPosition { get; set; }
        Vector2 Velocity { get; set; }
        bool Horizontal { get; set; }
        bool Vertical { get; set; }
        event Action Scrolled;
    }

    public interface IImageElement : IElement
    {
        Sprite Sprite { get; set; }
        Color Color { get; set; }
        float FillAmount { get; set; }
    }

    public interface IRawImageElement : IElement
    {
        Texture Texture { get; set; }
        Color Color { get; set; }
    }

    public sealed class NativeScrollView : NativeControl<ScrollRect>, IScrollElement
    {
        private bool writing;
        public NativeScrollView(string name, ScrollRect target) : base(name, target)
        {
            Listen(target.onValueChanged, _ =>
            {
                if (writing) return;
                Changed(nameof(NormalizedPosition));
                Changed(nameof(Velocity));
                if (CanReceiveInput()) Scrolled?.Invoke();
            });
            TrackCleanup(() => Scrolled = null);
        }
        public event Action Scrolled;
        public Vector2 NormalizedPosition
        {
            get => Component.normalizedPosition;
            set
            {
                if (value == NormalizedPosition) return;
                writing = true;
                try { Component.normalizedPosition = value; }
                finally { writing = false; }
                Changed();
            }
        }
        public Vector2 Velocity { get => Component.velocity; set { Component.velocity = value; Changed(); } }
        public bool Horizontal { get => Component.horizontal; set { Component.horizontal = value; Changed(); } }
        public bool Vertical { get => Component.vertical; set { Component.vertical = value; Changed(); } }
    }

    public sealed class NativeRawImage : NativeControl<RawImage>, IRawImageElement
    {
        public NativeRawImage(string name, RawImage target) : base(name, target) { }
        public Texture Texture { get => Component.texture; set { if (Texture == value) return; Component.texture = value; Changed(); } }
        public Color Color { get => Component.color; set { if (Color == value) return; Component.color = value; Changed(); } }
    }
}
