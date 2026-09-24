namespace MUI
{
    /// <summary>生命周期接受的资源与操作类型；创建后不可切换。</summary>
    public enum LifetimeMode
    {
        /// <summary>兼容同步资源与异步资源，销毁时可能需要等待。</summary>
        AsyncAllowed,

        /// <summary>只接受同步释放的资源，禁止登记异步工作。</summary>
        Synchronous
    }
}
