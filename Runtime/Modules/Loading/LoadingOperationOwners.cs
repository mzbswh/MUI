using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MUI.Loading
{
    /// <summary>每个生命周期只登记一个加载提示集合，仅持有尚未结束的令牌。</summary>
    internal sealed class LoadingOperationOwners : IDisposable
    {
        private static readonly ConditionalWeakTable<Lifetime, LoadingOperationOwners> owners =
            new ConditionalWeakTable<Lifetime, LoadingOperationOwners>();
        private readonly LinkedList<LoadingOperation> active = new LinkedList<LoadingOperation>();

        internal static void Own(Lifetime lifetime, LoadingOperation operation)
        {
            var group = owners.GetValue(lifetime, Create);
            if (lifetime.IsEnded)
            {
                throw new ObjectDisposedException(nameof(lifetime));
            }

            operation.OwnershipNode = group.active.AddLast(operation);
        }

        private static LoadingOperationOwners Create(Lifetime lifetime)
        {
            return lifetime.OwnDisposable(new LoadingOperationOwners());
        }

        public void Dispose()
        {
            List<Exception> errors = null;
            while (active.Last != null)
            {
                var operation = active.Last.Value;
                // 先移出集合；即使输入阻挡释放失败，也继续清理其余提示。
                active.RemoveLast();
                operation.OwnershipNode = null;
                try
                {
                    operation.Dispose();
                }
                catch (Exception error)
                {
                    if (errors == null)
                    {
                        errors = new List<Exception>();
                    }
                    errors.Add(error);
                }
            }

            if (errors != null)
            {
                throw new AggregateException("加载提示令牌清理失败。", errors);
            }
        }
    }
}
