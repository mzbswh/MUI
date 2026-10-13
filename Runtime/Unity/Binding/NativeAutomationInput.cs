using System;
using System.Collections.Generic;
using UnityEngine;

namespace MUI.Unity
{
    /// <summary>可选的原生自动化输入注册，不向控件添加组件，也不使用运行时反射。</summary>
    public static class NativeAutomationInput<T>
    {
        private static readonly Dictionary<Type, Func<Component, T, bool>> setters = new Dictionary<Type, Func<Component, T, bool>>();
        public static void Register<TComponent>(Func<TComponent, T, bool> setter) where TComponent : Component
        {
            if (setter == null) throw new ArgumentNullException(nameof(setter));
            setters[typeof(TComponent)] = (component, value) => setter((TComponent)component, value);
        }
        internal static bool Set(Component component, T value)
        {
            for (var type = component.GetType(); type != null; type = type.BaseType)
                if (setters.TryGetValue(type, out var setter)) return setter(component, value);
            throw new InvalidOperationException("组件未注册自动化输入：" + component.GetType().FullName);
        }
    }
}
