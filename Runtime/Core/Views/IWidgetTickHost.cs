using System;

namespace MUI
{
    public interface IWidgetTickHost : IView
    {
        event Action WidgetTickActivityChanged;

        bool HasWidgetTicks
        {
            get;
        }

        void TickChildren(float deltaTime, float unscaledDeltaTime);
    }
}
