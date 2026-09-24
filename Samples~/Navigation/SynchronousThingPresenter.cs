using System;

namespace MUI.Samples.Navigation
{
    /// <summary>只实现同步契约的道具 Presenter；候选负责提交与恢复显示状态。</summary>
    public sealed class SynchronousThingPresenter : Presenter<ThingItemViewModel, string, Unit>,
        ISynchronousArgsUpdatePresenter<string>
    {
        public const string FailingArgs = "演示部分提交失败";

        protected override void OnOpen(string args) => ViewModel.Label = args;

        public ISynchronousPreparedArgsUpdate PrepareArgsUpdate(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                throw new ArgumentException("道具显示文本不能为空。", nameof(args));
            }

            // 准备阶段只保存快照，不改变活动模型；返回后由框架负责唯一释放。
            return new PreparedUpdate(ViewModel, args);
        }

        private sealed class PreparedUpdate : ISynchronousPreparedArgsUpdate
        {
            private ThingItemViewModel model;
            private readonly string previous;
            private readonly string next;

            public PreparedUpdate(ThingItemViewModel model, string next)
            {
                this.model = model;
                previous = model.Label;
                this.next = next;
            }

            public void Commit()
            {
                model.Label = next;
                if (next == FailingArgs)
                {
                    throw new InvalidOperationException("示例在修改显示后故意失败，以观察同步回滚。");
                }
            }

            public void Rollback() => model.Label = previous;

            public void Dispose()
            {
                // 本候选不拥有业务模型，只释放借用引用；实际候选也应在此回收未移交资源。
                model = null;
            }
        }
    }
}
