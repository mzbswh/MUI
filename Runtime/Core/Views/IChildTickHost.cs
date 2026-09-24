using System;

namespace MUI
{
    public interface IChildTickHost : IView
    {
        event Action ChildTickActivityChanged;

        bool HasChildTicks
        {
            get;
        }

        void TickChildren(float unscaledDeltaTime);
    }
}
