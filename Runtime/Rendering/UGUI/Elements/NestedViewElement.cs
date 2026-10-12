using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.Widgets;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>旧包装节点入口；新静态 Widget 由父 View 的子 Widget 引用直接管理。</summary>
    [DisallowMultipleComponent]
    public sealed partial class NestedViewElement : Element, IElementBoundary, IWidgetElement, IBindingRebindTarget
    {
        [SerializeField] private View childView;
        private WidgetReference reference;
        private CleanupResponsibility nodeCleanup;
        private LifetimeScope parentLifetime;

        public ViewModel ViewModel
        {
            get => reference == null ? null : reference.VM; set
            {
                RequireAlive();
                RequireAvailableNode();
                reference.VM = value;
            }
        }

        public ViewModel VM
        {
            get => ViewModel; set => ViewModel = value;
        }

        public ViewModel DisplayedViewModel => reference == null ? null : reference.DisplayedViewModel;

        public Task PendingChange => reference == null ? Task.CompletedTask : reference.PendingChange;

        internal bool IsExecutingChild => reference != null && reference.IsExecutingChild;

        Task IWidgetElement.Preparation => reference == null ? Task.CompletedTask : ((IWidgetElement)reference).Preparation;

        protected override void OnInitialize()
        {
            if (childView == null)
                foreach (Transform child in transform)
                {
                    var candidate = child.GetComponent<View>();
                    if (candidate == null)
                        continue;
                    if (childView != null)
                        throw new InvalidOperationException("NestedViewElement requires one explicit child View when several are present.");
                    childView = candidate;
                }
            if (GetComponent<View>() != null || childView == null || childView.transform == transform || !childView.transform.IsChildOf(transform))
                throw new InvalidOperationException("Place NestedViewElement on a wrapper containing a descendant View.");
            reference = new WidgetReference(Name, childView, () => IsAlive && nodeCleanup == null,
                async (scope, provider, vm, args, token) => await scope.PrepareAsync(
                    new WidgetDefinition<ViewModel, Unit>(new MUI.Resources.ViewResource("nested:" + childView.GetInstanceID()),
                        () => throw new InvalidOperationException("A nested VM is required."), BindingRegistry.Create),
                    provider, Unit.Value, vm, token));
            reference.PropertyChanged += (_, change) =>
            {
                if (change.PropertyName != nameof(WidgetReference.VM))
                    NotifyChanged(change.PropertyName);
            };
            OnDispose(reference.Dispose);
        }

        void IWidgetElement.BeginParentActivation(WidgetScope scope, LifetimeScope lifetime)
        {
            parentLifetime = lifetime;
            ((IWidgetElement)reference).BeginParentActivation(scope, lifetime);
        }

        ValueTask<IPreparedBindingTarget> IBindingRebindTarget.PrepareRebindAsync(IReadOnlyDictionary<string, object> values, CancellationToken token)
            => ((IBindingRebindTarget)reference).PrepareRebindAsync(values, token);
    }
}
