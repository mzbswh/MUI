using System;
using System.Runtime.CompilerServices;

namespace MUI
{
    internal static class ViewTickOwnership
    {
        private static readonly ConditionalWeakTable<object, Claim> claims = new ConditionalWeakTable<object, Claim>();

        internal static IDisposable Acquire(object target)
        {
            lock (claims)
            {
                if (claims.TryGetValue(target, out _))
                {
                    throw new InvalidOperationException("This tick target already belongs to a driver.");
                }

                var claim = new Claim(target);
                claims.Add(target, claim);
                return claim;
            }
        }

        private sealed class Claim : IDisposable
        {
            private object target;

            public Claim(object target)
            {
                this.target = target;
            }

            public void Dispose()
            {
                lock (claims)
                {
                    if (target == null)
                    {
                        return;
                    }

                    claims.Remove(target);
                    target = null;
                }
            }
        }
    }
}
