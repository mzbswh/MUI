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

        void BeginParentActivation(ChildViewScope scope, Lifetime lifetime);

        /// <summary>直接检查同步准备状态；失败抛出原错误，不查询异步任务。</summary>
        bool TryCompleteSynchronousPreparation();
    }
}
