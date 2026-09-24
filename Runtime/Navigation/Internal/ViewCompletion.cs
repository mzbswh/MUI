using System.Threading.Tasks;

namespace MUI.Navigation
{
    /// <summary>一次性结果存储；同步查询不创建任务，异步等待仅在需要时建立完成信号。</summary>
    internal sealed partial class ViewCompletion<T>
    {
        private readonly object gate = new object();
        private bool completed;
        private T value;
        private TaskCompletionSource<T> completion;

        internal bool IsCompleted
        {
            get
            {
                lock (gate)
                {
                    return completed;
                }
            }
        }

        internal Task<T> Task
        {
            get
            {
                lock (gate)
                {
                    if (completion == null)
                    {
                        completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
                        if (completed)
                        {
                            completion.TrySetResult(value);
                        }
                    }

                    return completion.Task;
                }
            }
        }

        internal bool TryGetResult(out T result)
        {
            lock (gate)
            {
                result = value;
                return completed;
            }
        }

        internal bool TrySetResult(T result)
        {
            lock (gate)
            {
                if (completed)
                {
                    return false;
                }

                value = result;
                completed = true;
                completion?.TrySetResult(result);
                return true;
            }
        }
    }
}
