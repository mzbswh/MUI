using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>无需等待结果时使用的命令入口，不改变命令自身的执行及并发规则。</summary>
    public static class UICommandExtensions
    {
        /// <summary>
        /// 在当前线程立即触发执行，异步工作继续运行，失败统一交给 UIErrors。
        /// 取消和拒绝不报告为错误；需要结果时使用 ExecuteAsync。
        /// 不自动补充界面来源或绑定会话，调用方负责传入来源和取消信号。
        /// </summary>
        public static void Execute(this IUICommand command, ICommandTarget source = null,
            CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            _ = ExecuteObservedAsync(command, source, cancellationToken);
        }

        private static async Task ExecuteObservedAsync(IUICommand command, ICommandTarget source,
            CancellationToken cancellationToken)
        {
            try
            {
                var outcome = await command.ExecuteAsync(source, cancellationToken);
                if (outcome.Status == CommandStatus.Failed)
                {
                    UIErrors.Report(outcome.Error);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }
    }
}
