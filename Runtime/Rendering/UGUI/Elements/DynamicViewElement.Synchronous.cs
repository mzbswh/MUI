using System;
using System.Threading.Tasks;
using MUI.ChildViews;
using MUI.Resources;

namespace MUI.UGUI
{
    public sealed partial class DynamicViewElement
    {
        private bool synchronousRefreshing;
        private Exception synchronousPreparationFailure;
        private bool synchronousChange = true;
        private ChildViewChangeResult synchronousResult = new ChildViewChangeResult(ChildViewChangeStatus.Empty);
        private TaskCompletionSource<ChildViewChangeResult> synchronousResultCompletion;
        private TaskCompletionSource<bool> synchronousPreparationCompletion;

        private bool HasPreparationFailure => synchronousChange
            ? synchronousPreparationFailure != null
            : preparation != null && (preparation.IsFaulted || preparation.IsCanceled);

        /// <summary>直接读取最近一次同步替换结果；进行中的替换不能被当作已完成。</summary>
        public ChildViewChangeResult SynchronousResult
        {
            get
            {
                if (!synchronousChange || synchronousRefreshing)
                {
                    throw new InvalidOperationException("A completed synchronous dynamic change is required.");
                }

                return synchronousResult;
            }
        }

        private Task<ChildViewChangeResult> GetPendingChange()
        {
            if (!synchronousChange)
            {
                return pending ?? (pending = Task.FromResult(synchronousResult));
            }

            if (synchronousRefreshing && synchronousResultCompletion == null)
            {
                synchronousResultCompletion = new TaskCompletionSource<ChildViewChangeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                pending = synchronousResultCompletion.Task;
            }

            return pending ?? (pending = Task.FromResult(synchronousResult));
        }

        private Task GetPreparation()
        {
            if (!synchronousChange)
            {
                return preparation ?? Task.CompletedTask;
            }

            if (synchronousRefreshing && synchronousPreparationCompletion == null)
            {
                synchronousPreparationCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                preparation = synchronousPreparationCompletion.Task;
            }

            if (preparation == null)
            {
                preparation = synchronousPreparationFailure == null
                    ? Task.CompletedTask
                    : Task.FromException(synchronousPreparationFailure);
                if (synchronousPreparationFailure != null)
                {
                    _ = preparation.Exception;
                }
            }

            return preparation ?? Task.CompletedTask;
        }

        bool IChildViewElement.TryCompleteSynchronousPreparation()
        {
            if (synchronousRefreshing)
            {
                return false;
            }

            if (synchronousPreparationFailure != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(synchronousPreparationFailure).Throw();
            }

            return true;
        }

        /// <summary>配置借用的同步提供方；提供方不必实现任何异步接口。</summary>
        public void ConfigureSynchronous(ISynchronousViewProvider viewProvider)
        {
            RequireAlive();
            if (viewProvider == null)
            {
                throw new ArgumentNullException(nameof(viewProvider));
            }

            if (slot != null && slot.Mode != LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("ConfigureSynchronous requires a synchronous parent activation.");
            }

            if (ReferenceEquals(provider, viewProvider) && !HasPreparationFailure)
            {
                return;
            }

            RequireSynchronousIdle();
            provider = viewProvider;
            Refresh();
        }

        private void RequireSynchronousIdle()
        {
            if (synchronousRefreshing)
            {
                throw new InvalidOperationException("Cannot reenter a synchronous dynamic change.");
            }

            if (slot != null && slot.Mode == LifetimeMode.Synchronous)
            {
                slot.RequireSynchronousIdle();
            }
        }

        /// <summary>直接执行准备、提交和旧内容释放；仅显式兼容查询才创建任务信号。</summary>
        private void RefreshSynchronous()
        {
            var resultPublished = false;
            synchronousChange = true;
            pending = null;
            preparation = null;
            synchronousResultCompletion = null;
            synchronousPreparationCompletion = null;
            synchronousPreparationFailure = null;
            synchronousRefreshing = true;
            try
            {
                ChildViewChangeResult result;
                if (string.IsNullOrEmpty(source))
                {
                    result = slot.Clear();
                }
                else
                {
                    if (!(provider is ISynchronousViewProvider synchronous))
                    {
                        throw new InvalidOperationException("Configure a synchronous ViewProvider before binding a dynamic Source.");
                    }

                    var assigned = model ?? new UnboundModel();
                    var reusable = FindReusableContent(slot);
                    if (reusable != null)
                    {
                        result = slot.Rebind(reusable, assigned);
                    }
                    else
                    {
                        var mounted = ContentViewProvider.CreateSynchronous(synchronous, transform, model != null || showUnbound);
                        var template = new ChildViewTemplate<ViewModel, Unit>(new ViewResource(source),
                            () => throw new InvalidOperationException("Dynamic content uses an assigned model."),
                            (view, value) => value is UnboundModel ? new UnboundBinding(view, value) : BindingRegistry.Create(view, value),
                            supportsSynchronousLifecycle: true);
                        result = slot.Replace(scope => scope.PrepareSynchronous(template, mounted, Unit.Value, assigned),
                            _ => displayedProvider = synchronous);
                    }
                }

                synchronousResult = result;
                resultPublished = true;
                // 提交成功仍可能携带旧资源清理错误，必须向设置器和父准备阶段报告。
                if (result.Error != null)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(result.Error).Throw();
                }

                if (result.Status != ChildViewChangeStatus.Ready && result.Status != ChildViewChangeStatus.Empty)
                {
                    throw new InvalidOperationException("Synchronous dynamic change did not complete: " + result.Status);
                }
            }
            catch (Exception error)
            {
                synchronousPreparationFailure = error;
                if (!resultPublished)
                {
                    synchronousResult = new ChildViewChangeResult(ChildViewChangeStatus.Failed, error);
                }
                throw;
            }
            finally
            {
                synchronousRefreshing = false;
                if (synchronousResultCompletion != null)
                {
                    synchronousResultCompletion.TrySetResult(synchronousResult);
                }

                if (synchronousPreparationCompletion != null)
                {
                    if (synchronousPreparationFailure == null)
                    {
                        synchronousPreparationCompletion.TrySetResult(true);
                    }
                    else
                    {
                        synchronousPreparationCompletion.TrySetException(synchronousPreparationFailure);
                        _ = synchronousPreparationCompletion.Task.Exception;
                    }
                }
            }
        }
    }
}
