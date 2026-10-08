using System;
using System.Threading;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        /// <summary>立即启动标准异步打开流程，返回请求控制对象，不提前暴露 ViewHandle。</summary>
        public OpenRequest<TResult> BeginOpen<TViewModel, TArgs, TResult>(Route<TViewModel, TArgs, TResult> route,
            TArgs args,
            CancellationToken cancellationToken = default,
            TViewModel assignedViewModel = null)
            where TViewModel : ViewModel
        {
            AssertThread();
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            return new OpenRequest<TResult>(token => OpenAsync(route, args, token, assignedViewModel), cancellationToken);
        }
    }
}
