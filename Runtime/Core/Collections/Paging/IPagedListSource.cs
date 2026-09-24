using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>分页控制契约，与条目类型和具体游标类型解耦；消费方借用来源，不取得销毁权。</summary>
    public interface IPagedListSource
    {
        event Action StateChanged;

        PageInsertion Insertion
        {
            get;
        }

        bool IsActive
        {
            get;
        }

        bool IsLoading
        {
            get;
        }

        bool HasMore
        {
            get;
        }

        Exception Error
        {
            get;
        }

        ValueTask<PageLoadResult> LoadNextAsync(CancellationToken cancellationToken = default);
    }
}
