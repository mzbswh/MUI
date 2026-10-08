using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>可选渲染能力：在此 View 正下方设置宿主区域射线阻挡层。</summary>
    public interface IModalView : IView
    {
        /// <summary>准备尚不显示、不拦截输入的屏障；完成后才允许提交模态显示。</summary>
        Task PrepareModalBarrierAsync(CancellationToken cancellationToken);

        /// <summary>提交已准备屏障的显示或撤销阻挡；隐藏后仍须允许在原实例上恢复。</summary>
        void SetModalBarrier(bool enabled);
    }
}
