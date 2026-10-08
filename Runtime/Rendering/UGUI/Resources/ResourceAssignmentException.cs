using System;
using MUI.Resources;

namespace MUI.UGUI
{
    /// <summary>
    /// 目标赋值抛错，但目标可能已经引用候选资源。资源槽必须保留新旧凭证并冻结，
    /// 直到最终清理成功清空目标后才能归还；不能按普通赋值失败释放候选。
    /// </summary>
    internal sealed class ResourceAssignmentException : Exception
    {
        public ResourceAssignmentException(Exception innerException)
            : base("资源赋值结果不确定，已保留资源，必须结束所属生命周期后再重新绑定。",
                innerException ?? throw new ArgumentNullException(nameof(innerException)))
        {
        }
    }
}
