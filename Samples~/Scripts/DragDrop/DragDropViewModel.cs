using System;
using System.Threading.Tasks;
using MUI.DragDrop;
using MUI.UGUI;

namespace MUI.Examples
{
    [UIPage(typeof(Unit), typeof(Unit), ResourceKey = "MUIExamples/DragDrop")]
    public partial class DragDropViewModel : ViewModel, IAsyncDisposable
    {
        private readonly LifetimeScope lifetime = new LifetimeScope();
        private int delivered;

        public DragDropViewModel()
        {
            Source = new DragBinding<Crate>(lifetime, () => new Crate(7));
            Accept = Destination(false, false);
            Reject = Destination(true, false);
            Fail = Destination(false, true);
        }

        [Bind("Crate", nameof(DragSourceElement.Source))]
        public DragBinding<Crate> Source
        {
            get;
        }

        [Bind("Accept", nameof(DropTargetElement.Target))]
        public DropBinding<Crate> Accept
        {
            get;
        }

        [Bind("Reject", nameof(DropTargetElement.Target))]
        public DropBinding<Crate> Reject
        {
            get;
        }

        [Bind("Fail", nameof(DropTargetElement.Target))]
        public DropBinding<Crate> Fail
        {
            get;
        }

        private DropBinding<Crate> Destination(bool reject, bool fail) => new DropBinding<Crate>(new DropTarget<Crate>(lifetime, async (crate, token) =>
        {
            Status = "正在提交物资箱 " + crate.Id + "...";
            if (Delay)
            {
                await Task.Delay(600, token);
            }

            token.ThrowIfCancellationRequested();
            if (fail)
            {
                Status = "提交被拒绝，库存未修改。";
                return false;
            }

            Status = "提交成功，累计 " + ++delivered + " 次成功提交";
            return true;
        }, crate => !reject && crate.Id == 7));

        public void OnFinished(DropResult result)
        {
            if (!lifetime.IsEnded)
            {
                var outcome = result.Status == DropStatus.Committed ? "提交成功" :
                    result.Status == DropStatus.Rejected ? "提交被拒绝" :
                    result.Status == DropStatus.Cancelled ? "已取消" : "提交失败";
                Status = outcome + "，累计 " + delivered + " 次成功提交";
            }
        }

        public ValueTask DisposeAsync() => lifetime.DisposeAsync();

        public readonly struct Crate
        {
            public Crate(int id)
            {
                Id = id;
            }

            public int Id
            {
                get;
            }
        }
    }
}
