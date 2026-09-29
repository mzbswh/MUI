using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>页面与子视图共用的隐藏准备和异步准备协议，不提交显示或取得导航资格。</summary>
    internal static class ViewPreparation
    {
        /// <summary>先隐藏本视图，再建立本次激活的子视图所有权；外部回调后重新确认准备资格。</summary>
        internal static void Begin(IView view, Lifetime activation, Action<Action> invoke, Action requireCurrent)
        {
            requireCurrent();
            invoke(() => view.SetHostState(false, false));
            requireCurrent();
            if (view is IChildViewHost childHost)
            {
                invoke(() => childHost.BeginChildActivation(activation));
                requireCurrent();
            }
        }

        /// <summary>绑定必须属于本实例的模型和 View，且尚未开始绑定。</summary>
        internal static void ValidateBinding(BindingContext binding, IView view, ViewModel model)
        {
            if (binding == null)
            {
                throw new InvalidOperationException("View binding factory returned null.");
            }

            if (!ReferenceEquals(binding.Model, model))
            {
                throw new InvalidOperationException("View binding factory returned a different model.");
            }

            if (!ReferenceEquals(binding.BoundView, view))
            {
                throw new InvalidOperationException("View binding factory must return a context for the current view.");
            }

            if (binding.State != BindingState.Unbound)
            {
                throw new InvalidOperationException("View binding factory must return an unbound context.");
            }
        }

        /// <summary>
        /// 先等待业务异步打开，再等待必需子视图准备。每次调用和等待后检查原请求与宿主资格；
        /// 具体回调保护由宿主提供，成功仅表示准备结束，显示提交仍由宿主协调。
        /// </summary>
        internal static async ValueTask CompleteAsync(
            IView view,
            Func<CancellationToken, ValueTask> openAsync,
            Action requireCurrent,
            CancellationToken token)
        {
            requireCurrent();
            token.ThrowIfCancellationRequested();
            if (openAsync != null)
            {
                await openAsync(token);
                requireCurrent();
                token.ThrowIfCancellationRequested();
            }

            if (view is IChildViewHost childHost)
            {
                await childHost.CompleteChildPreparationAsync(token);
                requireCurrent();
                token.ThrowIfCancellationRequested();
            }
        }
    }
}
