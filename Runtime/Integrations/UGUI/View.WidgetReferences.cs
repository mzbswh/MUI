using System;
using System.Collections.Generic;
using UnityEngine;

namespace MUI.UGUI
{
    [Serializable]
    public sealed class ChildWidgetBinding
    {
        public string Id;
        public string Name;
        public string Source;
        public View Target;
        public bool CreateVM = true;
    }

    public sealed partial class View
    {
        [SerializeField, HideInInspector] private List<ChildWidgetBinding> childWidgets = new List<ChildWidgetBinding>();
        private readonly List<WidgetReference> widgetReferences = new List<WidgetReference>();
        private readonly List<IWidgetElement> widgetParticipants = new List<IWidgetElement>();

        public IReadOnlyList<ChildWidgetBinding> ChildWidgets => childWidgets;

        private static bool WidgetParticipantExists(IWidgetElement child) =>
            child != null && (!(child is UnityEngine.Object unityObject) || unityObject != null);

        /// <summary>代码构建层级时在 Initialize / Awake 前配置，之后结构固定到最终释放。</summary>
        public void ConfigureChildWidgets(IEnumerable<ChildWidgetBinding> bindings)
        {
            RequireAlive();
            if (index != null || initializing)
                throw new InvalidOperationException("Configure child Widgets before View initialization.");
            if (bindings == null)
                throw new ArgumentNullException(nameof(bindings));
            var pending = new List<ChildWidgetBinding>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            var targets = new HashSet<View>();
            foreach (var binding in bindings)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.Name) || !OwnsChildWidget(binding.Target) || !names.Add(binding.Name) || !targets.Add(binding.Target))
                    throw new ArgumentException("Invalid or duplicate child Widget binding.", nameof(bindings));
                pending.Add(new ChildWidgetBinding { Id = binding.Id, Name = binding.Name, Source = binding.Source, Target = binding.Target, CreateVM = binding.CreateVM });
            }
            childWidgets = pending;
        }

        /// <summary>只允许登记本 View 边界内的子 View，不穿过其他 View 或独立内容容器。</summary>
        public bool OwnsChildWidget(View child)
        {
            if (child == null || child == this || !child.transform.IsChildOf(transform))
                return false;
            for (var node = child.transform.parent; node != transform; node = node.parent)
            {
                if (node == null || node.GetComponent<View>() != null)
                    return false;
                foreach (var component in node.GetComponents<MonoBehaviour>())
                    if (component is IElementBoundary)
                        return false;
            }
            foreach (var component in GetComponents<MonoBehaviour>())
                if (component is IElementBoundary)
                    return false;
            return true;
        }

        private void CollectChildWidgets(ElementIndex candidate)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var targets = new HashSet<View>();
            foreach (var binding in childWidgets)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.Name) || !OwnsChildWidget(binding.Target) ||
                    !names.Add(binding.Name) || !targets.Add(binding.Target))
                    throw new InvalidOperationException("子 Widget 需要唯一名称和当前 View 边界内的子 View 引用。");
                var reference = new WidgetReference(binding.Name, binding.Target, () => IsAlive,
                    (scope, provider, vm, args, token) =>
                    {
                        if (!MUI.Widgets.WidgetRegistry.IsRegistered(vm.GetType()))
                            throw new InvalidOperationException("请先调用 Widget 的生成 BindingFactory.Register() 或模块 Initialize()：" + vm.GetType().FullName);
                        return MUI.Widgets.WidgetRegistry.PrepareAsync(scope, provider,
                            new MUI.Resources.ViewResource("nested:" + binding.Target.GetInstanceID()), vm, args, token);
                    });
                GetCleanupScope().OwnDisposable(reference);
                widgetReferences.Add(reference);
                widgetParticipants.Add(reference);
                candidate.Add(reference);
            }
        }
    }
}
