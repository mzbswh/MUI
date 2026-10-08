using System;
using MUI.Resources;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewHandle<TViewModel, TArgs> where TViewModel : ViewModel
    {
        private object contentProvider;
        private object providerVersion;
        private object bindingGeneration;

        /// <summary>准备、缓存恢复与提交共用创建时的代际，不给旧实例重新贴上新版本。</summary>
        public override bool IsContentCurrent
        {
            get
            {
                Owner.RequireThread();
                if (contentProvider == null || closeStarted)
                {
                    return false;
                }

                var current = ReadProviderVersion();
                return !closeStarted && ReferenceEquals(providerVersion, current) &&
                    ReferenceEquals(bindingGeneration, BindingRegistry.Generation);
            }
        }

        internal void InitializeProvider(object provider)
        {
            contentProvider = provider;
            bindingGeneration = BindingRegistry.Generation;
            providerVersion = ReadProviderVersion();
        }

        private object ReadProviderVersion()
        {
            object version = null;
            Invoke(() => version = contentProvider is IViewContentVersion versioned
                ? versioned.ContentVersion : contentProvider);
            if (version == null || version.GetType().IsValueType)
            {
                throw new InvalidOperationException("Child view provider version must be a non-null reference token.");
            }

            return version;
        }

        private void RequireContentCurrent()
        {
            if (!IsContentCurrent)
            {
                throw new OperationCanceledException("Child view content or binding generation changed.");
            }
        }

        private void ReleaseContentVersion()
        {
            contentProvider = null;
            providerVersion = null;
            bindingGeneration = null;
        }
    }
}
