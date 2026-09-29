# 异步 Tab

当前已实现默认 ReleaseOnLeave 的 Tab 流程：稳定键选择、隐藏准备、延迟加载提示、错误与重试、重复请求独立等待、有界快速切换、父关闭清理。基础离开守卫已实现；KeepPrevious、RestorePrevious 与有界实例缓存已接入，不将基础流程视为完整设计完成。

## 分层与接入

- `MUI.Tabs`：纯托管模块，依赖 Core/ChildViews，包含 TabContentController、TabContentDefinition、TabViewModel、TabSnapshot 和选择结果。
- `TabBarElement`：按钮按 TabKey 映射、选中标记、可用状态、键盘/手柄横向导航；加载不禁用 TabBar。
- `AsyncContentElement`：局部 LoadingOverlay、ErrorOverlay、Retry，以及内容 Provider 包装。
- `ContentViewProvider`：在隐藏时挂载子 View，并拒绝能越过父内容排序的 overrideSorting Canvas。内容区自身及预制子节点同样不能启用 Override Sorting；编辑器校验与运行时初始化都会检查同级 TabBar 是否排在内容区之后。

```text
MainView [View]
├── ContentRegion [AsyncContentElement]
│   ├── ContentHost
│   ├── LoadingOverlay
│   ├── ErrorOverlay [Text]
│   └── Retry [Button]
└── TabBar [TabBarElement]       最后一个兄弟节点，显示在内容上方
    ├── inventory [Button]
    │   └── Selected
    └── quests [Button]
        └── Selected
```

Element 支持序列化引用；未配置时按上述约定查找子节点。TabBar 可从直接子 Button 名称发现稳定键，或者在 Inspector 显式配置。Selected 必须为 Button 的子节点，不能是 Button 本身。错误提示默认显示用户可读的通用文字，异常详情保留在 Snapshot.Error，不直接展示实现细节。

```csharp
var mounted = contentElement.CreateProvider(projectViewProvider);
var controller = new TabContentController(parentView.ChildViews, new[]
{
    new TabContentDefinition("inventory", "Inventory", async (scope, token) =>
        await scope.PrepareAsync(template, mounted, args, cancellationToken: token))
}, loadingIndicatorDelay: TimeSpan.FromMilliseconds(80));

contentElement.Bind(controller);
tabBarElement.Bind(controller);
var result = await controller.SelectAsync("inventory");
```

控制器和 Slot 由父激活自动拥有；父激活必须已建立。手工宿主在显示前负责 CommitChildActivation，导航宿主自动执行。若默认 Tab 是首帧必需内容，在父的异步准备钩子中 await SelectAsync；当前不自动把一个手工创建控制器的默认选择加入父准备集合。

ContentDefinition 的准备委托返回同一 Scope 的新 Prepared Handle，不能自行 Commit。ChildViewTemplate 可以使用自定义 Presenter，VM 工厂创建的模型由投影拥有。业务直接访问只读 ViewModel.Snapshot；选择必须经过 Controller，不能通过写多个 bool 驱动内部状态。

## 状态与结果

Snapshot 原子包含 SelectedTab、DisplayedTab、Phase、Error、RequestVersion、LoadingIndicatorVisible。Phase 为 Empty/Loading/Ready/Error/Inactive；选中意图和已显示内容分别表达。候选提交时在同一同步调用中发布 Ready，过期请求不能修改新请求的提示。

默认 LoadingPlaceholder；可选择 Blank 或 KeepPrevious，后两者不显示加载字样。加载指示器可延迟出现，延迟任务随请求取消。LoadingPlaceholder/Blank 切换时隐藏并关闭旧激活；KeepPrevious 停用并冻结旧内容，提交新内容时释放。旧清理完成前不复用其资源。TabBar 为兄弟区域，不随内容销毁或因局部加载禁用。

异步模式配置非零加载提示延迟时，创建控制器的线程必须有可将计时续体送回所属 UI 线程的 `SynchronizationContext`；没有上下文的纯托管宿主应使用零延迟、Blank 或 KeepPrevious。计时结束后控制器再次检查线程，错误上下文不会在后台线程发布界面状态，而是报告诊断。纯同步模式不启动延迟任务。

相同 Loading Tab 共用工作，重复调用者的取消只返回 WaitCancelled。相同 Ready Tab 直接返回 Ready；Error 状态需 RetryAsync 或 forceReload=true。未知或禁用 Tab 返回 Rejected，不改变当前选择或取消有效请求。可用性在选择时重新验证，也可调用 RefreshAvailability 刷新按钮；当前刷新不会自动移除已显示内容。

新选择淘汰旧请求，旧结果 Superseded。父关闭返回 ParentInactive 并发布 Inactive；调用者在提交前取消已受理的选择时返回 Cancelled。仍有稳定旧内容时重新建立其激活，并将 SelectedTab/DisplayedTab 一起恢复为旧键；否则清空两个键并进入 Empty。恢复失败时发布 Error，结果附恢复错误，且不递归恢复。取消不触发 SelectionFailed；恢复后 Cancelled 也不代表内容为空。恢复令牌独立于已取消目标的令牌，但仍归父激活所有，并由后续选择撤销；恢复期间的加载提示使用有效恢复令牌，迟到计时器不修改新状态。加载失败发布 Error，Retry 使用新版本重试。同一控制器的准备回调、状态通知、当前子命令中同步重入选择被明确拒绝，避免状态提交和自身回收等待冲突；TabBar 的独立按钮请求不受此限制。

容量沿用 ChildViewSlot 的一个在途准备/回收加一个最新待启动请求。不合作 I/O 可以拖延新内容准备和物理清理，但不会无限创建更多候选。选择结果与物理清理完成不同；最终 Dispose 会等待清理并汇总错误。

## 运行时目录更新

`UpdateDefinitionsAsync(definitions, preferredKey)` 在修改前完整验证定义、重复键和可用性。失败保留原目录与选择。接受更新后，同一不可变定义对象仍存在且可用时保留当前内容/请求；同键换成新的定义对象视为内容契约变更，需要重新准备。

当前项被移除或禁用时，先结束原请求与内容，再选择可用的 preferredKey，否则选输入顺序中的第一个可用项。没有有效项时发布 Empty 并返回 Empty。目录已经接受后，调用者取消仅结束自己的等待（WaitCancelled），不会撤销目录更新或取消必要回退。父关闭仍终止全部工作。

`ReconcileAvailabilityAsync(preferredKey)` 重新评估现有定义，必要时执行上述回退；原有 RefreshAvailability 只更新按钮可用性。Items 目录以新只读集合发布，回调不能同步重入目录更新。

TabBar 隐藏目录中已移除的预配置按钮，并在对应键重新加入时恢复显示；Label 文本跟随定义。已有焦点属于被移除/禁用的按钮时移到可用按钮，无有效按钮则清空焦点。新键可通过显式 buttonTemplate 自动生成按钮，详见下方；未提供模板时仍需预配置 Entry。

### 动态按钮模板

在 Inspector 指定 buttonTemplate，或调用 `tabBar.ConfigureButtonTemplate(templateButton)`。模板必须 inactive，不包含持久化点击事件，也不包含 MUI View/Element；Label Text 和 Selected 子节点用于文字、选中标记。模板由宿主拥有，不会被 TabBar 销毁。

目录出现无预配置 Entry 的键时创建按钮；显示顺序与横向导航顺序跟随 Items。几何位置交给父级 HorizontalLayoutGroup 等布局组件，模板本身不决定所有按钮的位置。移除时立即解绑运行时点击监听、隐藏实例并调用 Destroy；不会无限保留曾经出现的键。现有预配置按钮仍按目录隐藏/恢复，不销毁。

使用中的生成按钮未移除前不能替换模板，模板不能同时作为实际 Tab Entry。仅 Snapshot 的 Loading/Ready 变化不会重复生成或重排按钮；Items 目录更换时才同步生成列表。按钮池与特殊布局虚拟化未实现。

## 离开守卫

在来源 TabContentDefinition 上配置 `canLeaveAsync`：

```csharp
new TabContentDefinition("edit", "Edit", prepare,
    canLeaveAsync: async (leave, token) =>
        await projectDialogs.ConfirmDiscardAsync(leave.SourceModel, token));
```

这里的 projectDialogs 是项目注入的确认服务示意，可以使用 `MUI.Dialogs.DialogService` 或项目自己的确认服务。Tab 控制器只依赖 `canLeaveAsync` 委托，不自动创建或管理确认窗口。上下文提供 SourceKey、TargetKey 和借用的 SourceModel。守卫应响应取消，并在 await 后检查 token，不在取消后读取借用模型。

普通切换或 forceReload 在离开当前已显示内容前调用来源守卫；确认期间不修改 SelectedTab、DisplayedTab 或 RequestVersion，不关闭旧内容。false 返回 Rejected/Denied；守卫异常返回 Failed，原显示仍保留。目标有效性和可用性在批准后重新检查。

相同待确认目标共用工作，后续调用者取消仅返回 WaitCancelled。选择新目标时，旧调用立即 Superseded 并请求取消旧守卫；只有旧守卫返回后才启动最新待确认目标，不并发堆积确认窗口。选择当前 Tab 可撤销待确认切换。守卫不能同步重入自身 Controller，也不能 await 自己或父级清理。

父关闭取消确认并返回 ParentInactive，完全绕过用户离开许可。UpdateDefinitionsAsync 是结构性更新，同样会取消旧确认并按新目录强制协调；需要业务确认的删除操作应先由业务取得许可，再更新目录。守卫不占用 Navigator 队列。

当前默认允许确认期间原内容继续运行；项目确认窗口决定是否屏蔽输入。没有强制终止不合作的守卫：其取消结果及时返回，但物理清理仍等待回调收敛。DialogService 的串行确认服务已经提供，Tab 与确认服务的组合仍需完整运行验收。

## 验证范围

2026-09-28：Unity 2022.3.62f3 附带 Mono 的不落盘内存探针验证了取消后原凭证恢复、无旧页清空选择、恢复失败、恢复中选择 C、父关闭、重复等待取消、恢复延迟提示及失败恢复中取消。取消恢复没有重新申请旧页凭证，最终父清理恰好归还一次；恢复失败保留父清理错误。受影响工程 Release 编译和全仓格式检查通过，独立 Unity 项目的既有 Tabs Play Mode 批处理演示退出码 0。这些证据覆盖托管协议与现有默认演示，不代替取消恢复的原生输入、冻结画面或布局验收。

导入 Tabs 示例，通过 Tools → MUI → Samples → Open Tabs Scene 启动。Inventory 正常加载，Quests 第一次模拟失败，再点击 Retry 成功；Locked 不可选。开启 BlankWhileLoading 可体验加载留空。

Unity Play mode 自动演示已运行通过：重复等待 WaitCancelled；禁用项 Rejected/Disabled 且仍选中 inventory；Loading 时 indicator=True/barEnabled=True；失败 Error；重试 Ready；快速切换旧 Superseded、新 Ready；父关闭 ParentInactive/Inactive，最终清理完成。离线编译也已通过。动态目录示例进一步验证：相同定义不改变请求版本；重复键失败且保留 quests；移除当前项回退 inventory；空目录 Empty；恢复后 quests 按钮重新显示。动态模板示例进一步验证新增 Mail 按钮、移除后 destroyed=True，程序化选择焦点回到 inventory；真实键盘/手柄操作尚未人工验收。

仍需完成：KeepPrevious/RestorePrevious 的 Unity 运行验收、缓存的 Unity 运行验收、按钮池优化、准备隔离的完整运行验收、父缓存恢复、完整输入可达性与运行故障验收。当前关闭旧内容后可能等待其不合作任务而延迟新内容，不提供伪造的清理完成结果。

离开守卫 Unity 示例已验证：拒绝为 Rejected/Denied 且 inventory 和版本保持；重复等待取消为 WaitCancelled，原切换仍 Ready；确认期间改选返回旧 Superseded/最新 Ready；父关闭使确认返回 ParentInactive 并完成清理。


## KeepPrevious 的保留与重新激活

通过构造参数 `pendingDisplay: TabPendingDisplay.KeepPrevious` 启用。控制器最多记录一份稳定旧内容，资源仍由原 ChildViewSlot/Scope 持有。接受新选择时调用 RetainAndDeactivate：冻结绑定、取消激活工作、关闭输入与 Tick，保留只读旧画面。Loading 快照的 SelectedTab 为新目标，DisplayedTab 为旧键；没有旧内容时留空。新候选提交前撤销旧画面，提交后按 ReleaseOnLeave 清理旧资源。

A 显示后切到 B，B 尚未完成又选择 A 时，同一不可变定义且未强制重载的 A 可通过 PrepareReactivationAsync 排空旧激活并复用原实例。恢复准备时 DisplayedTab 置空，完成前保持隐藏；不会再申请同一实例的第二份资源所有权。forceReload 始终走定义工厂。快速切换仍受 Slot 的一个在途操作加一个最新待启动请求约束。

失败默认清除旧内容，保留失败目标的 SelectedTab 并显示错误与重试；当前请求取消后清除内容。清理回调后再次检查请求与父状态，不能覆盖父关闭发布的 Inactive。旧回调、保留内容及正在退役的子内容不能重入选择或目录更新；新 TabBar 请求仍可被接受。

目录更新会关闭已移除、禁用或更换定义的保留内容，同时保留仍有效的新目标请求。相同键不代表兼容实例，必须是同一 TabContentDefinition 对象。自定义渲染器需要支持 IVisualRetentionView，绑定需要支持 IFreezableBindingContext，否则切换失败并清理。

此策略可与下述 RestorePrevious 自动失败恢复组合；有界 Tab 缓存见下节；准备超时隔离已由 `ChildViewPreparationOptions` 提供。不合作任务可能阻塞恢复及后续加载；当前只完成源码检查和 Tabs 示例及依赖离线编译，Unity 视觉、连续切换、失败、取消和资源释放仍待运行验收。

父页面进入显示保留期间，TabBarElement 和 AsyncContentElement 暂缓对按钮、选中标记与加载/错误节点的渲染，结束保留后应用最新状态。


## RestorePrevious 失败恢复

通过构造参数 `failureDisplay: TabFailureDisplay.RestorePrevious` 启用，默认仍为 ErrorPlaceholder。该策略可与三种 PendingDisplay 组合：KeepPrevious 显示冻结旧画面，LoadingPlaceholder/Blank 隐藏旧画面但保留唯一旧实例供失败回退。

目标失败后通过同一个 Slot 排队恢复，等待失败候选清理后，排空旧激活并重新激活原实例。恢复期间继续使用原请求版本，准备前后及提交前复核父状态、保留对象身份、定义对象与可用性；新选择或父关闭会撤销旧恢复的发布权。恢复成功同时设置 SelectedTab 和 DisplayedTab 为旧键、Phase 为 Ready。原目标请求仍返回 Failed，不能将恢复显示误报为目标加载成功。

`SelectionFailed` 事件提供 FailedTab、RestoredTab、RequestVersion 和 Error，供项目显示独立提示；恢复成功时 RestoredTab 为旧键，否则为 null。观察者异常隔离，回调中不能重入选择或目录更新。事件不自动弹窗，也不建立额外导航依赖。

没有旧内容、定义失效或恢复失败时显示原目标的错误占位并支持 Retry；恢复异常与原失败合并，仅尝试恢复一次。恢复期间调用者取消会清空内容并返回 Cancelled；新请求取代返回 Superseded，父关闭返回 ParentInactive。自动恢复不会申请第二份旧实例所有权。仍不保证不合作任务在限定时间内排空；运行验收与超时隔离未完成。


## 准备结束与提交前的有效性检查

IsEnabled 是同步且可重复求值的可用性谓词，不应产生业务副作用。控制器在准备启动、异步准备返回和最终提交时复核请求、定义对象及可用性；Slot 还在绑定提交前与显示前执行验证。普通切换与失败恢复均使用该门控，加载期间已禁用或过期的内容不会正常提交显示。

已经接受的目标在准备期间变为禁用，按本次加载失败处理，可触发 RestorePrevious；定义替换或新请求导致版本失效时取消旧准备。准备返回后的校验失败会清理本次候选；工厂返回其他 Scope 的句柄或已活动句柄时只拒绝结果，不取得其销毁权。运行时目录变化仍建议通过 UpdateDefinitionsAsync/ReconcileAvailabilityAsync 表达，以便同步按钮和回退选择。

Tabs 示例的 Inspector 已提供 Keep Previous While Loading 和 Failure Display，保留原 Blank While Loading 配置。手工体验可关闭 Automatic Walkthrough，选择 Inventory 后切换首次必定失败的 Quests，观察保留画面或失败恢复。选项与示例已通过离线编译，尚未运行 Unity 视觉与输入验收。


## 有界实例缓存

默认 ReleaseOnLeave。通过构造参数 `cacheOptions` 显式启用：

```csharp
var cache = new TabCacheOptions(TabContentRetention.CacheRecent, capacity: 3);
var controller = new TabContentController(parentView.ChildViews, definitions,
    pendingDisplay: TabPendingDisplay.KeepPrevious,
    failureDisplay: TabFailureDisplay.RestorePrevious,
    cacheOptions: cache);
```

`CacheRecent` 按最近使用顺序保留实例，容量满时等待最久未使用项完成淘汰。`KeepSelectedTabs` 仅保留 `keptTabs` 指定的键，键必须非空且唯一，数量不得超过显式容量。禁用缓存只能使用容量 0，其他模式要求正容量。`CachedContentCount` 返回已停用并入缓存的实例数。

候选成功替换后，Slot 将旧稳定内容交给缓存协调器：先通过 DeactivateAsync 排空旧激活，进入 Inactive 后才能入缓存。VM、Presenter 和 ViewLease 保留原所有权，缓存命中通过 PrepareReactivationAsync 建立新激活，不再申请同一实例的第二份所有权。失败候选、显式清空及父关闭不进入缓存；接管失败由原 Slot 继续最终关闭。

缓存命中要求同一 TabContentDefinition 对象。资源版本、业务键或账号上下文变化时，项目必须提供新定义并更新目录，不应原地改变工厂闭包后继续使用旧定义。强制重载会清理对应缓存并调用工厂；当前页同键重载成功后，不会把被替换的旧实例缓存为同键备份。

目录更新与可用性刷新触发串行缓存失效清理，后续缓存读取与写入等待清理结束；连续更新只合并为一次复查，不累积任务队列。淘汰和缓存回调不能重入选择、目录修改或控制器销毁，避免等待自身。父关闭等待 Slot、在途工作及缓存清理，错误统一汇总。

容量只计算停用缓存项，另外最多持有当前/保留内容及 Slot 的有界在途候选/退役操作。只有清理完成才能复用或腾出容量供后续工作；不合作任务可能阻塞后续准备。当前缓存组合使用保留停用能力，渲染器和绑定须支持相应显示保留/冻结契约。停用缓存的估算字节预算见下节；资源后端的全局预算、超时策略及父页面缓存重开传播由项目决定，Unity 运行验收也未完成。


## 缓存估算字节预算

`TabContentDefinition.estimatedRetainedBytes` 表示该定义的一个停用实例仍持有的资源估算大小，默认 null 表示未知。`TabCacheOptions.maxEstimatedBytes` 可设置停用缓存总估算额度，必须为正数且仅用于启用缓存的模式。例如：

```csharp
var cache = new TabCacheOptions(TabContentRetention.CacheRecent,
    capacity: 3, maxEstimatedBytes: 64L * 1024 * 1024);
```

启用字节额度后，缺少估算或单项超过额度的实例不进入缓存，按离开释放处理；不会把未知大小当作零。容量与字节额度同时约束准入，需要腾出空间时依次淘汰最久未使用项，并等待释放完成。比较使用减法避免 long 合计溢出。

`CachedEstimatedBytes` 返回当前停用缓存的合计估算值；没有缓存时为 0，存在未知大小或合计无法用 long 表示时为 null。未配置字节额度时仍执行数量上限，允许未知大小实例缓存。估算必须覆盖项目希望纳入额度的 VM、视图及仍保留的图片/字体等资源，共享资源是否按实例重复计入由项目约定；资源变化时需更新定义使旧缓存失效。

该值不是实际 CPU/GPU 分配或进程 RSS/PSS，不会限制当前显示内容、在途候选或隔离资源。全局资源统计和预算联动由项目资源系统负责，不能据此声称全框架内存存在硬上限。


## 主动清理缓存

`await controller.ClearCacheAsync(cancellationToken)` 请求释放已有停用缓存，并等待实际释放完成，可由项目的低内存或场景管理流程调用。清理复用目录失效队列，一项失败仍继续后续项，最终以 AggregateException 返回错误；错误也保留在控制器的最终释放结果中。

同一清理期间的重复调用共享结果。开始前令牌已取消则不发起清理；接受请求后的取消只结束该调用者等待，不取消底层清理，也不影响其他等待者。父关闭仍等待相关工作收尾。关闭/销毁回调、准备回调、守卫与状态通知中拒绝等待清理，避免等待自身。

清理针对执行时已有的停用缓存，不关闭当前内容、不取消正在加载的新目标，也不永久禁用缓存；后续成功切换仍可能产生新缓存。账号或业务上下文变化还应替换内容定义并更新目录，不能仅清缓存后继续使用过期的活动界面。此入口已经离线编译，尚未完成 Unity 低内存/并发清理/释放失败运行验收。


## 准备超时与有界隔离

通过控制器构造参数配置准备等待期限，例如：

```csharp
preparationOptions: new ChildViewPreparationOptions(
    timeout: TimeSpan.FromSeconds(10), maxQuarantinedPreparations: 2)
```

未配置 timeout 时保留原有持续等待行为。启用后，超过期限的准备返回 Failed/TimeoutException，并发出合作取消；已被取消或取代但未退出的准备也进入隔离。隔离不释放仍在使用的 ViewLease，不允许迟到候选提交。迟到完成会关闭同 Scope 的 Prepared 句柄，等待物理清理后释放隔离名额；准备失败的清理任务也纳入等待。

`QuarantinedPreparationCount` 表示尚未完成准备或迟到清理的操作数量。达到 MaxQuarantinedPreparations 后，新准备失败并报告容量耗尽，直到旧任务结束后才允许重试。最多为隔离上限加一个正在准备的操作，另有一个最新待启动请求；未结束请求的取消源由隔离项继续持有，不提前销毁。

该超时只覆盖候选准备，不覆盖旧实例关闭、缓存淘汰或最终宿主清理。同一保留实例正在重新激活时，其后续关闭仍必须等待过渡退出，因此不承诺所有恢复场景都能立即继续。Slot/控制器最终 Dispose 仍等待隔离项完成，不伪造资源释放，也不能强制终止无限运行的第三方代码。

目前隔离预算按操作数量计算，与缓存估算字节额度分别约束不同对象；它不会也不应与项目资源系统的全局预算合并。导航级超时、清理超时与最终宿主隔离协议仍待实现。新增路径已离线编译，尚未进行 Unity 超时、迟到、父关闭与恢复运行验收。

## 示例配置与手动验收入口

`Samples~/Tabs/TabsDemo` 在 Inspector 暴露缓存容量、准备期限、隔离上限、模拟准备延迟及是否忽略准备中的取消。默认容量与期限均为 0，保持离开释放及不启用准备超时的行为。请在 Play 前配置；工厂捕获启动时的准备设置，不因运行中修改 Inspector 改变同一个定义的缓存兼容性。

关闭 Automatic Walkthrough 后，可通过组件上下文菜单 `Log Cache And Preparation State` 观察缓存及隔离数量，通过 `Clear Cached Tabs` 清理停用缓存。具体操作见 [Tabs 示例说明](../Samples~/Tabs/README.md)。模拟延迟 3000 毫秒、期限 0.2 秒、忽略取消及隔离上限 2 可观察期限失败与容量拒绝；该配置不会重试成功，需重新运行并调整期限来验证成功路径。

迟到结果已经进入 Closing/Closed/Failed 时，隔离清理等待其原有 CleanupCompletion；状态本身不证明资源已经释放。上述路径及示例当前只经过离线编译，Unity 手动验收尚未完成。

## 停用缓存过期时间

`TabCacheOptions` 可设置 `timeToLive: TimeSpan.FromSeconds(30)`，仅允许在启用缓存时提供正值；默认 null 不按时间过期。每次旧激活排空并实际进入缓存后重新开始计时，不计算正在显示及停用清理的耗时。计时使用单调时钟。

缓存命中时同步检查期限；过期项从缓存移除并等待关闭，再走普通工厂准备，绝不复活过期实例。强制重载、定义不兼容及非 Inactive 状态继续走同一释放路径。现有目录失效清理也检查过期。创建控制器时若存在 UI 同步上下文，启用 TTL 后自动启动每秒一次的扫描；单个控制器最多一条循环，慢速清理期间等待同一维护任务，不积压计时请求。父级取消或控制器销毁结束扫描，已经开始的释放仍完整收尾。

没有同步上下文的纯托管宿主应从所属线程周期调用 `RefreshCache()`；该方法只请求维护，不等待释放。回调重入或维护忙碌时跳过本轮，由下次调用复查。同步上下文必须能把延迟恢复调度回所属 UI 线程；错误上下文会触发线程诊断并结束自动扫描。命中时的期限检查始终生效，不依赖扫描精度。数量和估算字节诊断包括尚未移交清理的实际缓存项，过期不意味着当场完成物理释放。

Tabs 示例的 Cache Time To Live Seconds 为 0 时不设期限，正值要求 Cache Capacity 大于 0。该路径只经过离线编译，真实等待、命中重建及释放失败仍需 Unity 验收。

缓存复用的清理责任从移出缓存时即转交给本次准备。即使 `PrepareReactivationAsync` 在入口校验或操作登记前失败，控制器也关闭该实例并等待清理；不能假设 Scope 内部回滚已经启动。内部回滚与外部兜底共用同一个关闭任务，准备与清理分别失败时保留两者错误。


## 资源与绑定版本失效

Tab 缓存现在在准入、停用排空后、每次淘汰等待后、命中恢复前检查 ChildViewHandle.IsContentCurrent。资源提供方更换 ContentVersion 或 BindingRegistry.Reset 后，旧缓存只关闭回收，不再恢复为新内容；下一次选择走新建流程。真正的子视图恢复和提交仍再次校验，覆盖预检查之后发生的失效。版本读取异常通过 UIErrors 报告，并保守拒绝复用。

只要启用缓存且有 UI 同步上下文，每个控制器最多一条每秒维护循环，即使没有 TTL 也检查资源代际。资源变更与缓存时间顺序无关，因此扫描整个有界目录，不能仅检查最老项。无同步上下文的宿主周期调用 RefreshCache；命中检查不依赖扫描是否已经执行。维护仍等待上一批完成，不累积清理队列。

当前已稳定显示的 Tab 不因提供方换代自动消失；离开后不入缓存，再次选择或显式 Reload 时使用新内容。准备中换代则失败/取消并回收，KeepPrevious/RestorePrevious 仍服从现有失败策略，不能恢复版本已失效的旧物理实例。项目账号切换或必须立即撤下旧业务内容时仍需显式协调关闭，资源代际不是业务权限协议。


## 参与项目清理流程

项目需要释放闲置页签时显式调用 tabs.ClearCacheAsync()，纯同步模式调用 tabs.ClearCache()。控制器只操作自身缓存，不登记全局回收参与者，不监听平台内存事件，也不回收项目资源池。

清理是显式的，不通过反射或全局静态表寻找 Tab。控制器只负责自身缓存，项目可以把 `ClearCacheAsync` 接入场景切换或平台内存策略。离线编译通过，实际平台通知、多宿主共享及故障运行验收尚未执行。


## 完全同步模式

父 Scope 的 LifetimeMode 为 Synchronous 时，使用 `TabContentDefinition.CreateSynchronous` 定义同步内容工厂和可选 `canLeave` 守卫，再构造同一个 TabContentController。同步控制器只接收同步定义，默认异步控制器只接收原有异步定义；定义模式不匹配在登记时拒绝，不能以某次 ValueTask 已完成来推断同步能力。

```csharp
var definition = TabContentDefinition.CreateSynchronous(
    "inventory", "背包",
    scope => scope.PrepareSynchronous(template, provider, Unit.Value),
    canLeave: context => !hasUnsavedChanges);
var tabs = new TabContentController(parentScope, new[] { definition });
var result = tabs.Select("inventory");
```

其中 template 必须声明 supportsSynchronousLifecycle，provider 实现独立 ISynchronousViewProvider；模型、Presenter、绑定、子树及资源归还均受同步能力约束。控制器与槽由父 Scope 对应的 Lifetime 托管，也可直接 Dispose。

同步入口为 Select、Retry、UpdateDefinitions、ReconcileAvailability、ClearCache 和 Dispose；RefreshAvailability/RefreshCache 根据父模式直接执行。SelectAsync、RetryAsync、UpdateDefinitionsAsync、ReconcileAvailabilityAsync、ClearCacheAsync 在纯同步控制器中拒绝；DisposeAsync 仅作为共同所有权接口转调 Dispose 并返回完成信号，不调度异步释放。TabBar 和 AsyncContentElement 的点击/重试入口自动选择同步方法；CreateSynchronousProvider 支持只有同步接口的提供方，并保留内容区的 Canvas 排序限制。

同步选择直接执行守卫、隐藏候选准备、提交和旧内容退役。不发布在途 Loading，也不启动延迟指示器；PendingDisplay 只对异步等待有意义。RestorePrevious 在候选失败时维持原来仍活动的同一实例，恢复选中与显示身份，不重新运行旧页 OnOpen。原请求仍返回 Failed 并发出 SelectionFailed。ErrorPlaceholder 则同步清空旧页并发布错误状态。若清理失败而无法清空，DisplayedTab 如实保留仍显示的内容，不能伪报空白。

缓存共用原有目录、定义身份、数量/估算字节额度、资源代际和 TTL 规则。提交新页后同步停用旧页，只有成功进入 Inactive 的实例才进入缓存；命中通过 PrepareReactivation 同步复用原 View/VM/Presenter，强制重载则释放缓存并新建。淘汰与 ClearCache 逐项同步 Dispose，失败继续回收其他项并汇总；历史清理失败会阻止继续选择，最终 Dispose 保留失败结果。Ready 且 Error 非空表示内容提交成功但旧内容清理失败，业务必须检查 Error，不能只看 Status。

TTL 不启动 Task.Delay 或维护任务。项目从帧循环调用 RefreshCache；选择、可用性刷新和目录更新也会复查缓存，未主动维护时命中仍检查过期。纯同步项目在 UI 线程显式调用 ClearCache；平台内存事件及跨宿主回收顺序由项目协调。

同步操作在父 Scope 和内部同步 Lifetime 登记，守卫、工厂、状态通知及清理回调中拒绝重入选择/更新/销毁，避免边使用资源边释放。关闭返回时全部清理尝试已经完成；Unity 原生 Destroy 仍遵循帧末销毁语义。同步目录更新沿用原有目录替换规则：完整验证后接受，失效选择不经过离开守卫，清空后选择回退项。

新增 SynchronousTabsDemo 展示背包/任务切换、同步离开守卫、失败保留、缓存复用、帧驱动 TTL 与同步目录更新。当前仅通过离线编译，Unity 视觉、输入、异常回调与缓存生命周期尚未运行验收。

## 控件换绑与请求归属

`TabBarElement.Bind(controller)` 与 `AsyncContentElement.Bind(controller)` 同时设置控制器和它的 ViewModel。直接将 ViewModel 改成另一个模型或 null 时，控件会解除不匹配的旧控制器；后续选择和重试改为发送 SelectionRequested / RetryRequested，由项目接线。赋回同一个控制器的模型仍保留直连关系；更换控制器时应分别对两个控件调用 Bind，不能只换显示模型。

解除这里只撤销控件对控制器的调用关系，不取消或释放旧控制器；控制器仍由原有生命周期拥有。没有请求事件订阅时，单独绑定模型只展示状态，不会自行发起切换。

`AsyncContentElement` 更新加载遮罩、错误文字和重试按钮时，会在原生启停及文字更新后检查本轮渲染是否仍有效。如果回调中换绑、发布新状态、释放控件或进入画面保留状态，旧渲染停止后续写入；错误文字组件单独被销毁时也不会继续访问它。这些检查不创建任务，同步与异步控制器共用同一表现路径。

`TabBarElement` 同样在按钮启停、文字、可交互状态、选中标记及动态条目层级更新后检查渲染有效性。动态按钮移除时先解除集合归属和监听，再执行原生停用与销毁。左右导航跳过隐藏或不可交互的按钮；焦点失效时选择可用按钮，但不会覆盖回调中已经选中的其他对象。渲染中的嵌套更新只标记待刷新，由下一次有效 LateUpdate 处理，避免递归进入按钮创建；普通更新仍立即执行，不创建 Task。新建按钮先登记清理归属，下一轮再根据最新模型复用或移除；层级排序只移动尚未就位的直接子按钮。原生回调与实际焦点行为仍需 Unity 运行验收。
