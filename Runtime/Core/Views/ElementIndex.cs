using System;
using System.Collections.Generic;

namespace MUI
{
    /// <summary>按名称与可赋值类型查找，歧义属于契约错误。</summary>
    public sealed class ElementIndex
    {
        private readonly Dictionary<string, List<IElement>> entries = new Dictionary<string, List<IElement>>(StringComparer.Ordinal);

        public void Add(IElement element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            if (!element.IsAlive)
            {
                throw new InvalidOperationException("Cannot index a destroyed element.");
            }

            if (string.IsNullOrWhiteSpace(element.Name))
            {
                throw new InvalidOperationException("Element name is empty.");
            }

            if (!entries.TryGetValue(element.Name, out var candidates))
            {
                candidates = new List<IElement>();
                entries.Add(element.Name, candidates);
            }

            foreach (var candidate in candidates)
            {
                if (candidate is IComponentElement existing && element is IComponentElement incoming
                    ? existing.ComponentType == incoming.ComponentType
                    : candidate.GetType() == element.GetType())
                {
                    throw new InvalidOperationException($"Duplicate Element '{element.Name}' ({element.GetType().FullName}).");
                }
            }

            candidates.Add(element);
        }

        public TElement Get<TElement>(string name, Type componentType = null)
                    where TElement : class, IElement
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            TElement found = null;
            var exact = componentType != null && entries.TryGetValue(name, out var typedCandidates) &&
                typedCandidates.Exists(candidate => candidate is TElement && candidate is IComponentElement typed && typed.ComponentType == componentType);
            if (entries.TryGetValue(name, out var candidates))
            {
                foreach (var candidate in candidates)
                {
                    if (!(candidate is TElement match))
                    {
                        continue;
                    }

                    if (componentType != null && (!(match is IComponentElement typed) ||
                        (exact ? typed.ComponentType != componentType : !componentType.IsInstanceOfType(typed.ComponentTarget))))
                        continue;
                    if (!match.IsAlive)
                    {
                        throw new InvalidOperationException($"Element '{name}' has been destroyed.");
                    }

                    if (found != null)
                    {
                        throw new InvalidOperationException($"Ambiguous Element '{name}' ({typeof(TElement).FullName}).");
                    }

                    found = match;
                }
            }

            return found ?? throw new InvalidOperationException($"Missing Element '{name}' ({typeof(TElement).FullName}).");
        }
    }
}
