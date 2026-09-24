using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>
    /// 页面与子视图共用的激活收尾顺序。调用者先撤销业务资格，并负责防止重复进入；
    /// 此处不决定导航结果、父级所有权或实例资源的最终释放。
    /// </summary>
    internal static partial class ViewActivationCleanup
    {
        /// <summary>
        /// 依次执行关闭回调、启动解绑、排空激活资源、等待解绑完成。
        /// 每一步失败仍继续后续清理；返回首个生命周期回调异常，所有异常追加到调用方列表。
        /// 回调由调用者包装，保留顶层导航与子视图各自的重入保护。
        /// </summary>
        internal static async ValueTask<Exception> RunAsync(
            Func<ValueTask> closeAsync,
            Action close,
            BindingContext binding,
            Lifetime activation,
            List<Exception> errors)
        {
            Exception lifecycleFailure = null;
            if (closeAsync != null)
            {
                try
                {
                    await closeAsync();
                }
                catch (Exception error)
                {
                    lifecycleFailure = error;
                    errors.Add(error);
                }
            }

            if (close != null)
            {
                try
                {
                    close();
                }
                catch (Exception error)
                {
                    lifecycleFailure = lifecycleFailure ?? error;
                    errors.Add(error);
                }
            }

            // 解绑先同步撤销订阅和命令资格，之后与激活资源清理共同排空。
            Task unbind = Task.CompletedTask;
            if (binding != null)
            {
                try
                {
                    unbind = binding.UnbindAsync().AsTask();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            try
            {
                await activation.DisposeAsync();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            try
            {
                await unbind;
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            return lifecycleFailure;
        }
    }
}
