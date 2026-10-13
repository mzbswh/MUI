using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    [Serializable]
    public sealed class NativeControlBinding
    {
        public string Name;
        public Component Target;
        public string ComponentType;
        public Type BindingType => string.IsNullOrEmpty(ComponentType) ? (Target == null ? null : Target.GetType()) : Type.GetType(ComponentType, false);
    }

    [Serializable]
    public sealed class AutoBindingProperty
    {
        public string Property;
        public bool Native;
        public string Extension;
        public string Source;
        public BindingMode Mode;
    }

    [Serializable]
    public sealed class AutoBindingSelection
    {
        public string Control;
        public string ComponentType;
        public List<AutoBindingProperty> Properties = new List<AutoBindingProperty>();
    }

    public sealed partial class View
    {
        [SerializeField] private List<NativeControlBinding> nativeBindings = new List<NativeControlBinding>();
        public IReadOnlyList<NativeControlBinding> NativeBindings => nativeBindings;

        [SerializeField, HideInInspector] private string muiAutoBindings = string.Empty;
        [SerializeField, HideInInspector] private List<AutoBindingSelection> autoBindingSelections = new List<AutoBindingSelection>();
        public IReadOnlyList<AutoBindingSelection> AutoBindingSelections => autoBindingSelections;
        public string AutoBindingsMetadata => muiAutoBindings;

        public bool OwnsNativeControl(Component target)
        {
            if (target == null || !target.transform.IsChildOf(transform)) return false;
            for (var node = target.transform; node != null; node = node.parent)
            {
                if (node != transform && node.GetComponent<View>() != null) return false;
                if (node != target.transform)
                    foreach (var component in node.GetComponents<MonoBehaviour>())
                        if (component is IElementBoundary) return false;
                if (node == transform) return true;
            }
            return false;
        }

        private void CollectNativeBindings(ElementIndex candidate)
        {
            var targets = new HashSet<Component>();
            var names = new HashSet<(string, Type)>();
            foreach (var binding in nativeBindings)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.Name) || !OwnsNativeControl(binding.Target) ||
                    binding.BindingType == null || !binding.BindingType.IsInstanceOfType(binding.Target) ||
                    !targets.Add(binding.Target) || !names.Add((binding.Name, binding.BindingType)) || binding.Target.GetComponent<Element>() != null)
                    throw new InvalidOperationException("原生控件绑定需要唯一的名称与组件类型组合、有效的页面内组件引用，不能与 Element 重复绑定。");
                var adapter = NativeControl.Create(binding.Name, binding.Target);
                adapter.ComponentType = binding.BindingType;
                GetCleanupScope().OnDispose(adapter.Dispose);
                candidate.Add(adapter);
            }
        }
    }
}
