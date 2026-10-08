using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.Navigation
{
    /// <summary>
    /// 视图内容的兼容代际；活动内容与缓存凭证各自承担独立的所有权。
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

        internal abstract ValueTask ReleaseCachedAsync(List<Exception> errors, Func<IViewResourceReleaseTraceScope> beginResourceRelease = null);
    }

    internal sealed partial class ViewContent<TViewModel, TArgs, TResult> : ViewContent where TViewModel : ViewModel
    {
        private readonly LifetimeScope instance;
        private IAcquiredView ownedResource;
        private bool creationStarted;
        private bool instanceReleased;

        internal ViewContent(object cacheGeneration, object bindingGeneration, object providerVersion)
                    : base(cacheGeneration, bindingGeneration, providerVersion)
        {
            instance = new LifetimeScope();
        }

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

        internal bool CanCache => View is ICacheableView &&
            ownedResource is ICacheableViewAcquisition acquisition && acquisition.SupportsCaching;

        internal void Create(Route<TViewModel, TArgs, TResult> route, TViewModel assignedModel,
                    Action<Action> invoke, Func<Func<ValueTask>, ValueTask> invokeAsync, Action requireCurrent)
        {
            if (creationStarted || instance.IsEnded)
            {
                throw new InvalidOperationException("View content has already been created or released.");
            }

            creationStarted = true;
            Lifecycle = ViewPresenterLifecycle<TViewModel, TArgs, TResult>.Create(
                assignedModel, route.ModelFactory, route.PresenterFactory, instance,
                invoke, invokeAsync, requireCurrent);
        }

        internal void Adopt(IAcquiredView acquired)
        {

            if (ownedResource != null || instance.IsEnded)
            {
                throw new InvalidOperationException("View content cannot acquire another view acquisition.");
            }

            // 先接管凭证，再读取与校验 View；自定义凭证读取失败也走同一实例释放路径。
            ownedResource = acquired ?? throw new InvalidOperationException("View provider returned a null resource acquisition.");
            View = ownedResource.View;
            if (View == null || !View.IsAlive)
            {
                throw new InvalidOperationException("View provider returned an invalid View.");
            }
        }

        internal override ValueTask ReleaseCachedAsync(List<Exception> errors,
            Func<IViewResourceReleaseTraceScope> beginResourceRelease = null) => ReleaseAsync(errors, beginResourceRelease);

        /// <summary>销毁业务实例并排空其资源，成功后才把独立 View 凭证移交缓存。</summary>
        internal async ValueTask<CachedViewContent> DetachForCacheAsync(List<Exception> errors, LifetimeScope activation)
        {
            if (!CanCache)
            {
                return null;
            }
            var before = errors.Count;
            await ReleaseInstanceAsync(errors, Array.Empty<Action>());
            if (errors.Count != before || !instance.IsCleanupConfirmed || !activation.IsCleanupConfirmed)
            {
                return null;
            }

            using (UIErrors.BeginPhase("CacheReset"))
            {
                try
                {
                    ((ICacheableView)View).ResetForCache();
                }
                catch (Exception error)
                {
                    UIErrors.AttachPhase(error, "CacheReset");
                    throw;
                }
            }
            if (!View.IsAlive)
            {
                throw new InvalidOperationException("View was destroyed while resetting for cache.");
            }
            var cached = new CachedViewContent(ownedResource, CacheGeneration, BindingGeneration, ProviderVersion);
            ownedResource = null;
            View = null;
            Lifecycle = null;
            return cached;
        }

        /// <summary>调用方先结束激活；最终销毁继续执行全部资源收尾，错误写入同一集合。</summary>
        internal ValueTask ReleaseAsync(List<Exception> errors,
            Func<IViewResourceReleaseTraceScope> beginResourceRelease = null, params Action[] detachHost)
        {
            return ReleaseCoreAsync(errors, beginResourceRelease, null, detachHost);
        }

        internal ValueTask ReleaseWithActivationAsync(List<Exception> errors, LifetimeScope activation,
            Func<IViewResourceReleaseTraceScope> beginResourceRelease)
        {
            return ReleaseCoreAsync(errors, beginResourceRelease, activation, Array.Empty<Action>());
        }

        private async ValueTask ReleaseCoreAsync(List<Exception> errors,
            Func<IViewResourceReleaseTraceScope> beginResourceRelease, LifetimeScope activation, Action[] detachHost)
        {
            await ReleaseInstanceAsync(errors, detachHost);
            if (ownedResource != null)
            {
                using (var phase = beginResourceRelease == null ? null : beginResourceRelease())
                {
                    var before = errors.Count;
                    var instanceScope = instance;
                    await ViewInstanceCleanup.ReleaseViewResourceAsync(ownedResource, errors,
                        () => instanceScope.IsCleanupConfirmed && (activation == null || activation.IsCleanupConfirmed));
                    FinishResourceReleaseTrace(phase, errors, before);
                }
            }
            ownedResource = null;
            View = null;
            Lifecycle = null;
        }

        private async ValueTask ReleaseInstanceAsync(List<Exception> errors, Action[] detachHost)
        {
            if (instanceReleased)
            {
                return;
            }
            instanceReleased = true;
            if (Lifecycle != null)
            {
                Lifecycle.Destroy(errors);
            }
            await ViewInstanceCleanup.RunAsync(instance, null, errors, detachHost);
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
