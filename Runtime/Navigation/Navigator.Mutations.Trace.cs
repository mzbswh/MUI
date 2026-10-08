using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        /// <summary>记录参数事务的真实提交、恢复及清理结果，不记录参数值。</summary>
        public ValueTask<ArgsUpdateOutcome> UpdateArgsAsync<TViewModel, TArgs, TResult>(ViewHandle source,
            Route<TViewModel, TArgs, TResult> route, TArgs args, CancellationToken cancellationToken = default) where TViewModel : ViewModel
        {
            AssertThread();
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }
            var trace = BeginOperationTrace(route.Key, "UpdateArgsAsync", source);
            try
            {
                using (EnterOperationTrace(trace))
                {
                    var operation = UpdateArgsAsyncUntraced(source, route, args, cancellationToken);
                    return trace.Id == 0 ? operation : ObserveTracedOperationAsync(operation, trace, FinishMutationTrace);
                }
            }
            catch (Exception error)
            {
                FinishOperationTrace(trace, source, "抛出异常", error);
                throw;
            }
        }

        private void FinishMutationTrace(NavigationTraceOperation trace, ArgsUpdateOutcome result)
        {
            if (trace.Id == 0 || !traceRecording || !ReferenceEquals(trace.Session, traceSession))
            {
                return;
            }
            FinishOperationTrace(trace, trace.Source, result.Status + "/" + result.Rejection + "/" +
                result.Cleanup + "/视图故障=" + result.ViewFaulted, result.Error);
        }

        /// <summary>记录同一实例换绑结果，不保留旧模型或新模型引用。</summary>
        public ValueTask<RebindOutcome> RebindAsync<TViewModel, TArgs, TResult>(ViewHandle source,
            Route<TViewModel, TArgs, TResult> route, TViewModel viewModel, CancellationToken cancellationToken = default) where TViewModel : ViewModel
        {
            AssertThread();
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }
            if (viewModel == null)
            {
                throw new ArgumentNullException(nameof(viewModel));
            }
            var trace = BeginOperationTrace(route.Key, "RebindAsync", source);
            try
            {
                using (EnterOperationTrace(trace))
                {
                    var operation = RebindAsyncUntraced(source, route, viewModel, cancellationToken);
                    return trace.Id == 0 ? operation : ObserveTracedOperationAsync(operation, trace, FinishMutationTrace);
                }
            }
            catch (Exception error)
            {
                FinishOperationTrace(trace, source, "抛出异常", error);
                throw;
            }
        }

        private void FinishMutationTrace(NavigationTraceOperation trace, RebindOutcome result)
        {
            if (trace.Id == 0 || !traceRecording || !ReferenceEquals(trace.Session, traceSession))
            {
                return;
            }
            FinishOperationTrace(trace, trace.Source, result.Status + "/" + result.Rejection + "/" +
                result.Cleanup + "/视图故障=" + result.ViewFaulted, result.Error);
        }
    }
}
