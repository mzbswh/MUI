# 预加载与异步 Prefab Provider

当前接入边界：LoadedPrefabViewProvider 与 SynchronousLoadedPrefabViewProvider 已移至 Samples~/ResourceIntegration，命名空间为 MUI.Samples.ResourceIntegration；它们是项目侧提供方示例，需导入该示例后使用。框架只定义 IViewProvider/IPreloadViewProvider 等契约并持有其返回的界面凭证。下文加载和驻留策略描述的是该示例，不是框架要求。

`IPreloadViewProvider.PreloadAsync` 返回独立的 `IPreloadLease`。PreloadLease 是幂等释放包装，持有资源身份但不创建 View，不执行 Presenter/绑定钩子。创建 View 不消费这个持有；调用方可在创建完成后释放预加载，实例仍保有自己的资源引用。

## 项目接入

```csharp
var provider = new LoadedPrefabViewProvider(root, projectLoader,
    resource => resource.Key + "@" + resource.Version);
var resource = new ViewResource("Inventory", "2");
var preloadLifetime = new Lifetime();
await preloadLifetime.PreloadAsync(provider, resource, token);

// 资源已驻留，可以同步创建；页面准备是否同步仍由 Route/Presenter 决定。
IViewLease view = provider.Create(resource);
await preloadLifetime.DisposeAsync(); // 不会释放 view 仍使用的资源
await view.DisposeAsync();
await provider.DisposeAsync();
```

projectLoader 实现 IResourceLoader，负责资源库实际下载/读取和引用释放。必须显式提供 ViewResource → 资源库键的映射，包含项目所需的版本语义，不把版本静默丢弃。Unity 2022.3 主线程/同步上下文是本适配器的运行条件。

提供方在交出 Lease 前若回滚失败，使用 ResourceLoadException 携带实际清理结果（允许已完成且失败的 Task）；调用方等待 CleanupCompletion，失败时保留未归还的资源占用。普通 AggregateException 无法表达这一所有权状态。

LoadedPrefabViewProvider 会复用当前已驻留资源项，在预加载和每个实例之间独立计数；最后一份持有释放时调用底层资源 Lease。没有驻留项时同步入口返回 RequiresPreload，不阻塞异步加载。异步入口加载后实例化；每个 await 后检查取消，迟到 Lease 由接收者回收。

预加载期间不创建 View，只校验加载的 Prefab 含根 View。实例化使用与常驻 PrefabViewProvider 共用的 PrefabViewFactory，先隐藏初始化再挂载。释放实例先执行 View 清理和 Unity Destroy，等待 Unity 对象实际消失后再释放 Prefab 资源，避免在延迟销毁期间卸载依赖。构造失败时同样保留资源直到失败实例被销毁。

并发未完成的加载默认最多 16 个，可配置；满额会拒绝额外加载。已有驻留资源的创建不占用加载额度。当前不会合并同时开始的相同资源请求；底层加载器应提供共享资源的独立 Lease。

Provider Dispose 拒绝新工作、取消并等待在途加载、释放 staging 节点。已交付的 View/Preload Lease 仍归各自调用者，必须自行释放；它们的释放在 Provider 关闭后仍有效。Provider 借用 projectLoader，不销毁项目加载服务。释放失败被报告，不伪造资源已成功回收。

Lifetime.PreloadAsync 在结果返回前登记所有权；父取消后迟到的预加载持有会释放。返回的 Lease 可以提前 Dispose（幂等），但调用者不能把同一所有权转交给另一个 Lifetime。

## Navigator 托管预加载

```csharp
var navigator = new Navigator(provider, preloadCapacity: 32);
var result = await navigator.PreloadAsync(inventoryRoute, token);
await navigator.ClearPreloadsAsync();
await navigator.ShutdownAsync();
```

Navigator 按完整 ViewResource（key/version）共享预加载工作和驻留 Lease，不创建 VM/Presenter，不进入页面历史。原始请求的 token 可以取消该工作，并及时返回 Cancelled；后端忽略取消时，其工作和容量仍由 Navigator 持有，直到迟到凭证实际回收。重复调用只有独立等待权，取消返回 WaitCancelled。不同 Route 使用同一资源可共享预加载，但 Route 自身的不可变定义注册规则保持不变。

PreloadOutcome 包括 Ready、Unsupported、CapacityExceeded、Cancelled、Superseded、WaitCancelled、HostClosed、Reentrant、Failed。只有实现 IPreloadViewProvider 的 Provider 支持此入口。Ready 表示持有资源，不代表该 Route 能同步打开。

容量默认 32 个资源预留，包括加载中、驻留中和退役中。ClearPreloadsAsync 立即更换批次并取消旧工作，等待旧请求与 Lease 回收；旧结果不得加入新批次。清理期间可以接受新预加载，但旧批次在实际回收前仍占额度。重复清理不会取消已经进入另一个调用者的清理任务。

未完成的 Clear 由内部 Lifetime 跟踪；Shutdown 取消预加载、同时开始活动 UI 关闭，并最终等待所有预加载清理。资源释放失败保留额度并在清理/退出中报告，不假装回收成功。页面 ViewLease 的独立持有保证清理预加载不会提前卸载存活实例。

成功预加载当前一直持有到 ClearPreloadsAsync/Shutdown，不在每次 Open 后自动淘汰。页面实例缓存与自动 LRU 由 Navigator 单独负责，不由预加载批次管理；资源字节预算、共享加载和物理卸载由项目资源系统负责。Provider/生命周期回调不能 await 自己的预加载清理，入口明确拒绝这种重入。

## 验证与当前边界

Navigation 示例使用模拟异步加载器验证：RequiresPreload → Available；预加载一次后同步创建；释放 preload 时 nativeAlive=True、releases=0；View Destroy 完成后 nativeDestroyed=True、releases=1；取消后的迟到加载也回收，最终 loads=2/releases=2。Unity Play mode 正常结束。

示例使用场景模板和计数释放，并不证明 Addressables/YooAsset 等真实资源系统的卸载行为。具体资源库适配和故障运行验收仍需项目验证；Navigator 的页面实例缓存清理和预加载请求合并已有独立实现，但不代替项目资源系统的共享计数与预算。

停止帧推进时，Unity 延迟销毁可能使释放继续等待；不能提前把仍存在的对象当作已回收。宿主应先等待 UI 清理，再停止帧驱动/释放底层资源系统。同步构造失败的异步回收由 Provider 观察并报告，调用者不能将同步异常等同于物理回收完成。

Navigator 的 Unity 示例验证：重复等待 WaitCancelled；容量满 CapacityExceeded；预加载 Ready/reservations=1；Clear 后 reservations=0；旧批次迟到 Superseded 且退役时仍限额；Shutdown 后 loads=5/releases=5。


## 完整同步视图资源契约

`ISynchronousViewProvider` 独立于 IViewProvider，提供能力查询和同步 Create；返回 `ISynchronousViewLease`，通过 Dispose 同步归还。自定义同步提供方不需要实现 CreateAsync 或 DisposeAsync。`SynchronousViewLease` 用同一个同步回调兼容现有 IViewLease 管线，仍保证恰好一次释放。

当前 `PrefabViewProvider` 和 `BorrowedViewProvider` 同时实现两类提供方接口。需要编译期约束同步归还时，以 ISynchronousViewProvider 类型调用：

```csharp
ISynchronousViewProvider synchronous = residentPrefabProvider;
using (ISynchronousViewLease lease = synchronous.Create(resource))
{
    // 此处只持有隐藏 View，不会自动创建 Presenter 或激活绑定。
    // 如果另行激活，必须先结束 Presenter、绑定和全部子视图，再退出作用域。
}
```

`ContentViewProvider` 在底层明确实现同步视图契约时开放同步入口，复用异步路径的隐藏挂载、排序限制与内容代际检查。挂载失败同步归还候选，保留原始错误及清理错误；底层仅支持异步释放时，能力查询返回 Unsupported，不能调用异步路径试探。挂载完成后再次检查宿主、视图及父节点，防止原生回调使候选失效后仍交付。

常驻 Prefab 的同步释放清理 View 并请求 Unity Destroy，不等待原生对象物理消失，也不卸载共享 Prefab。LoadedPrefabViewProvider 仍可能需要等待实例实际销毁后释放底层资源，所以不实现完整同步视图契约；预加载后的同步 Create 不改变这一点。

纯同步导航、ChildViewScope、绑定及 Tab 生命周期已有独立实现；其完整运行验收仍以实现状态文档为准。ViewLease 不能代替宿主排空业务工作。纯同步宿主在激活前拒绝异步组件，不能等到 Dispose 时再补救。
