using System;
using System.Threading.Tasks;

namespace MUI
{
    public abstract partial class ViewModel
    {
        private LifetimeScope ownedWidgetVMs;

        /// <summary>外部持有父模型时，在所有投影清理后由拥有者调用；框架创建的父模型由框架调用。</summary>
        internal bool HasOwnedWidgetVMs => ownedWidgetVMs != null;

        /// <summary>生成的默认子模型归父模型所有；替换后仍保留到父模型最终释放，避免提前销毁仍被投影的模型。</summary>
        protected T CreateOwnedWidgetVM<T>(Func<T> factory) where T : ViewModel
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
            if (ownedWidgetVMs != null && ownedWidgetVMs.IsEnded)
                throw new ObjectDisposedException(GetType().Name);
            var vm = factory() ?? throw new InvalidOperationException("Widget VM factory returned null.");
            if (ReferenceEquals(vm, this))
                throw new InvalidOperationException("A model cannot own itself.");
            if (ownedWidgetVMs == null)
                ownedWidgetVMs = new LifetimeScope();
            ownedWidgetVMs.OnDisposeAsync(() => ReleaseOwnedVMAsync(vm));
            return vm;
        }

        public ValueTask ReleaseWidgetVMsAsync() => ownedWidgetVMs == null ? default : ownedWidgetVMs.DisposeAsync();

        internal static async ValueTask ReleaseOwnedVMAsync(ViewModel vm)
        {
            Exception failure = null;
            try
            {
                if (vm is IAsyncDisposable asyncDisposable)
                    await CleanupRegistry.GetResponsibility(asyncDisposable, "WidgetVM").DisposeAsync();
                else if (vm is IDisposable disposable)
                    disposable.Dispose();
            }
            catch (Exception error) { failure = error; }
            try
            {
                await vm.ReleaseWidgetVMsAsync();
            }
            catch (Exception error) { if (failure != null) throw new AggregateException(failure, error); throw; }
            if (failure != null)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
