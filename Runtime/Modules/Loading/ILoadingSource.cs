using System;

namespace MUI.Loading
{
    /// <summary>
    /// UI 线程上的只读加载表现来源。实现方负责聚合、时钟和输入阻挡，观察者不获取输入锁。
    /// Snapshot 读取必须无副作用，事件访问器只负责订阅与退订，发布时应隔离观察者异常。
    /// </summary>
    public interface ILoadingSource
    {
        /// <summary>在所属 UI 线程通知完整的新状态。</summary>
        event Action<LoadingSnapshot> Changed;

        /// <summary>当前完整状态；控件可读取初始值，并通过 Changed 接收后续状态。</summary>
        LoadingSnapshot Snapshot
        {
            get;
        }
    }
}
