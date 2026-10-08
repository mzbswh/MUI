using System;
using System.Collections.Generic;
using MUI.UGUI;

namespace MUI.Editor
{
    public static partial class ViewContractValidator
    {
        private static readonly Dictionary<Type, ElementValidator> elementValidators = new Dictionary<Type, ElementValidator>();

        /// <summary>每个域注册一次只读编辑器校验器，通常在 InitializeOnLoad 中调用。</summary>
        public static void RegisterElementValidator<TElement>(Action<TElement, View, ICollection<string>> validator)
            where TElement : Element
        {
            if (validator == null)
            {
                throw new ArgumentNullException(nameof(validator));
            }

            var type = typeof(TElement);
            if (elementValidators.TryGetValue(type, out var current))
            {
                if (current.Identity.Equals(validator))
                {
                    return;
                }

                throw new InvalidOperationException($"An Editor structure validator is already registered for {type.FullName}.");
            }

            elementValidators.Add(type, new ElementValidator { Identity = validator, Validate = (element, view, errors) => validator((TElement)element, view, errors) });
        }

        private static List<KeyValuePair<Type, ElementValidator>> SnapshotElementValidators()
        {
            var snapshot = new List<KeyValuePair<Type, ElementValidator>>(elementValidators);
            snapshot.Sort((a, b) => StringComparer.Ordinal.Compare(a.Key.AssemblyQualifiedName, b.Key.AssemblyQualifiedName));
            return snapshot;
        }

        private static void ValidateExtensions(Element element,
                    View view,
                    List<string> errors,
                    List<KeyValuePair<Type, ElementValidator>> snapshot)
        {
            foreach (var pair in snapshot)
            {
                if (!pair.Key.IsInstanceOfType(element))
                {
                    continue;
                }

                try
                {
                    pair.Value.Validate(element, view, errors);
                }
                catch (Exception error)
                {
                    errors.Add($"Element validator {pair.Key.FullName} failed at {Path(element.transform, view.transform)}: {error.Message}");
                }
            }
        }

        private sealed class ElementValidator
        {
            internal Delegate Identity;
            internal Action<Element, View, ICollection<string>> Validate;
        }
    }
}
