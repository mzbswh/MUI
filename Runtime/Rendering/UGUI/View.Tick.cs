using System;

namespace MUI.UGUI
{
    public sealed partial class View
    {
        public event Action WidgetTickActivityChanged;

        public bool HasWidgetTicks => IsAlive && childViews != null && childViews.HasTicks;

        public void TickChildren(float deltaTime, float unscaledDeltaTime)
        {
            RequireAlive();
            if (childViews != null && childViews.HasTicks)
            {
                childViews.Tick(deltaTime, unscaledDeltaTime);
            }
        }

        private void NotifyWidgetTicks()
        {
            var handlers = WidgetTickActivityChanged;
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
