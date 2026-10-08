using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Samples.Navigation
{
    /// <summary>使用统一参数更新契约的道具 Presenter；候选负责隔离准备与提交。</summary>
    public sealed class LocalThingPresenter : Presenter<ThingItemViewModel, string, Unit>,
        IArgsUpdatePresenter<string>
    {
        public const string FailingArgs = "演示部分提交失败";

        protected override void OnOpen(string args) => ViewModel.Label = args;

        public ValueTask<IPreparedArgsUpdate> PrepareArgsUpdateAsync(string args, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(args))
            {
                throw new ArgumentException("道具显示文本不能为空。", nameof(args));
            }

            // 准备阶段只保存快照，不改变活动模型；返回后由框架负责唯一释放。
            return new ValueTask<IPreparedArgsUpdate>(new PreparedUpdate(ViewModel, args));
        }

        private sealed class PreparedUpdate : IPreparedArgsUpdate
        {
            private ThingItemViewModel model;
            private readonly string next;

            public PreparedUpdate(ThingItemViewModel model, string next)
            {
                this.model = model;
                this.next = next;
            }

            public void Commit()
            {
                model.Label = next;
                if (next == FailingArgs)
                {
                    throw new InvalidOperationException("示例在修改显示后故意失败，以观察故障关闭。");
                }
            }


            public ValueTask DisposeAsync()
            {
                // 本候选不拥有业务模型，只释放借用引用；实际候选也应在此回收未移交资源。
                model = null;
                return default;
            }
        }
    }
}
