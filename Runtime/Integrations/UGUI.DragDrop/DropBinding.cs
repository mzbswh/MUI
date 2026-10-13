using System;
using MUI.DragDrop;

namespace MUI.UGUI
{
    public abstract class DropBinding
    {
    }

    public sealed class DropBinding<TPayload> : DropBinding
    {
        public DropBinding(DropTarget<TPayload> target)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        internal DropTarget<TPayload> Target
        {
            get;
        }
    }
}
