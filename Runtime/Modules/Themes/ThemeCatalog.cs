using System;
using System.Collections.Generic;

namespace MUI.Themes
{
    /// <summary>按名称复制的只读值目录；缺键时沿回退目录查找，类型错误不继续回退。</summary>
    public sealed class ThemeCatalog
    {
        private readonly Dictionary<string, ThemeValue> values = new Dictionary<string, ThemeValue>(StringComparer.Ordinal);
        private readonly ThemeCatalog fallback;

        /// <summary>建立目录；重复名称和空项属于配置错误。</summary>
        public ThemeCatalog(string name, IEnumerable<ThemeValue> values, ThemeCatalog fallback = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Theme name is required.", nameof(name));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            Name = name;
            this.fallback = fallback;
            foreach (var value in values)
            {
                if (value == null)
                {
                    throw new ArgumentException("Null theme entry.", nameof(values));
                }

                this.values.Add(value.Name, value);
            }
        }

        /// <summary>提供方用于查找此目录的稳定名称。</summary>
        public string Name
        {
            get;
        }

        /// <summary>按语义键与精确类型读取，不隐式转换数值或字符串。</summary>
        public T Get<T>(ThemeToken<T> token)
        {
            var validated = new ThemeToken<T>(token.Name);
            for (var catalog = this; catalog != null; catalog = catalog.fallback)
            {
                if (catalog.values.TryGetValue(validated.Name, out var entry))
                {
                    if (entry.Type != typeof(T))
                    {
                        throw new InvalidOperationException($"Theme token '{token.Name}' is {entry.Type.Name}, expected {typeof(T).Name}.");
                    }

                    return (T)entry.Value;
                }
            }

            throw new KeyNotFoundException($"Theme token '{token.Name}' is missing in '{Name}' and its fallbacks.");
        }
    }
}
