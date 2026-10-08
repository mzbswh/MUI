using System;
using System.Threading;

namespace MUI.Resources
{
    // 跨资源类型共享，以便也能检测 A<T> -> B<U> -> A<T> 的循环。
    internal static class ResourceReleaseContext
    {
        private static readonly AsyncLocal<Frame> Current = new AsyncLocal<Frame>();

        public static bool Contains(object owner)
        {
            for (var frame = Current.Value; frame != null; frame = frame.Parent)
            {
                if (frame.Active && ReferenceEquals(frame.Owner, owner))
                {
                    return true;
                }
            }

            return false;
        }

        public static IDisposable Enter(object owner)
        {
            var frame = new Frame
            {
                Owner = owner,
                Parent = Current.Value
            };
            Current.Value = frame;
            return frame;
        }

        private sealed class Frame : IDisposable
        {
            public object Owner;
            public Frame Parent;
            public bool Active = true;

            public void Dispose()
            {
                Active = false;
                Current.Value = Parent;
                Owner = null;
            }
        }
    }
}
