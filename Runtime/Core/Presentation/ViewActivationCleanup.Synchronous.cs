using System;
using System.Collections.Generic;

namespace MUI
{
    internal static partial class ViewActivationCleanup
    {
        /// <summary>
        /// 同步结束激活：关闭回调、解绑、激活资源释放按序执行，每一步失败仍继续清理。
        /// 只接受预先约束过的同步 Lifetime，防止关闭回调临时登记异步工作。
        /// 能力不满足时在调用关闭回调、退订或取消之前拒绝。
        /// </summary>
        internal static Exception Run(Action close, BindingContext binding, Lifetime activation, List<Exception> errors)
        {
            RequireSynchronous(binding, activation);
            Exception lifecycleFailure = null;
            if (close != null)
            {
                try
                {
                    close();
                }
                catch (Exception error)
                {
                    lifecycleFailure = error;
                    errors.Add(error);
                }
            }

            if (binding != null)
            {
                try
                {
                    binding.Unbind();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            try
            {
                activation.Dispose();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            return lifecycleFailure;
        }

        internal static void RequireSynchronous(BindingContext binding, Lifetime activation)
        {
            if (activation == null)
            {
                throw new ArgumentNullException(nameof(activation));
            }

            if (activation.Mode != LifetimeMode.Synchronous || !activation.CanDisposeSynchronously
                || (binding != null && (binding.LifetimeMode != LifetimeMode.Synchronous || !binding.CanUnbindSynchronously)))
            {
                throw new InvalidOperationException("Activation cleanup requires a synchronous lifetime and binding without pending work.");
            }
        }
    }
}
