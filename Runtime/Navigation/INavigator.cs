using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    /// <summary>顶层界面导航入口；在宿主 UI 线程使用，子视图不进入此导航集合。</summary>
    public interface INavigator
    {
        /// <summary>生命周期变化通知；可选依赖降级可发生在父提交前，回调不能阻塞等待自身导航操作。</summary>
        event Action<NavigationEvent> LifecycleChanged;

        /// <summary>开始有界生命周期追踪，清空上一轮记录；不订阅项目事件。</summary>
        void StartLifecycleTrace(int capacity = 512);

        /// <summary>停止追踪，可选择清除记录。</summary>
        void StopLifecycleTrace(bool clear = false);

        /// <summary>复制已记录的生命周期元数据，不创建任务。</summary>
        NavigationTraceSnapshot CaptureLifecycleTrace();

        /// <summary>直接依赖的独立快照；不转移所有权，未知或终态句柄返回空集合。</summary>
        IReadOnlyList<ViewHandle> GetDependencies(ViewHandle handle);

        /// <summary>父页面拥有者的独立快照，不包括显式打开关系。</summary>
        IReadOnlyList<ViewHandle> GetOwners(ViewHandle handle);

        /// <summary>可选依赖的降级原因快照；不转移资源所有权。</summary>
        IReadOnlyList<DependencyFailure> GetDependencyFailures(ViewHandle handle);

        /// <summary>当前实例是否仍被显式打开。</summary>
        bool HasExplicitOwnership(ViewHandle handle);

        /// <summary>撤销显式关系；需要关闭时异步等待，取消不撤回已提交关闭。</summary>
        ValueTask<ExplicitOwnershipReleaseOutcome> ReleaseExplicitOwnershipAsync(ViewHandle handle,
            CancellationToken cancellationToken = default);

        /// <summary>异步准备并打开界面，等待首次激活就绪；返回结果仍需检查成功状态及错误。</summary>
        ValueTask<OpenOutcome<TResult>> OpenAsync<TViewModel, TArgs, TResult>(Route<TViewModel, TArgs, TResult> route, TArgs args, CancellationToken cancellationToken = default, TViewModel assignedViewModel = null)
            where TViewModel : ViewModel;

        /// <summary>立即发起标准异步打开，提供取消与完成任务；请求对象不代表已成立的界面句柄。</summary>
        OpenRequest<TResult> BeginOpen<TViewModel, TArgs, TResult>(Route<TViewModel, TArgs, TResult> route, TArgs args, CancellationToken cancellationToken = default, TViewModel assignedViewModel = null)
            where TViewModel : ViewModel;

        /// <summary>先准备隐藏候选，再经过源界面守卫提交替换；源清理任务与目标激活结果分别报告。</summary>
        ValueTask<ReplaceOutcome<TResult>> ReplaceAsync<TViewModel, TArgs, TResult>(ViewHandle source, Route<TViewModel, TArgs, TResult> route, TArgs args, CancellationToken cancellationToken = default, TViewModel assignedViewModel = null)
            where TViewModel : ViewModel;

        /// <summary>显式更新活动实例参数；页面须支持隔离候选与同步提交，提交失败则故障关闭，不重新调用 OnOpen。</summary>
        ValueTask<ArgsUpdateOutcome> UpdateArgsAsync<TViewModel, TArgs, TResult>(ViewHandle source,
            Route<TViewModel, TArgs, TResult> route, TArgs args, CancellationToken cancellationToken = default)
            where TViewModel : ViewModel;

        /// <summary>借用新模型并换绑活动实例，保留句柄、参数及结果；自身绑定命令不能等待换绑。</summary>
        ValueTask<RebindOutcome> RebindAsync<TViewModel, TArgs, TResult>(ViewHandle source,
            Route<TViewModel, TArgs, TResult> route, TViewModel viewModel, CancellationToken cancellationToken = default)
            where TViewModel : ViewModel;

        /// <summary>按正常守卫请求关闭并等待结果；取消令牌只结束等待，不中止已开始的清理。</summary>
        ValueTask<CloseOutcome> CloseAsync(ViewHandle handle, CancellationToken cancellationToken = default);

        /// <summary>按前到后关闭调用时本层已提交页面快照；每项独立经过关闭守卫。</summary>
        ValueTask<BatchCloseOutcome> CloseLayerAsync(int layer, CancellationToken cancellationToken = default);

        /// <summary>关闭调用时全部已提交页面快照，不停止宿主或取消准备中的打开。</summary>
        ValueTask<BatchCloseOutcome> CloseAllAsync(CancellationToken cancellationToken = default);

        /// <summary>绕过业务守卫关闭指定界面；仍执行资源清理，调用者取消仅结束等待。</summary>
        ValueTask<CloseOutcome> ForceCloseAsync(ViewHandle handle, CancellationToken cancellationToken = default);

        /// <summary>等待已开始关闭的实例完成物理清理；超时隔离不缩短此等待，调用者可取消自己的等待。</summary>
        ValueTask<CloseOutcome> WaitForCleanupAsync(ViewHandle handle, CancellationToken cancellationToken = default);

        /// <summary>用类型化结果请求正常关闭；结果是否接受取决于关闭提交，仍需检查关闭结果。</summary>
        ValueTask<CloseOutcome> CompleteAsync<TResult>(ViewHandle<TResult> handle, TResult result, CancellationToken cancellationToken = default);

        /// <summary>处理一次返回操作，结合局部返回处理器与导航历史决定关闭目标。</summary>
        ValueTask<CloseOutcome> BackAsync(CancellationToken cancellationToken = default);

        /// <summary>直接捕获有界导航快照，不创建任务或执行项目/渲染器回调。</summary>
        NavigationSnapshot CaptureSnapshot(int maxInstances = 256);

        /// <summary>查询仍在账本中的实例快照，包括准备中与尚未完成清理的实例。</summary>
        bool TryGetInstanceSnapshot(ViewHandle handle, out ViewInstanceSnapshot snapshot);

        /// <summary>查询句柄状态；终态历史有界，历史过期不能继续当作活动界面使用。</summary>
        ViewState GetState(ViewHandle handle);

        /// <summary>请求将活动界面置前，不重新创建或激活实例。</summary>
        bool BringToFront(ViewHandle handle);

        /// <summary>停止接受导航工作，取消准备并等待请求与实例清理；不受单个打开请求令牌控制。</summary>
        ValueTask ShutdownAsync();

        /// <summary>由导航器取得并持有路由资源的预加载驻留凭证，不创建界面实例。</summary>
        ValueTask<PreloadOutcome> PreloadAsync(Route route, CancellationToken cancellationToken = default);

        /// <summary>清除导航器持有的预加载并等待相关收尾，不释放调用方持有的界面实例。</summary>
        ValueTask ClearPreloadsAsync();
    }
}
