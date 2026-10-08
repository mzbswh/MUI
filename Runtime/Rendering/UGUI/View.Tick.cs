using System;

namespace MUI.UGUI
{
    public sealed partial class View
    {
        public event Action ChildTickActivityChanged;

        public bool HasChildTicks => IsAlive && childViews != null && childViews.HasTicks;

        public void TickChildren(float unscaledDeltaTime)
        {
            RequireAlive();
            if (childViews != null && childViews.HasTicks)
            {
                childViews.Tick(unscaledDeltaTime);
            }
        }

        private void NotifyChildTicks()
        {
            var handlers = ChildTickActivityChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action handler in handlers.GetInvocationList())
            {
                try
                {
                    handler();
                }
                catch (Exception error)
                {
                    UIErrors.Report(error);
                }
            }
        }
    }
}
