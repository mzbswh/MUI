using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.Navigation
{
    /// <summary>
    /// 页面实例内容的所有权单元：持有 View、模型、Presenter 及实例资源。
    /// 不持有导航 Handle、业务结果或激活令牌；结束一次激活不等于销毁内容。
    /// </summary>
    internal abstract class ViewContent
    {
        protected ViewContent(object cacheGeneration, object bindingGeneration, object providerVersion)
        {
            CacheGeneration = cacheGeneration;
            BindingGeneration = bindingGeneration;
            ProviderVersion = providerVersion;
        }

        internal object ProviderVersion
        {
            get;
        }

        internal object CacheGeneration
        {
            get;
        }

        internal object BindingGeneration
        {
            get;
        }

        internal abstract LifetimeMode Mode
        {
            get;
        }

        internal abstract bool CanReleaseSynchronously
        {
            get;
        }

        internal abstract void ReleaseCached(List<Exception> errors, Func<IViewResourceReleaseTraceScope> beginResourceRelease = null);

        internal abstract ValueTask ReleaseCachedAsync(List<Exception> errors, Func<IViewResourceReleaseTraceScope> beginResourceRelease = null);
    }

    internal sealed partial class ViewContent<TViewModel, TArgs, TResult> : ViewContent where TViewModel : ViewModel
    {
        private readonly Lifetime instance;
        private IViewLease lease;
        private bool creationStarted;

        internal ViewContent(object cacheGeneration, object bindingGeneration, object providerVersion,
                    LifetimeMode mode = LifetimeMode.AsyncAllowed)
                    : base(cacheGeneration, bindingGeneration, providerVersion)
        {
            instance = new Lifetime(mode);
        }

        internal override LifetimeMode Mode => instance.Mode;

        internal IView View
        {
            get; private set;
        }

        internal ViewPresenterLifecycle<TViewModel, TArgs, TResult> Lifecycle
        {
            get; private set;
        }

        internal TViewModel Model => Lifecycle == null ? null : Lifecycle.Model;

        internal Presenter<TViewModel, TArgs, TResult> Presenter => Lifecycle == null ? null : Lifecycle.Presenter;

        internal void Create(Route<TViewModel, TArgs, TResult> route, TViewModel assignedModel,
                    Action<Action> invoke, Func<Func<ValueTask>, ValueTask> invokeAsync, Action requireCurrent)
        {
            if (creationStarted || instance.IsEnded)
            {
                throw new InvalidOperationException("View content has already been created or released.");
            }

            if (Mode == LifetimeMode.Synchronous)
            {
                if (!route.SupportsSynchronousLifecycle)
                {
                    throw new InvalidOperationException("Route has not declared a complete synchronous lifecycle.");
                }

                if (assignedModel == null && typeof(IAsyncDisposable).IsAssignableFrom(typeof(TViewModel)) &&
                    !typeof(IDisposable).IsAssignableFrom(typeof(TViewModel)))
                {
                    throw new InvalidOperationException("Owned route models must support synchronous disposal.");
                }
            }

            creationStarted = true;
            Lifecycle = ViewPresenterLifecycle<TViewModel, TArgs, TResult>.Create(
                assignedModel, route.ModelFactory, route.PresenterFactory, instance,
                invoke, invokeAsync, requireCurrent);
            if (Mode == LifetimeMode.Synchronous)
            {
                if (!Lifecycle.CanReleaseModelSynchronously)
                {
                    throw new InvalidOperationException("Route model factory violated its synchronous release contract.");
                }

                if (Presenter is IAsyncOpenPresenter<TArgs> || Presenter is IAsyncClosePresenter ||
                    (Presenter is ICloseGuard && !(Presenter is ISynchronousCloseGuard)))
                {
                    throw new InvalidOperationException("Synchronous route content cannot require asynchronous presenter hooks.");
                }
            }
        }

        internal void Adopt(IViewLease acquired)
        {
            if (Mode == LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("Synchronous content requires an independent synchronous view lease.");
            }

            if (lease != null || synchronousLease != null || instance.IsEnded)
            {
                throw new InvalidOperationException("View content cannot acquire another view lease.");
            }

            // 先接管凭证，再读取与校验 View；自定义凭证读取失败也走同一实例释放路径。
            lease = acquired ?? throw new InvalidOperationException("View provider returned a null lease.");
            View = lease.View;
            if (View == null || !View.IsAlive)
            {
                throw new InvalidOperationException("View provider returned an invalid View.");
            }
        }

        internal override ValueTask ReleaseCachedAsync(List<Exception> errors,
            Func<IViewResourceReleaseTraceScope> beginResourceRelease = null) => ReleaseAsync(errors, beginResourceRelease);

        /// <summary>调用方先结束激活；最终销毁继续执行全部资源收尾，错误写入同一集合。</summary>
        internal ValueTask ReleaseAsync(List<Exception> errors,
            Func<IViewResourceReleaseTraceScope> beginResourceRelease = null, params Action[] detachHost)
        {
            if (Mode == LifetimeMode.Synchronous)
            {
                Release(errors, beginResourceRelease, detachHost);
                return default;
            }

            return ReleaseCoreAsync(errors, beginResourceRelease, detachHost);
        }

        private async ValueTask ReleaseCoreAsync(List<Exception> errors,
            Func<IViewResourceReleaseTraceScope> beginResourceRelease, Action[] detachHost)
        {
            if (Lifecycle != null)
            {
                Lifecycle.Destroy(errors);
            }

            await ViewInstanceCleanup.RunAsync(instance, null, errors, detachHost);
            if (lease != null)
            {
                using (var phase = beginResourceRelease == null ? null : beginResourceRelease())
                {
                    var before = errors.Count;
                    await ViewInstanceCleanup.ReleaseViewResourceAsync(lease, errors);
                    FinishResourceReleaseTrace(phase, errors, before);
                }
            }
            lease = null;
            View = null;
            Lifecycle = null;
        }

        private static void FinishResourceReleaseTrace(IViewResourceReleaseTraceScope phase, List<Exception> errors, int before)
        {
            if (phase == null)
            {
                return;
            }

            if (errors.Count == before)
            {
                phase.Complete();
            }
            else
            {
                phase.Fail(errors[before]);
            }
        }
    }
}
