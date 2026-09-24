using System;

namespace MUI.Navigation
{
    /// <summary>内部准入拒绝；在事务边界转为类型化结果，不作为生命周期故障发布。</summary>
    internal sealed class NavigationPreparationRejectedException : InvalidOperationException
    {
        internal NavigationPreparationRejectedException(OpenRejection rejection, string message) : base(message)
        {
            Rejection = rejection;
        }

        internal OpenRejection Rejection
        {
            get;
        }
    }
}
