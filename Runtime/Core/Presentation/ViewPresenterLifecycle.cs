using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>
    /// 页面与子视图共用的 Presenter 和绑定生命周期执行器。
    /// 宿主管理状态、资源凭证和回调重入保护，并保证准备、收尾与销毁不会并发执行。
    /// </summary>
    internal sealed partial class ViewPresenterLifecycle<TViewModel, TArgs, TResult>
        where TViewModel : ViewModel
    {
        private readonly ViewModelOwnership<TViewModel> modelOwnership;
        private readonly Presenter<TViewModel, TArgs, TResult> presenter;
        private readonly LifetimeScope instance;
        private readonly Action<Action> invoke;
        private readonly Func<Func<ValueTask>, ValueTask> invokeAsync;
        private bool created;
        private bool opened;
        private bool destroyed;

        private ViewPresenterLifecycle(
                    ViewModelOwnership<TViewModel> modelOwnership,
                    Presenter<TViewModel, TArgs, TResult> presenter,
                    LifetimeScope instance,
                    Action<Action> invoke,
                    Func<Func<ValueTask>, ValueTask> invokeAsync)
        {
            this.modelOwnership = modelOwnership;
            this.presenter = presenter;
            this.instance = instance;
            this.invoke = invoke;
            this.invokeAsync = invokeAsync;
        }

        private TViewModel model => modelOwnership.Model;

        internal TViewModel Model => model;

        internal Presenter<TViewModel, TArgs, TResult> Presenter => presenter;

        internal bool HasTick => presenter is IViewTick || presenter is ILowFrequencyViewTick;

        internal float TickInterval
        {
            get; private set;
        }

        internal int MaxTickCatchUp
        {
            get; private set;
        } = 1;

        internal BindingContext Binding
        {
            get; private set;
        }

        /// <summary>
        /// 共用模型与 Presenter 创建协议：外部模型只借用，工厂模型先归实例所有，再检查取消。
        /// 所有权登记后的后续工厂失败由宿主的实例清理负责，不在此处提前销毁仍被回调持有的对象。
        /// </summary>
        internal static ViewPresenterLifecycle<TViewModel, TArgs, TResult> Create(
            TViewModel assignedModel,
            Func<TViewModel> modelFactory,
            Func<TViewModel, Presenter<TViewModel, TArgs, TResult>> presenterFactory,
            LifetimeScope instance,
            Action<Action> invoke,
            Func<Func<ValueTask>, ValueTask> invokeAsync,
            Action requireCurrent)
        {
            requireCurrent();
            // 实例只登记一个所有权对象，模型不直接散落在实例资源登记表中。
            // 准备失败时即使执行器尚未返回，宿主也能通过实例清理接管的模型。
            var ownership = instance.Own(new ViewModelOwnership<TViewModel>());
            invoke(() => ownership.Initialize(assignedModel, modelFactory));
            var model = ownership.Model;

            requireCurrent();
            Presenter<TViewModel, TArgs, TResult> presenter = null;
            invoke(() => presenter = presenterFactory(model));
            if (presenter == null)
            {
                throw new InvalidOperationException("Presenter factory returned null.");
            }

            requireCurrent();
            var lifecycle = new ViewPresenterLifecycle<TViewModel, TArgs, TResult>(ownership, presenter, instance, invoke, invokeAsync);
            if (lifecycle.HasTick)
            {
                var claim = ViewTickOwnership.Acquire(presenter);
                try
                {
                    instance.OwnDisposable(claim);
                }
                catch
                {
                    claim.Dispose();
                    throw;
                }

                invoke(() =>
                {
                    lifecycle.TickInterval = ViewTickTiming.Interval(presenter);
                    lifecycle.MaxTickCatchUp = ViewTickTiming.CatchUpLimit(presenter);
                });
                requireCurrent();
            }

            return lifecycle;
        }

        /// <summary>每个实例只创建一次 Presenter；重新激活时重新建立绑定并调用打开回调。</summary>
        internal void Prepare(
            IView view,
            LifetimeScope activation,
            TArgs args,
            ICommandTarget target,
            Func<IView, TViewModel, BindingContext> bindingFactory,
            Action requireCurrent,
            Func<ViewPreparationStep, IViewPreparationTraceScope> trace = null)
        {
            if (destroyed || opened || Binding != null)
            {
                throw new InvalidOperationException("Previous view activation must finish before preparation.");
            }

            ViewPreparation.Begin(view, activation, invoke, requireCurrent);
            if (!created)
            {
                // 创建回调失败也需要销毁回调清理其已经建立的实例资源。
                created = true;
                using (var phase = trace?.Invoke(ViewPreparationStep.PresenterCreate))
                {
                    try
                    {
                        invoke(() => presenter.Create(model, instance));
                        requireCurrent();
                        phase?.Complete();
                    }
                    catch (Exception error)
                    {
                        phase?.Fail(error);
                        throw;
                    }
                }
            }

            // 验证工厂结果后才接管；无效的共享上下文不能被本次失败清理解绑。
            using (var phase = trace?.Invoke(ViewPreparationStep.Binding))
            {
                try
                {
                    invoke(() =>
                    {
                        var candidate = bindingFactory(view, model);
                        ViewPreparation.ValidateBinding(candidate, view, model);
                        Binding = candidate;
                    });
                    requireCurrent();
                    invoke(() => Binding.SetCommandTarget(target));
                    requireCurrent();
                    invoke(Binding.Bind);
                    requireCurrent();
                    phase?.Complete();
                }
                catch (Exception error)
                {
                    phase?.Fail(error);
                    throw;
                }
            }
            using (var phase = trace?.Invoke(ViewPreparationStep.PresenterOpen))
            {
                try
                {
                    invoke(() => presenter.Open(new ActivationContext<TArgs, TResult>(args, activation, target, view as IInputView)));
                    opened = true;
                    requireCurrent();
                    phase?.Complete();
                }
                catch (Exception error)
                {
                    phase?.Fail(error);
                    throw;
                }
            }
        }

        internal ValueTask PrepareAsync(IView view, TArgs args, Action requireCurrent, CancellationToken token,
            Func<ViewPreparationStep, IViewPreparationTraceScope> trace = null)
        {
            Func<CancellationToken, ValueTask> openAsync = null;
            if (presenter is IAsyncOpenPresenter<TArgs> asynchronous)
            {
                if (trace == null)
                {
                    openAsync = cancellation => invokeAsync(() => asynchronous.OnOpenAsync(args, cancellation));
                }
                else
                {
                    openAsync = async cancellation =>
                    {
                        using (var phase = trace(ViewPreparationStep.PresenterOpenAsync))
                        {
                            try
                            {
                                await invokeAsync(() => asynchronous.OnOpenAsync(args, cancellation));
                                phase?.Complete();
                            }
                            catch (Exception error)
                            {
                                phase?.Fail(error);
                                throw;
                            }
                        }
                    };
                }
            }

            return ViewPreparation.CompleteAsync(view, openAsync, requireCurrent, token);
        }

        /// <summary>只结束本次激活，保留 Presenter 和实例资源供显式重新激活使用。</summary>
        internal async ValueTask<Exception> EndActivationAsync(
            LifetimeScope activation,
            bool committed,
            CancellationToken cleanupToken,
            List<Exception> errors)
        {
            // 关闭、停用与换绑共用排空边界，避免 OnClose 观察到一半切换的模型。
            if (Rebinding != null)
            {
                var rebound = await Rebinding;
                if ((rebound.ViewFaulted || rebound.Cleanup == RebindCleanup.Failed) && rebound.Error != null && !errors.Contains(rebound.Error))
                {
                    errors.Add(rebound.Error);
                }
            }

            if (rebindCleanupFailure != null && !errors.Contains(rebindCleanupFailure))
            {
                errors.Add(rebindCleanupFailure);
            }

            Func<ValueTask> closeAsync = null;
            if (committed && opened && presenter is IAsyncClosePresenter asynchronous)
            {
                closeAsync = () => invokeAsync(() => asynchronous.OnCloseAsync(cleanupToken));
            }

            Action close = null;
            if (opened)
            {
                close = () =>
                {
                    opened = false;
                    invoke(presenter.Close);
                };
            }

            var failure = await ViewActivationCleanup.RunAsync(closeAsync, close, Binding, activation, errors);
            Binding = null;
            return failure;
        }

        /// <summary>结束实例时最多调用一次销毁回调；回调失败不阻断宿主后续资源释放。</summary>
        internal void Destroy(List<Exception> errors)
        {
            if (destroyed)
            {
                return;
            }

            destroyed = true;
            if (!created)
            {
                return;
            }

            created = false;
            try
            {
                invoke(presenter.Destroy);
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
        }
    }
}
