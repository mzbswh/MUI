namespace MUI
{
    /// <summary>可选的查询版本契约，消费方据此拒绝同一来源内旧查询的异步状态回写。</summary>
    public interface IVersionedPagedListSource
    {
        /// <summary>非空的身份标记；接受查询重置时立即替换，旧标记不得复用。</summary>
        object QueryVersion
        {
            get;
        }
    }
}
