using System;
using System.Threading.Tasks;
using MUI.DragDrop;

namespace MUI.UGUI
{
    public abstract class DragBinding
    {
        internal abstract DragInteraction Begin(IDisposable capture, Action<DropResult> settle);
    }

    public sealed class DragBinding<TPayload> : DragBinding
    {
        private readonly LifetimeScope owner;
        private readonly Func<TPayload> payload;

        public DragBinding(LifetimeScope owner, Func<TPayload> payload)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        internal override DragInteraction Begin(IDisposable capture, Action<DropResult> settle) => new Interaction(new DragSession<TPayload>(payload(), owner, capture, settle));

        private sealed class Interaction : DragInteraction
        {
            private readonly DragSession<TPayload> session;

            internal Interaction(DragSession<TPayload> session)
            {
                this.session = session;
            }

            internal override Task<DropResult> Completion => session.Completion;

            internal override bool IsCompleted => session.IsCompleted;

            internal override bool IsDragging => session.Phase == DragPhase.Dragging;

            internal override void Pump() => session.Pump();

            internal override void Cancel() => session.Cancel();

            internal override bool CanDrop(DropBinding target) => target is DropBinding<TPayload> typed && session.CanDrop(typed.Target);

            internal override bool TryDrop(DropBinding target)
            {
                if (!(target is DropBinding<TPayload> typed))
                {
                    return false;
                }

                _ = session.DropAsync(typed.Target);
                return true;
            }
        }
    }

    internal abstract class DragInteraction
    {
        internal abstract Task<DropResult> Completion
        {
            get;
        }

        internal abstract bool IsCompleted
        {
            get;
        }

        internal abstract bool IsDragging
        {
            get;
        }

        internal abstract void Pump();

        internal abstract void Cancel();

        internal abstract bool CanDrop(DropBinding target);

        internal abstract bool TryDrop(DropBinding target);
    }
}
