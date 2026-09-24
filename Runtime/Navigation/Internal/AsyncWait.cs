using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    internal static class AsyncWait
    {
        public static async Task<T> WithCancellation<T>(Task<T> task, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!token.CanBeCanceled || task.IsCompleted)
            {
                return await task;
            }

            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(task, cancelled.Task) != task)
                {
                    throw new OperationCanceledException(token);
                }
            }

            return await task;
        }
    }
}
