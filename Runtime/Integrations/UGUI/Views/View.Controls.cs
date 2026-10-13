using System;
using MUI.Unity;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class View
    {
        /// <summary>程序化构造 View 时登记现有组件；必须在 View 初始化前完成。</summary>
        public void RegisterControl(string name, Component target, Type componentType = null)
        {
            RequireAlive();
            if (index != null || initializing) throw new InvalidOperationException("请在 View 初始化前登记控件。");
            if (string.IsNullOrWhiteSpace(name) || !OwnsNativeControl(target)) throw new ArgumentException("控件名称无效或不属于当前 View。");
            var type = componentType ?? target.GetType();
            if (!type.IsInstanceOfType(target)) throw new ArgumentException("组件类型不匹配。");
            foreach (var binding in nativeBindings)
            {
                if (binding.Target == target && binding.Name == name && binding.BindingType == type) return;
                if (binding.Target == target || binding.Name == name && binding.BindingType == type)
                    throw new InvalidOperationException("控件引用或名称与类型重复。");
            }
            nativeBindings.Add(new NativeControlBinding { Name = name, Target = target, ComponentType = type.AssemblyQualifiedName });
        }
        public TComponent GetControl<TComponent>(string name) where TComponent : Component =>
            (TComponent)GetElement<NativeControl>(name, typeof(TComponent)).Target;
    }

    public static class ViewControlRegistration
    {
        /// <summary>登记原生组件，不添加包装组件；父 View 应在构造时保持未激活。</summary>
        public static TComponent BindControl<TComponent>(this GameObject node) where TComponent : Component
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            var component = node.GetComponent<TComponent>();
            var view = node.GetComponentInParent<View>(true);
            if (component == null || view == null) throw new InvalidOperationException("需要原生组件和所属 View。");
            view.RegisterControl(node.name, component, typeof(TComponent));
            return component;
        }
    }
}
