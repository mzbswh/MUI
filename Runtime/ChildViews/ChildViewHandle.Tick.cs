using System;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewHandle<TViewModel, TArgs>
        where TViewModel : ViewModel
    {
        private float tickInterval;
        private int maxTickCatchUp = 1;
        private double tickRemainder;
        private bool hasPresenterTick;

        internal override bool WantsTick => IsActive && bindingsCommitted && (hasPresenterTick || (view is IChildTickHost child && child.HasChildTicks));

        private bool CanTick() => !IsRebinding && WantsTick && (!template.PauseTickWhenHidden || (parentVisible && localVisible && (!(view is IVisibilityView visible) || visible.IsVisible)));

        internal override void Tick(float delta)
        {
            if (!CanTick())
            {
                return;
            }

            if (hasPresenterTick)
            {
                var count = ViewTickTiming.Due(ref tickRemainder, delta, tickInterval, maxTickCatchUp);
                for (var i = 0; i < count && CanTick(); i++)
                {
                    try
                    {
                        Invoke(() =>
                        {
                            if (presenter is IViewTick frame)
                            {
                                frame.OnViewTick(delta);
                            }
                            else
                            {
                                ((ILowFrequencyViewTick)presenter).OnLowFrequencyTick(tickInterval);
                            }
                        });
                    }
                    catch (Exception error)
                    {
                        UIErrors.Report(error);
                    }
                }
            }

            if (CanTick() && view is IChildTickHost children && children.HasChildTicks)
            {
                Invoke(() => children.TickChildren(delta));
            }
        }
    }
}
