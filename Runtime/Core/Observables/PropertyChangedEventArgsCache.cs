using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace MUI
{
    /// <summary>复用不可变通知参数；动态名称超出容量或长度限制时直接分配，不永久保留。</summary>
    internal static class PropertyChangedEventArgsCache
    {
        private const int capacity = 256;
        private const int maximumNameLength = 128;
        private static readonly object gate = new object();
        private static readonly Dictionary<string, PropertyChangedEventArgs> cached =
            new Dictionary<string, PropertyChangedEventArgs>(StringComparer.Ordinal);
        private static readonly PropertyChangedEventArgs nullName = new PropertyChangedEventArgs(null);
        private static readonly PropertyChangedEventArgs emptyName = new PropertyChangedEventArgs(string.Empty);

        internal static PropertyChangedEventArgs Get(string name)
        {
            if (name == null)
            {
                return nullName;
            }

            if (name.Length == 0)
            {
                return emptyName;
            }

            if (name.Length > maximumNameLength)
            {
                return new PropertyChangedEventArgs(name);
            }

            lock (gate)
            {
                if (cached.TryGetValue(name, out var args))
                {
                    return args;
                }

                args = new PropertyChangedEventArgs(name);
                if (cached.Count < capacity)
                {
                    cached.Add(name, args);
                }

                return args;
            }
        }
    }
}
