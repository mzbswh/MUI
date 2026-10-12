using System;

namespace MUI.Pages
{
    /// <summary>内部准入拒绝；在事务边界转为类型化结果，不作为生命周期故障发布。</summary>
    internal sealed class PagePreparationRejectedException : InvalidOperationException
    {
        internal PagePreparationRejectedException(OpenRejection rejection, string message) : base(message)
        {
            Rejection = rejection;
        }

        internal OpenRejection Rejection
        {
            get;
        }
    }
}
