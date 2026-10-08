using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.ChildViews;
using MUI.Resources;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>
    /// 动态子界面边界。Source 控制实例存在，ViewModel 控制
    /// 业务绑定。绑定此元素前需配置借用的提供方。
    /// 返回的 UGUI 实例在仍隐藏时挂载到此元素下。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DynamicViewElement : Element, IElementBoundary, IChildViewElement, IBindingRebindTarget
    {
        private IViewProvider provider;
        private IViewProvider displayedProvider;
        private ChildViewScope scope;
        private ChildViewSlot slot;
        private string source;
        private ViewModel model;
        private bool showUnbound;
        private Task<ChildViewChangeResult> pending;
        private Task preparation;
        private object refreshVersion;
        private PreparedRebind preparedRebind;

        public string Source
        {
            get => source;
            set
            {
                RequireAlive();
                ValidateSource(value);
                if (source == value && !HasPreparationFailure)
                {
                    return;
                }

                source = value;
                if (preparedRebind == null)
                {
                    Refresh();
                    NotifyChanged();
                }
            }
        }

        public ViewModel ViewModel
        {
            get => model;
            set
            {
                RequireAlive();
                if (ReferenceEquals(model, value) && !HasPreparationFailure)
                {
                    return;
                }

                if (value != null)
                {
                    BindingRegistry.GetManifest(value.GetType());
                }

                model = value;
                if (preparedRebind == null)
                {
                    Refresh();
                    NotifyChanged();
                }
            }
        }

        public bool ShowUnbound
        {
            get => showUnbound;
            set
            {
                RequireAlive();
                if (showUnbound == value && !HasPreparationFailure)
                {
                    return;
                }

                showUnbound = value;
                if (preparedRebind == null)
                {
                    Refresh();
                    NotifyChanged();
                }
            }
        }

        /// <summary>最近一次内容变更的完成结果；未提交内容时为 Empty。</summary>
        public Task<ChildViewChangeResult> PendingChange => pending ??
            (pending = Task.FromResult(new ChildViewChangeResult(ChildViewChangeStatus.Empty)));

        private bool HasPreparationFailure => preparation != null && (preparation.IsFaulted || preparation.IsCanceled);

        public bool HasInstance => slot != null && slot.Current != null;

        public string DisplayedSource => HasInstance ? slot.Current.Resource.Key : null;

        public ViewModel DisplayedViewModel => HasInstance && !(slot.Current.Model is UnboundModel) ? slot.Current.Model : null;

        Task IChildViewElement.Preparation => preparation ?? Task.CompletedTask;

        public void Configure(IViewProvider viewProvider)
        {
            RequireAlive();
            if (viewProvider == null)
            {
                throw new ArgumentNullException(nameof(viewProvider));
            }

            if (ReferenceEquals(provider, viewProvider) && !HasPreparationFailure)
            {
                return;
            }

            provider = viewProvider;
            Refresh();
        }

        /// <summary>原子更新来源与模型组合，避免产生中间候选。</summary>
        public void SetContent(string resourceKey, ViewModel viewModel)
        {
            RequireAlive();
            ValidateSource(resourceKey);
            if (viewModel != null)
            {
                BindingRegistry.GetManifest(viewModel.GetType());
            }

            if (source == resourceKey && ReferenceEquals(model, viewModel) && !HasPreparationFailure)
            {
                return;
            }

            source = resourceKey;
            model = viewModel;
            if (preparedRebind == null)
            {
                Refresh();
                NotifyChanged(nameof(Source));
                NotifyChanged(nameof(ViewModel));
            }
        }

        protected override void OnInitialize()
        {
            if (GetComponent<View>() != null)
            {
                throw new InvalidOperationException("DynamicViewElement must be a content boundary below its parent View.");
            }

            OnDispose(() =>
            {
                if (slot != null)
                {
                    _ = ObserveCleanupAsync(slot.DisposeAsync());
                }

                slot = null;
                scope = null;
                displayedProvider = null;
                model = null;
                provider = null;
            });
        }

        void IChildViewElement.BeginParentActivation(ChildViewScope scope, LifetimeScope lifetime)
        {
            RequireAlive();
            this.scope = scope;
            slot = new ChildViewSlot(scope);
            displayedProvider = null;
            var activationSlot = slot;
            slot.CurrentChanged += _ =>
            {
                if (ReferenceEquals(slot, activationSlot))
                {
                    displayedProvider = null;
                }
            };
            refreshVersion = new object();
            // 初始绑定必须提供本次激活期望的内容。
            source = null;
            model = null;
            pending = null;
            preparation = null;
            lifetime.OnDispose(() =>
            {
                if (ReferenceEquals(slot, activationSlot))
                {
                    refreshVersion = null;
                    source = null;
                    model = null;
                    showUnbound = false;
                    slot = null;
                    this.scope = null;
                    displayedProvider = null;
                    provider = null;
                    pending = null;
                    preparation = null;
                    preparedRebind = null;
                }
            });
        }

        async ValueTask<IPreparedBindingTarget> IBindingRebindTarget.PrepareRebindAsync(
            IReadOnlyDictionary<string, object> values, CancellationToken cancellationToken)
        {
            RequireAlive();
            if (scope == null || slot == null || !scope.IsActive)
            {
                throw new InvalidOperationException("Dynamic content has no active parent scope.");
            }

            var nextSource = source;
            var nextModel = model;
            var nextShowUnbound = showUnbound;
            if (values.TryGetValue(nameof(Source), out var sourceValue))
            {
                if (sourceValue != null && !(sourceValue is string))
                {
                    throw new InvalidOperationException("Dynamic Source binding must produce a string.");
                }

                nextSource = (string)sourceValue;
            }

            if (values.TryGetValue(nameof(ViewModel), out var modelValue))
            {
                if (modelValue != null && !(modelValue is ViewModel))
                {
                    throw new InvalidOperationException("Dynamic ViewModel binding must produce a ViewModel.");
                }

                nextModel = (ViewModel)modelValue;
            }

            if (values.TryGetValue(nameof(ShowUnbound), out var showValue))
            {
                if (!(showValue is bool enabled))
                {
                    throw new InvalidOperationException("Dynamic ShowUnbound binding must produce a Boolean.");
                }

                nextShowUnbound = enabled;
            }

            ValidateSource(nextSource);
            if (nextModel != null)
            {
                BindingRegistry.GetManifest(nextModel.GetType());
            }

            var owner = slot;
            var parentScope = scope;
            var requestedProvider = provider;
            var previous = owner.Current;
            var version = refreshVersion;
            owner.ValidatePreparedCandidate(null);
            var unchanged = source == nextSource && ReferenceEquals(model, nextModel) &&
                showUnbound == nextShowUnbound && !HasPreparationFailure &&
                (string.IsNullOrEmpty(nextSource) ? previous == null :
                    previous != null && previous.Resource.Key == nextSource &&
                    ReferenceEquals(previous.Model, nextModel) &&
                    ReferenceEquals(displayedProvider, requestedProvider));
            ChildViewHandle candidate = null;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!unchanged && !string.IsNullOrEmpty(nextSource))
                {
                    if (requestedProvider == null)
                    {
                        throw new InvalidOperationException("Configure a ViewProvider before binding a dynamic Source.");
                    }

                    var resource = new ViewResource(nextSource);
                    var assigned = nextModel ?? new UnboundModel();
                    var mounted = new ContentViewProvider(requestedProvider, transform,
                        nextModel != null || nextShowUnbound);
                    var template = new ChildViewTemplate<ViewModel, Unit>(resource,
                        () => throw new InvalidOperationException("Dynamic content uses an assigned model."),
                        (view, value) => value is UnboundModel ? new UnboundBinding(view, value) : BindingRegistry.Create(view, value));
                    candidate = await parentScope.PrepareAsync(template, mounted, Unit.Value, assigned, cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                return new PreparedRebind(this, parentScope, owner, previous, version, requestedProvider,
                    nextSource, nextModel, nextShowUnbound, candidate, unchanged);
            }
            catch (Exception failure)
            {
                try
                {
                    if (candidate != null)
                    {
                        await candidate.DisposeAsync();
                    }
                    else if (failure is ChildViewPreparationException preparationFailure)
                    {
                        await preparationFailure.CleanupCompletion;
                    }
                }
                catch (Exception cleanup)
                {
                    throw new AggregateException("Dynamic rebind preparation and cleanup failed.", failure, cleanup);
                }

                throw;
            }
        }

        private void Refresh()
        {
            if (slot == null)
            {
                return;
            }

            var version = new object();
            refreshVersion = version;
            var owner = slot;
            // 在调用提供方、绑定或版本查询前发布本轮信号，重入读取不能取得上一轮结果。
            var completion = new TaskCompletionSource<ChildViewChangeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending = completion.Task;
            var preparationCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var prepared = preparationCompletion.Task;
            preparation = prepared;
            try
            {
                _ = CompleteChangeAsync(StartChange(owner, version), completion, preparationCompletion);
            }
            catch (Exception error)
            {
                CompleteChange(new ChildViewChangeResult(ChildViewChangeStatus.Failed, error), completion, preparationCompletion);
            }

            if (!ReferenceEquals(refreshVersion, version) || !ReferenceEquals(slot, owner))
            {
                _ = ObservePreparationAsync(prepared);
                return;
            }

            if (prepared.IsCompleted)
            {
                prepared.GetAwaiter().GetResult();
            }
            else
            {
                _ = ObservePreparationAsync(prepared);
            }
        }

        private Task<ChildViewChangeResult> StartChange(ChildViewSlot owner, object version)
        {
            Task<ChildViewChangeResult> change;
            if (string.IsNullOrEmpty(source))
            {
                change = owner.ClearAsync().AsTask();
            }
            else
            {
                if (provider == null)
                {
                    throw new InvalidOperationException("Configure a ViewProvider before binding a dynamic Source.");
                }

                var resource = new ViewResource(source);
                var assigned = model ?? new UnboundModel();
                var currentProvider = provider;

                var reusable = FindReusableContent(owner);
                if (!ReferenceEquals(refreshVersion, version) || !ReferenceEquals(slot, owner))
                {
                    return Task.FromResult(new ChildViewChangeResult(ChildViewChangeStatus.Superseded));
                }

                if (reusable != null)
                {
                    change = owner.RebindAsync(reusable, assigned).AsTask();
                }
                else
                {
                    var mounted = new ContentViewProvider(currentProvider, transform, model != null || showUnbound);
                    var template = new ChildViewTemplate<ViewModel, Unit>(resource, () => throw new InvalidOperationException("Dynamic content uses an assigned model."), (view, value) => value is UnboundModel ? new UnboundBinding(view, value) : BindingRegistry.Create(view, value));
                    change = owner.ReplaceAsync(async (scope, token) => await scope.PrepareAsync(template, mounted, Unit.Value, assigned, token),
                        committed: _ =>
                        {
                            if (ReferenceEquals(slot, owner) && ReferenceEquals(refreshVersion, version))
                            {
                                displayedProvider = currentProvider;
                            }
                        }).AsTask();
                }
            }

            return change;
        }

        private static async Task CompleteChangeAsync(Task<ChildViewChangeResult> change,
            TaskCompletionSource<ChildViewChangeResult> completion, TaskCompletionSource<bool> preparationCompletion)
        {
            ChildViewChangeResult result;
            try
            {
                result = await change;
            }
            catch (OperationCanceledException)
            {
                result = new ChildViewChangeResult(ChildViewChangeStatus.Cancelled);
            }
            catch (Exception error)
            {
                result = new ChildViewChangeResult(ChildViewChangeStatus.Failed, error);
            }

            CompleteChange(result, completion, preparationCompletion);
        }

        private static void CompleteChange(ChildViewChangeResult result,
            TaskCompletionSource<ChildViewChangeResult> completion, TaskCompletionSource<bool> preparationCompletion)
        {
            completion.TrySetResult(result);
            if (result.Error != null || result.Status == ChildViewChangeStatus.Failed)
            {
                preparationCompletion.TrySetException(result.Error ?? new InvalidOperationException("Dynamic preparation failed."));
            }
            else if (result.Status == ChildViewChangeStatus.Cancelled || result.Status == ChildViewChangeStatus.ParentInactive)
            {
                preparationCompletion.TrySetCanceled();
            }
            else
            {
                preparationCompletion.TrySetResult(true);
            }
        }

        /// <summary>只复用同提供方的已绑定内容；首次准备和未绑定显示仍走候选替换。</summary>
        private ChildViewHandle<ViewModel, Unit> FindReusableContent(ChildViewSlot owner)
        {
            var expectedProvider = provider;
            var expectedSource = source;
            var expectedModel = model;
            var current = owner.Current as ChildViewHandle<ViewModel, Unit>;
            if (current == null || expectedModel == null || current.Model == null || current.Model is UnboundModel ||
                current.Model.GetType() != expectedModel.GetType() ||
                !ReferenceEquals(displayedProvider, expectedProvider) || current.Resource.Key != expectedSource ||
                !current.CanRebind)
            {
                return null;
            }

            // 资源版本查询可执行项目回调，回调后不能继续使用已改变的配置或实例。
            return ReferenceEquals(slot, owner) && ReferenceEquals(owner.Current, current) &&
                ReferenceEquals(provider, expectedProvider) && source == expectedSource &&
                ReferenceEquals(model, expectedModel) ? current : null;
        }

        private static void ValidateSource(string value)
        {
            if (!string.IsNullOrEmpty(value) && string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Dynamic Source cannot be whitespace.", nameof(value));
            }
        }

        private static async Task ObservePreparationAsync(Task task)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }

        private static async Task ObserveCleanupAsync(ValueTask task)
        {
            try
            {
                await task;
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }

        private sealed class PreparedRebind : IPreparedBindingTarget
        {
            private readonly DynamicViewElement element;
            private readonly ChildViewScope scope;
            private readonly ChildViewSlot slot;
            private readonly ChildViewHandle previous;
            private readonly object version;
            private readonly IViewProvider provider;
            private readonly string source;
            private readonly ViewModel model;
            private readonly bool showUnbound;
            private readonly bool unchanged;
            private ChildViewHandle candidate;
            private Task retirement;
            private bool adopted;
            private bool started;
            private string originalSource;
            private ViewModel originalModel;
            private bool originalShowUnbound;

            internal PreparedRebind(DynamicViewElement element, ChildViewScope scope, ChildViewSlot slot,
                ChildViewHandle previous, object version, IViewProvider provider, string source,
                ViewModel model, bool showUnbound, ChildViewHandle candidate, bool unchanged)
            {
                this.element = element;
                this.scope = scope;
                this.slot = slot;
                this.previous = previous;
                this.version = version;
                this.provider = provider;
                this.source = source;
                this.model = model;
                this.showUnbound = showUnbound;
                this.candidate = candidate;
                this.unchanged = unchanged;
            }

            public void Validate()
            {
                if (!element.IsAlive || !ReferenceEquals(element.scope, scope) ||
                    !ReferenceEquals(element.slot, slot) || !ReferenceEquals(element.provider, provider) ||
                    !ReferenceEquals(element.refreshVersion, version) ||
                    !ReferenceEquals(slot.Current, previous))
                {
                    throw new OperationCanceledException("Dynamic content changed during rebind preparation.");
                }

                slot.ValidatePreparedCandidate(candidate);
            }

            public void BeginCommit()
            {
                Validate();
                if (element.preparedRebind != null)
                {
                    throw new InvalidOperationException("Dynamic content already has a rebind commit.");
                }

                originalSource = element.source;
                originalModel = element.model;
                originalShowUnbound = element.showUnbound;
                element.preparedRebind = this;
                started = true;
            }

            public void Commit()
            {
                if (!started || !ReferenceEquals(element.preparedRebind, this) ||
                    element.source != source || !ReferenceEquals(element.model, model) ||
                    element.showUnbound != showUnbound)
                {
                    throw new InvalidOperationException("Dynamic binding writes differ from the prepared child content.");
                }

                Validate();
                if (!unchanged)
                {
                    retirement = slot.CommitPreparedCandidate(candidate);
                    adopted = true;
                    element.displayedProvider = string.IsNullOrEmpty(source) ? null : provider;
                    element.refreshVersion = new object();
                    element.pending = Task.FromResult(new ChildViewChangeResult(
                        string.IsNullOrEmpty(source) ? ChildViewChangeStatus.Empty : ChildViewChangeStatus.Ready));
                    element.preparation = Task.CompletedTask;
                }

                element.preparedRebind = null;
                if (originalSource != element.source)
                {
                    element.NotifyChanged(nameof(Source));
                }

                if (!ReferenceEquals(originalModel, element.model))
                {
                    element.NotifyChanged(nameof(ViewModel));
                }

                if (originalShowUnbound != element.showUnbound)
                {
                    element.NotifyChanged(nameof(ShowUnbound));
                }
            }

            public async ValueTask DisposeAsync()
            {
                if (ReferenceEquals(element.preparedRebind, this))
                {
                    element.preparedRebind = null;
                }

                if (!adopted && candidate != null)
                {
                    await candidate.DisposeAsync();
                }

                if (retirement != null)
                {
                    await retirement;
                }

                candidate = null;
            }
        }

        private sealed class UnboundModel : ViewModel
        {
        }

        private sealed class UnboundBinding : BindingContext<ViewModel>
        {
            public UnboundBinding(IView view, ViewModel model) : base(view, model)
            {
            }

            protected override void BuildBindings(BindingBuilder<ViewModel> builder)
            {
            }
        }
    }
}
