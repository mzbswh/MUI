using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MUI
{
    internal sealed partial class BindingSession
    {
        private readonly Dictionary<(IElement element, string property), string> targetWriters = new Dictionary<(IElement, string), string>(TargetIdentity.Instance);
        private readonly Dictionary<string, string> sourceWriters = new Dictionary<string, string>(StringComparer.Ordinal);

        public void RegisterPropertyWriter(IElement element, string source, string property, BindingMode mode)
        {
            if (!IsActive)
            {
                throw new InvalidOperationException("Binding session has ended.");
            }

            if (element is IBindingPropertyPolicy policy)
            {
                property = policy.GetBindingWriteTarget(property, mode);
                if (string.IsNullOrEmpty(property))
                {
                    throw new InvalidOperationException("控件绑定策略返回了空写入目标。");
                }
            }

            var forward = mode != BindingMode.OneWayToSource;
            var reverse = mode == BindingMode.TwoWay || mode == BindingMode.OneWayToSource;
            var target = (element, property);
            var name = element.Name + "." + property;
            if (forward && targetWriters.TryGetValue(target, out var previousSource))
            {
                throw new InvalidOperationException($"Multiple writers for {name}: {previousSource}, {source}.");
            }

            if (reverse && sourceWriters.TryGetValue(source, out var previousTarget))
            {
                throw new InvalidOperationException($"Multiple reverse writers for {source}: {previousTarget}, {name}.");
            }

            if (forward)
            {
                targetWriters.Add(target, source);
            }

            if (reverse)
            {
                sourceWriters.Add(source, name);
            }
        }

        private sealed class TargetIdentity : IEqualityComparer<(IElement element, string property)>
        {
            public static readonly TargetIdentity Instance = new TargetIdentity();

            public bool Equals((IElement element, string property) left, (IElement element, string property) right) => ReferenceEquals(left.element, right.element) && StringComparer.Ordinal.Equals(left.property, right.property);

            public int GetHashCode((IElement element, string property) value) => unchecked(RuntimeHelpers.GetHashCode(value.element) * 397 ^ StringComparer.Ordinal.GetHashCode(value.property));
        }
    }
}
