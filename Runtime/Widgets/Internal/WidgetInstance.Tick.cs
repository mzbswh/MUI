using System;

namespace MUI.Widgets
{
    internal sealed partial class WidgetInstance<TViewModel, TArgs>
        where TViewModel : ViewModel
    {
        private bool hasPresenterTick;

        internal override bool WantsTick => IsActive && bindingsCommitted && (hasPresenterTick || (view is IWidgetTickHost child && child.HasWidgetTicks));

        private bool CanTick() => !IsRebinding && WantsTick && (!template.PauseTickWhenHidden || (parentVisible && localVisible && (!(view is IVisibilityView visible) || visible.IsVisible)));

        internal override void Tick(float deltaTime, float unscaledDeltaTime)
        {
            if (!CanTick())
            {
                return;
            }

            if (hasPresenterTick)
            {
                try
                {
                    lifecycle.Update(deltaTime, unscaledDeltaTime);
                }
                catch (Exception error) { UIErrors.Report(error); }
            }

            if (CanTick() && view is IWidgetTickHost children && children.HasWidgetTicks)
            {
                Invoke(() => children.TickChildren(deltaTime, unscaledDeltaTime));
            }
        }
    }
}
