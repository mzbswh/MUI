using System.Threading.Tasks;
using MUI.ChildViews;

namespace MUI.UGUI
{
    internal interface IChildViewElement
    {
        Task Preparation
        {
            get;
        }

        void BeginParentActivation(ChildViewScope scope, LifetimeScope lifetime);
    }
}
