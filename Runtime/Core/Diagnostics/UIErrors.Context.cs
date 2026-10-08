using System;
using System.Threading;

namespace MUI
{
    public static partial class UIErrors
    {
        private static readonly AsyncLocal<UIErrorContext> context = new AsyncLocal<UIErrorContext>();

        /// <summary>当前异步调用链的诊断上下文；仅含值与有界字符串，读取不产生报告。</summary>
        public static UIErrorContext CurrentContext => context.Value;

        /// <summary>为同步步骤及其异步延续提供上下文；省略字段继承外层值，释放只恢复当前调用链。</summary>
        public static IDisposable BeginContext(UIErrorContext value) => new ErrorContextScope(value);

        internal static IDisposable BeginPhase(string phase) =>
            BeginContext(new UIErrorContext(Guid.Empty, 0, null, null, phase));

        /// <summary>固定通知所属页面；只有同一页面的调用链才可贡献当前操作名。</summary>
        internal static IDisposable BeginOwnedPhase(UIErrorContext owner, string operation, string phase)
        {
            var current = CurrentContext;
            var sameOwner = owner.HostId != Guid.Empty && owner.HostId == current.HostId &&
                owner.ViewId == current.ViewId;
            return BeginContext(new UIErrorContext(owner.HostId, owner.ViewId, owner.RouteKey,
                sameOwner ? current.Operation ?? operation : operation, phase));
        }

        internal static void AttachPhase(Exception error, string phase) =>
            AttachContext(error, new UIErrorContext(Guid.Empty, 0, null, null, phase).WithFallback(CurrentContext));

        /// <summary>查询异常的原始位置；聚合的单个原因沿用其上下文，多原因应逐项查询。</summary>
        public static UIErrorContext GetDiagnosticContext(Exception error)
        {
            while (error is AggregateException aggregate && aggregate.InnerExceptions.Count == 1)
            {
                error = aggregate.InnerExceptions[0];
            }

            if (error == null || !records.TryGetValue(error, out var record))
            {
                return default;
            }

            lock (record)
            {
                return record.Context;
            }
        }

        /// <summary>附加发生位置但不报告；传播只补充缺失字段，不覆盖原始阶段或重新生成诊断标识。</summary>
        public static void AttachContext(Exception error, UIErrorContext value)
        {
            if (error == null || value.IsEmpty)
            {
                return;
            }

            if (error is AggregateException aggregate && aggregate.InnerExceptions.Count != 0)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    AttachContext(inner, value);
                }

                return;
            }

            var record = records.GetValue(error, CreateRecord);
            lock (record)
            {
                record.Context = record.Context.WithFallback(value);
            }
        }

        private sealed class ErrorContextScope : IDisposable
        {
            private readonly UIErrorContext previous;
            private bool disposed;

            internal ErrorContextScope(UIErrorContext value)
            {
                previous = context.Value;
                context.Value = value.WithFallback(previous);
            }

            public void Dispose()
            {
                if (!disposed)
                {
                    disposed = true;
                    context.Value = previous;
                }
            }
        }
    }
}
