using System;
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
    public sealed partial class DynamicViewElement : Element, IElementBoundary, IChildViewElement
    {
        private object provider;
        private object displayedProvider;
        private ChildViewSlot slot;
        private string source;
        private ViewModel model;
        private bool showUnbound;
        private Task<ChildViewChangeResult> pending;
        private Task preparation;
        private object refreshVersion;

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

                RequireSynchronousIdle();
                source = value;
                Refresh();
                NotifyChanged();
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

                RequireSynchronousIdle();
                model = value;
                Refresh();
                NotifyChanged();
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

                RequireSynchronousIdle();
                showUnbound = value;
                Refresh();
                NotifyChanged();
            }
        }

        /// <summary>选择结果；同步模式按需提供兼容信号，回调内查询在当前替换结束后完成。</summary>
        public Task<ChildViewChangeResult> PendingChange => GetPendingChange();

        public bool HasInstance => slot != null && slot.Current != null;

        public string DisplayedSource => HasInstance ? slot.Current.Resource.Key : null;

        public ViewModel DisplayedViewModel => HasInstance && !(slot.Current.Model is UnboundModel) ? slot.Current.Model : null;

        Task IChildViewElement.Preparation => GetPreparation();

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

            RequireSynchronousIdle();
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

            RequireSynchronousIdle();
            if (source == resourceKey && ReferenceEquals(model, viewModel) && !HasPreparationFailure)
            {
                return;
            }

            source = resourceKey;
            model = viewModel;
            Refresh();
            NotifyChanged(nameof(Source));
            NotifyChanged(nameof(ViewModel));
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
                    if (slot.Mode == LifetimeMode.Synchronous)
                    {
                        slot.Dispose();
                    }
                    else
                    {
                        _ = ObserveCleanupAsync(slot.DisposeAsync());
                    }
                }

                slot = null;
                displayedProvider = null;
                model = null;
                provider = null;
            });
        }

        void IChildViewElement.BeginParentActivation(ChildViewScope scope, Lifetime lifetime)
        {
            RequireAlive();
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
            synchronousPreparationFailure = null;
            synchronousChange = lifetime.Mode == LifetimeMode.Synchronous;
            synchronousResult = new ChildViewChangeResult(ChildViewChangeStatus.Empty);
            synchronousResultCompletion = null;
            synchronousPreparationCompletion = null;
            // 初始绑定必须提供本次激活期望的内容。
            source = null;
            model = null;
            pending = null;
            preparation = null;
        }

        private void Refresh()
        {
            if (slot == null)
            {
                return;
            }

            if (slot.Mode == LifetimeMode.Synchronous)
            {
                RefreshSynchronous();
                return;
            }

            synchronousChange = false;
            var version = new object();
            refreshVersion = version;
            var owner = slot;
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
                if (!(provider is IViewProvider asynchronous))
                {
                    throw new InvalidOperationException("Asynchronous dynamic content requires an IViewProvider.");
                }

                var reusable = FindReusableContent(owner);
                if (!ReferenceEquals(refreshVersion, version) || !ReferenceEquals(slot, owner))
                {
                    return;
                }

                if (reusable != null)
                {
                    change = owner.RebindAsync(reusable, assigned).AsTask();
                }
                else
                {
                    var mounted = new ContentViewProvider(asynchronous, transform, model != null || showUnbound);
                    var template = new ChildViewTemplate<ViewModel, Unit>(resource, () => throw new InvalidOperationException("Dynamic content uses an assigned model."), (view, value) => value is UnboundModel ? new UnboundBinding(view, value) : BindingRegistry.Create(view, value));
                    change = owner.ReplaceAsync(async (scope, token) => await scope.PrepareAsync(template, mounted, Unit.Value, assigned, token),
                        committed: _ =>
                        {
                            if (ReferenceEquals(slot, owner))
                            {
                                displayedProvider = asynchronous;
                            }
                        }).AsTask();
                }
            }

            var prepared = RequirePreparedAsync(change);
            if (!ReferenceEquals(refreshVersion, version) || !ReferenceEquals(slot, owner))
            {
                // 创建或清理回调可能已发起新切换或新激活，旧请求只负责观察自身结果。
                _ = ObservePreparationAsync(prepared);
                return;
            }

            pending = change;
            preparation = prepared;
            if (prepared.IsCompleted)
            {
                prepared.GetAwaiter().GetResult();
            }
            else
            {
                _ = ObservePreparationAsync(prepared);
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

        private static async Task RequirePreparedAsync(Task<ChildViewChangeResult> change)
        {
            var result = await change;
            // 新内容提交成功也可能携带旧内容的清理错误，与同步准备保持一致。
            if (result.Error != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(result.Error).Throw();
            }

            if (result.Status == ChildViewChangeStatus.Failed)
            {
                throw result.Error ?? new InvalidOperationException("Dynamic preparation failed.");
            }

            if (result.Status == ChildViewChangeStatus.Cancelled || result.Status == ChildViewChangeStatus.ParentInactive)
            {
                throw new OperationCanceledException("Dynamic preparation was cancelled.");
            }
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
