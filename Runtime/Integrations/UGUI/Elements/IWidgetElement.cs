using System.Threading.Tasks;
using MUI.Widgets;

namespace MUI.UGUI
{
    internal interface IWidgetElement
    {
        Task Preparation
        {
            get;
        }

        void BeginParentActivation(WidgetScope scope, LifetimeScope lifetime);
    }
}
