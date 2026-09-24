# Navigation sample

1. Import **Navigation** from the package samples.
2. Choose **Tools → MUI → Samples → Open Navigation Scene** and enter Play mode.
3. The sample opens one page synchronously and another with asynchronous preparation. Confirm returns the typed result 42; Close dismisses the front page.

Enable Automatic Walkthrough for a guided sequence. The batch entry point `MUI.Samples.Navigation.Editor.NavigationSampleMenu.RunPreviewBatch` enables it automatically in a disposable project.

The walkthrough logs hook order, refusal to synchronously open an async-only route, lifecycle reentrancy rejection, typed completion, duplicate close, Back/history, cancelled preparation, cancelled close waiting and host shutdown. It uses an inactive scene template as the resident Prefab source, so no external art or resource package is required.

The first page also contains a ThingItem child View with generated bindings. The sample prepares it hidden, commits it, preserves its local hidden intent across parent visibility changes, releases the borrowed childView without destroying its node, then reuses that node with an asynchronous Presenter. Closing the parent closes the child.

A second item uses NestedViewElement and a generated binding from PageViewModel.Item. The walkthrough replaces its model, clears it with null, and binds it again. Its wrapper is a binding boundary, so the two items can each contain an ItemLabel without causing parent lookup ambiguity. The Tab module is not implemented yet.

This sample uses the legacy EventSystem input module. Use the legacy input backend (or Both), or supply a compatible EventSystem. Current navigation has no modal or transition policy; both belong to subsequent implementation stages.

A DynamicViewElement shows Source and VM as separate inputs. Its sample provider simulates I/O that completes after cancellation. The walkthrough replaces that slow request, keeps an unbound instance when VM is null, rebinds it, and clears Source. Provider configuration happens explicitly in the page binding factory.

The preload walkthrough uses DemoPrefabLoader to show independent preload/view claims, release after Unity destruction, and cleanup of successful late loads after cancellation. The loader borrows a scene template; this is not validation of a third-party resource library.

Navigator preload walkthrough covers shared-resource wait cancellation, capacity limits, clear-before-load-completes invalidation, and shutdown release accounting. Its preload-only model factory deliberately throws if invoked, demonstrating that preloading does not create business instances.


Enable **Show Enter Transition Preview** on NavigationDemo to display a dedicated page before the regular demonstration. It fades in over 0.8 seconds with controls gated, waits for the shared readiness result, stays visible for 1.2 seconds, then closes. The route configures only its own View through its binding factory, so other sample routes retain their original timings. Cancelling/destroying the demo closes the preview through its finally block. This is an opt-in visual example; it has compiled but has not been run in Unity.


虚拟列表流程新增显式变高示例：1000 项在 30、50、70 高度间交替，定位第 500 项，再把第 0 项由 30 改为 130。日志记录首个可见索引是否保留及滚动偏移增量（预期约 100），随后切换三列网格，再恢复原列表。该新增流程只完成离线编译，尚未在 Unity 中运行；不包含文本自动测量。


变高示例同时混排默认模板与棕色 featured 模板，并把键 500 从 featured 切回默认模板，保留同一个 ViewModel。列表边界现在包含两个未激活模板，原生条目数量日志已扣除这两个模板。新增混排与模板切换尚未运行验收。


分页流程现包含尾部追加和头部插入：先演示分页失败重试及页尾预取，再加载键 80..99，定位 90，向头部插入 60..79。`MUI Paging prepend` 日志记录数量、首键及阅读锚点是否保持。该新增流程仅编译通过，未在 Unity 运行。


## 自动高度测量预览

打开 Navigation Scene，在 Play 前启用 NavigationDemo 的 **Show Measured List Preview**。此选项进入独立的测量预览分支，保留页面供手动滚动，不继续普通导航演示。请关闭 Show Enter Transition Preview，便于直接观察列表。

预览使用 2000 条不同长度的文本、默认与 featured 两种模板。根 VerticalLayoutGroup 根据换行 Text 计算首选高度；选择覆盖层通过 LayoutElement.ignoreLayout 排除，不使用 ContentSizeFitter 争夺根尺寸。列表每帧最多测量 2 项，初始行高 40 仅作估算。

流程依次定位键 1500 并聚焦、把视口宽度改为 140、把模板与当前单元字号改为 24 并显式失效高度缓存、更新键 1500 的文本并发布集合 Update。Console 的 `MUI Measured list` 日志分别记录定位结果、首个可见索引和物化数量。任一步定位未就绪则停止后续演示，保留现场。随后可手动滚动观察长文本、模板混排及单元复用。

预期关注点：目标应在测量修正后保持可见；短文本与长文本应有不同高度；物化数量主要随视口变化；失效前的高度不能在宽度或字号变化后继续使用。此流程仅完成离线编译，未在 Unity 中运行，日志与预期不代表已经通过验收。


## 顶层页面缓存演示

Play 前启用 NavigationDemo 的 **Show Cache Walkthrough**，建议关闭两个 Preview 选项。示例先异步打开页面并关闭入缓存，再同步用新参数打开同一路由。观察日志：模型和 View 引用相同，Handle 不同，Selection 从 10 变为 20；关闭旧句柄不会关闭新激活。第二次关闭后等待 ClearCacheAsync，缓存数量应为 0，并出现最终 OnDestroy。OnCreate 应仅调用一次，OnOpen/OnClose 应各两次。CachedPagePresenter 显式重置每次激活的子模型、列表与选择，动态内容源设为 null。

缓存仅保留顶层实例内容，初次异步准备仍可能涉及子视图；重新打开会重建子激活。示例未在 Unity 中运行，上述为人工验收预期，不是通过记录。


缓存演示开启时 UIHost 使用容量 2。基础复开流程后增加两段：Timed 路由保留 100ms，等待 200ms 后应创建新模型；随后按 A、B、A、C 顺序打开并关闭 KeepAlive 路由，预期 B 被淘汰，A 仍能复用。日志同时记录目录数量与待释放数量，最后等待 ClearCacheAsync。过期是否已被扫描移出不影响命中拒绝；上述流程仍仅完成离线编译，未取得实际运行结果。


同一缓存演示配置 2 MiB 估算预算：普通路由声明每实例 1 MiB；字节段先缓存 1 MiB 条目，再关闭 1.5 MiB 条目，即使数量上限 2 尚未用满也需要淘汰旧内容。未知大小和 3 MiB 条目均不进入缓存。若旧内容释放立即成功，预期目录保持 1、预留为 1572864 字节；如释放异步未结束，新项可能拒绝入缓存，待释放字节不会提前扣减。最后成功清空应预留为 0；任何释放失败都保留相应失败额度并报告异常。数字仅展示预算机制，未测量实际内存。


缓存演示也包含代际失效：页面打开时调用 InvalidateCacheAsync，页面应仍为 Open；随后关闭旧页，缓存应为 0。再次打开同一路由应创建不同的 VM，关闭新页后可以重新缓存。此段验证预期用于区分“清理目录”和“禁止旧内容以后回到缓存”；目前没有 Unity 运行结果。


资源预加载段新增 LoadedPrefabViewProvider.Invalidate 演示：已预加载资源换代后，再次请求不能复用旧 Ready；容量被旧批释放占用时可能暂时返回 CapacityExceeded。随后在 50ms 演示加载尚未结束时换代，旧结果应取消或被取代并释放，不发布为新版本 Ready。观察加载/释放计数与状态日志，最终 Shutdown 等待所有已接管资源清理。这仍是待运行的示例流程。


## 共享资源加载预算演示



## 显式清理预加载与缓存

MUI 不监听平台低内存事件，也不提供全局资源预算或自动回收服务。项目可以在自己的场景切换、低内存或资源策略流程中显式调用 `navigator.ClearPreloadsAsync()`、`navigator.ClearCacheAsync()` 或 `navigator.InvalidateCacheAsync()`；这些入口只清理 Navigator 自己持有的预加载和停用 UI 内容，不改变活动页面，也不替项目资源后端卸载共享资源。

示例中的清理日志仅用于观察导航内部计数。平台低内存事件、实际内存下降、共享资源计数和物理卸载必须由项目资源系统自行验证。

## 分页滑动窗口演示

分页流程末尾分别演示 Append 与 Prepend 的有界窗口，页大小 20、容量 30，读取三页。观察 `MUI Paging window` 日志：保留数为 20、30、30，后两页移出数为 10、20；追加最终保留键 30..59，前插最终保留键 40..69。日志同时记录首个可见键，便于检查原锚点保留及淘汰后的回退；本段关闭自动预取。条目模型只借用，窗口不销毁业务模型。当前仅离线编译，尚未执行 Unity 运行验收。

## 分页查询重置演示

有界窗口演示后运行 NavigationDemo.PagingReset：旧加载故意等候受控信号并忽略取消，随后连续请求中间查询与最新查询。放行旧页后，预期 `MUI Paging reset` 输出 old=Superseded、first=Superseded、latest=Applied、during=Resetting、new=Loaded，middleLoads=0、latestLoads=1、count=3、firstKey=100。同一来源不需要重新赋值给列表；新页必须等待旧加载排空后才开始，旧查询错误由查询身份拦截。演示 finally 始终放行旧页，避免演示失败阻塞生命周期清理。仅离线编译，尚未在 Unity 执行。

## 分组虚拟列表演示

分页演示后运行 NavigationDemo.GroupedList，使用同一 VirtualListElement：100 组、每组 25 条正文，加标题共 2600 行。标题借用 featured 模板并使用 50 高度，正文使用默认模板。定位并选中 251 后折叠组 0，预期剩 2575 行且阅读锚点保持；折叠组 10 后剩 2550 行，选择清除，定位隐藏正文返回未找到。反转分组并 Reset 后首组为 99，组 0/10 继续折叠；展开组 10 后重新定位 251，全部折叠后仅剩 100 行标题，TotalItemCount 仍为 2600。日志记录物化数量，可与视口大小核对。源码与离线编译已检查，以上运行预期尚未在 Unity 验收。

分组列表的 `SetAllExpanded` 同样用一次增量事务更新变化的标题及正文，保留其余行的动态高度测量；空分组只发布标题 Update。结构 `Reset` 仍按整体替换处理。

## 树形虚拟列表演示

`TreeList.SetExpanded`、`ExpandAncestors`、`SetAllExpanded` 通过一次批量 Add/Remove/Update 通知提交展开变化，不对整棵树发 Reset。展开状态改变且仍可见的节点收到 Update，插入或移除的可见后代按连续区间合并；未变化的可见行可保留动态高度测量。只改变隐藏节点的展开状态时，可见集合不通知、Version 不递增，重新展开祖先时采用已保存状态。结构 Reset 仍发布 Reset 并重新测量。此优化保留现有全量快照及可见索引重建，不承诺展开操作与总节点数无关。

分组流程后运行 NavigationDemo.TreeList：20 个根、每根 5 个分支、每分支 20 个叶子，共 2120 节点，初始显示 120 行。日志记录隐藏叶子 leaf:7:2:13 的深度与可见索引，随后展开祖先并聚焦；折叠 root:0 检查锚点，折叠 root:7 检查选择清除与 branch:7:2 的展开状态保留。反转全部输入并 Reset 后首根应为 root:19，root:0 仍折叠；全部折叠时显示 20 行，恢复目标路径后为 45 行。根使用 featured 模板，本例只用文本缩进展示深度，不创建嵌套布局容器。离线编译通过，以上仍为待执行的 Unity 验收预期。

虚拟网格仍使用同一个 VirtualListElement，配置 columnCount 或运行时 Columns。既有流程对比 100/10000 项的三列物化量，并演示变高网格及切回列表；仅支持纵向等宽列，横向网格、瀑布流与跨列标题尚未实现。

## 显式参数更新演示

开启 NavigationDemo 的 **Show Args Update Walkthrough**，在常规导航流程前运行独立示例。成功更新到 Updated/20 后，使用新参数 Open 应复用原 Handle，OpenCount 为 1；Prepare failure 预期 PreparationFailed，Rollback 在修改标题后抛错，预期 CommitFailed 且上下文和模型恢复为 Updated/20。最后准备一个忽略取消的 Slow 候选并请求强制关闭，放行候选后预期更新 CancelledBeforeCommit，候选释放数为 3，OnClose 在该清理完成后执行。准备与提交失败会通过 UIErrors 输出预期诊断。此示例默认关闭、只做离线编译验证，尚未取得 Unity 运行结果。

Show Args Update Walkthrough 也启用 ThingItem 子视图演示：UpdateArgsAsync("Iron") 后 Args 与 Label 更新而 OpenCount 不变；停用再恢复才调用第二次 OnOpen，并继续使用 Iron 参数。Slow 候选准备期间第二个更新返回 Busy，随后停用取消更新、放行迟到候选，预期 CancelledBeforeCommit、Inactive、CandidatesDisposed=2、ClosedDuringArgsUpdate=false；最后恢复该子项以继续父关闭演示。父子更新现在共用 Core 的 ArgsUpdateOutcome，清理枚举为 ArgsUpdateCleanup。仍仅有离线编译证据。

## ViewModel 换绑演示

开启默认关闭的 **Show Rebind Walkthrough**。拒绝模型会在 Presenter 已撤销旧订阅后抛错，预期 Failed、旧模型恢复、原模型释放数为 0；成功换绑后，同句柄查询得到新模型，OpenCount=1，分别修改旧/新模型时 Presenter 通知增量为 1，原工厂模型释放数为 1。关闭后的借用模型释放数仍为 0，由示例调用方随后释放。

第二个页面的原模型使用受控释放信号。实际进入模型释放后才强制关闭，预期换绑与关闭均尚未完成；放行后换绑 Applied、关闭 Closed，原模型只释放一次。ThingItem 还演示换绑到新道具模型并恢复原借用模型，OpenCount 不变。失败恢复会输出预期诊断。以上为运行预期，仅通过离线编译，尚未取得 Unity 实测结果。

NavigationDemo.Diagnostics 订阅 LifecycleChanged。开启对应演示后，ArgsUpdateFinished/ViewModelRebindFinished 会输出状态、清理状态、恢复失败标志、事件序号与最新提交版本。它们代表操作及清理已结束，可能晚于同句柄的关闭事件，不能当作页面仍活动的通知。

Show Args Update Walkthrough 末尾增加参数清理错误演示：第一次更新提交成功，但候选 DisposeAsync 抛错；第二次更新及清理成功；最后关闭仍报告历史清理失败。预期日志依次为 Applied/Failed、Applied/Complete、Failed/Failed，关闭 Error 非空。该示例仅离线编译，尚未在 Unity 执行。

## 关闭超时演示

开启默认关闭的 **Show Close Timeout Walkthrough**，宿主清理隔离准入阈值设为 1。页面关闭预算为 50 毫秒，OnCloseAsync 故意等待受控信号并忽略取消。预期 ForceClose 返回 ClosedWithCleanupPending/Pending，页面结果 Dismissed/Pending，PendingCleanupCount=1，物理清理与同步 OnClose 尚未结束。另一个路由的新页面返回 Rejected/CleanupCapacity。

放行关闭钩子后，WaitForCleanupAsync 预期返回 Closed/Complete，隔离数归零，清理令牌已取消，OnClose 已执行。finally 始终放行并等待实际清理。LifecycleChanged 先出现 CloseCleanupPending，最后出现 Closed；超时诊断属于演示预期。以上只通过离线编译，尚未在 Unity 运行。


## 独立纯同步子视图示例

将 SynchronousChildDemo 挂到空对象，配置 ThingItem Prefab（带名为 ItemLabel 的 TextElement）和 Canvas 下的内容节点。运行时同步创建道具；组件菜单“同步关闭道具”“同步打开道具”可反复使用，销毁组件时同步释放子 Scope 和提供方。可用前文示例生成的 ThingItem Prefab。

此组件的代码没有 async/await，演示 LifetimeMode.Synchronous、支持完整同步生命周期的模板、PrepareSynchronous 和 Dispose。它不使用顶层导航，也不代表 Tab 或动态列表已经具备完整同步模式。当前仅离线编译，Unity 实际操作尚未验收。

同步子界面的命令或生命周期回调可调用自己的 `Handle.RequestClose()`：忙碌时请求登记到所属 Scope，退出回调后由 `scope.Pump()` 或下一次 `scope.Tick(deltaTime)` 关闭。不要在当前命令中直接 Dispose 自身。Scope 的待请求会参与 Tick 注册；UIHost 的 Navigator.Pump 会独立派发隐藏层级请求，不受业务 Tick 暂停影响；独立宿主需要显式调用 Scope.Pump 或 View.PumpChildRequests。重复请求合并，Scope 取消和句柄移除会清除待请求，整个过程不启动异步任务。


同步道具组件还提供“同步停用道具并保留实例”和“同步恢复原道具实例”。停用后模型 Label 改为新的数量，恢复时应通过重新绑定显示该值；原 View/VM/Presenter/资源持有权保持同一份。恢复失败会关闭原实例，组件清除终态句柄后允许重新打开。此流程同样没有异步方法，尚未完成 Unity 运行验收。


## 纯同步静态嵌套控件

配置一个场景父 View：其后代包装节点名为 NestedItem，挂 NestedViewElement，包装节点下面放 ThingItem 子 View（含 ItemLabel/TextElement）。将父 View 赋给 SynchronousNestedDemo.parentView。父 View 名称和布局契约使用 SynchronousNested；模型为 `SynchronousNestedDemo.SynchronousNestedViewModel`，展示用 partial 外层按功能组织模型；生成绑定/路由工厂位于同一外层，模块注册入口使用完整类型路径。

组件的 Start 同步建立父绑定、子激活并显示；菜单“同步更换嵌套道具模型”和“同步移除嵌套道具”修改父模型，NestedViewElement 直接同步替换或移除借用子项。销毁组件同步退订并释放子激活，场景原生节点仍由场景所有者管理。示例不调用顶层 Navigator，也没有 async/await。Unity 操作尚未验收。


## 纯同步动态子视图

配置场景父 View，以及其后代的 DynamicViewElement 内容节点（不要在内容节点本身挂 View），再准备带 ItemLabel/TextElement 的 ThingItem Prefab。挂载 SynchronousDynamicDemo，将三个引用赋给 parentView、content、itemPrefab。

Start 使用纯同步 Lifetime、常驻 Prefab 提供方和 ConfigureSynchronous，直接 SetContent 后提交父子激活。组件菜单“同步替换动态道具”“同步清空动态道具”分别创建新的道具实例和清空内容。组件销毁同步释放父激活与全部子实例，然后释放提供方。代码没有 async/await；模型为借用，资源凭证同步释放，Unity Destroy 的原生销毁仍在帧末发生。仅完成离线编译，未进行 Unity 运行验收。

替换和清空菜单通过 `content.SynchronousResult.Status` 直接读取结果，不访问任务。同步操作进行期间读取该属性会明确拒绝；需要兼容任务的调用者可显式读取 `PendingChange`，回调内取得的信号对应本次替换，结束后才完成。同步失败仍直接抛出，之后可读取结果中的错误；同值重新赋值可触发失败重试。


## 纯同步虚拟列表与 Grid

配置父 View，其下的 VirtualListElement 使用已配置的 ScrollRect/Viewport/Content，以及边界内、Content 外的禁用 ThingItem 模板（ItemLabel/TextElement）。关闭 AutoLoadPages；容量需要容纳视口行数乘三列及 overscan。挂 SynchronousVirtualListDemo，赋 parentView/list。

启动直接绑定 1000 项，组件菜单可以在单列和三列网格间切换、定位键 500、移除首项。滚动和增量更新直接同步绑定/回收物化单元，日志显示定位结果与物化数量。允许配置原有多模板和高度测量；同步定位无法在有界次数内完成测量时明确失败。组件销毁同步释放子激活；场景 View/原生包装节点仍由场景所有者管理。示例没有 async/await；仅离线编译，Unity 运行与性能未验收。


SynchronousChildDemo 新增“同步换绑原道具视图”：调用现有句柄 Rebind 借用新的 ThingItemViewModel，原 View/句柄保持不变，显示新标签。日志输出提交、清理及恢复结果；无法恢复导致实例终止时清除示例引用，允许重新打开。这里只验证了离线编译，尚未执行 Unity 换绑/恢复验收。


同步道具示例现在使用字符串参数和 SynchronousThingPresenter。“同步更新道具参数”更新 Handle.Args 和显示文本；“演示同步参数提交失败并回滚”先修改文本再故意抛错，应返回 CommitFailed 并恢复此前参数及显示。候选 Dispose 释放借用引用，示例不拥有额外原生资源。以上仅离线编译，尚未运行 Unity 验收。


## 同步关闭守卫与直接结果读取

启用 NavigationDemo 的 Show Synchronous Guard Walkthrough：示例 Presenter 只实现 ISynchronousCloseGuard，第一次关闭返回 Denied，TryGetResult 应返回 false；允许关闭并递增 CloseVersion 后再次关闭，TryGetResult 直接取得已发布结果。演示使用默认异步 Navigator，关闭等待仍使用现有异步入口，不代表纯同步导航已完成。未启用时不运行此流程。当前仅离线编译，未在 Unity 执行。


同步守卫演示先调用新的 navigator.Close(handle)：当前默认宿主创建的是允许异步的生命周期实例，因此应返回 RequiresAsync，页面仍保持 Open；随后原有 CloseAsync 演示守卫拒绝与允许。该准入检查用于区分“同步准备”与“完整同步生命周期”，不是同步导航的运行验收。


## 完全同步宿主示例

将 `SynchronousNavigationDemo` 挂到场景对象，配置 `UIHost` 和 `SynchronousNavigationView` Prefab。Prefab 根使用 `View`，契约包含 `Title`（TextElement）与 `Confirm`（ButtonElement）；使用 `SynchronousNavigationViewModel` 生成的绑定。该示例直接调用 `InitializeSynchronous` 和 `Navigator.Open`，路由声明 `supportsSynchronousLifecycle: true`，业务代码没有 async/await。

运行后可以使用组件菜单同步关闭、返回、重新打开、读取结果和回收缓存；`allowClose=false` 时守卫拒绝关闭，改为 true 后允许。确认按钮生成同步命令，携带结果 42 请求关闭，由 UIHost 下一次同步 Pump 在命令栈退出后执行；结果通过 `ObserveResult` 直接通知，也可通过 `TryGetResult` 主动读取。重复打开应复用缓存并保持创建计数；显式清理缓存会释放停用实例资源，关闭则先释放本次激活资源。宿主销毁直接调用同步 Shutdown。

示例与依赖已离线编译，尚未在 Unity 运行。同步预加载导航入口及常驻 Prefab 适配已接入，真实按需加载适配与运行验收仍待完善，不应把基本链路接通理解为全部同步设计已经实现。

同步示例还提供“同步更新参数”“同步参数提交失败并回滚”“同步换绑模型”菜单。更新不重开页面，故意失败在写入标题后抛错，候选 Rollback 应恢复旧标题及参数；换绑保留原句柄和参数，Presenter 使用当前参数初始化新模型。新模型为借用，换绑后不再按自有模型内容接纳到页面缓存。这些操作的 Unity 运行效果尚未验收。

批量关闭演示：多次执行“同步打开批量关闭示例页”可在第 1 层打开最多 3 个页面；“同步关闭第 1 层”只处理该层，“同步关闭全部页面”还处理原第 0 层页面。关闭遵守 allowClose 守卫，日志逐项显示 Status/Cleanup；Completed 不等于全部关闭，应读取 AllClosed。批量关闭后宿主仍可继续打开界面。上述运行步骤尚未在 Unity 验收。

同步替换演示：“同步替换当前页面”准备新页后替换原页；“同步替换准备失败并保留原页”使候选 OnOpen 抛错，原句柄应保持 Open。关闭守卫拒绝时应得到 CloseDenied，候选同步释放。替换提交后读取 SourceClose 查看原页清理，目标失败不会回滚已经提交的替换。

第 1 层示例使用 CloseOldest：连续打开第 4 个页面会先准备新页，再经最旧页面守卫进行替换，保持上限 3 个。结果的 ReplacedHandle 和 ReplacedClose 可直接读取。相关运行行为尚未在 Unity 验收。

同步预加载菜单：“同步预加载与重复复用”连续调用应保持驻留占用 1；“同步清除预加载”应降为 0，活动页面保持有效。低内存菜单同样回收预加载。此处使用常驻 Prefab 目录，预加载仅取得独立引用，不产生额外加载，也不触发原生卸载；实际按需加载后端需要实现 ISynchronousPreloadViewProvider。以上运行步骤尚未在 Unity 验收。



加载器释放仅放弃引用，不自动卸载克隆依赖；项目若需要真正卸载，应在适当边界统一安排。自定义后端只有满足 ISynchronousInstantiableResourceLoader 的克隆安全归还契约才能使用此适配，其他后端使用原异步适配。Resources 路径缺失、版本变化、容量与延迟销毁行为尚未在 Unity 验收。



同步页面中的可替换图标可通过 ImageElement 创建 ResourceSlot：

```csharp
// 在 View 的资源接入代码中调用，icon 为 ImageElement。
// itemLifetime 为本次绑定的同步 Lifetime，由条目所有者结束。
var icons = icon.CreateSynchronousSpriteSlot(
    itemLifetime, new MUI.UnityResources.UnityResourcesLoader());
icons.Replace("Icons/Stone");
// 再次 Replace 先赋新图标再归还旧凭证；Clear 清空并归还。
// Lifetime 已自动登记此槽，结束时先清空 Image，再同步释放当前图标。
```

同一 ImageElement 在所属 Lifetime 成功释放前不能创建第二个槽或直接写 Sprite。需要保留退役画面时，持有期须覆盖实际显示期，详见[动态资源槽](../../docs/RESOURCE-SLOTS.md)。同步槽的加载器、赋值和释放回调内不能重入槽。释放错误会通知并保留到最终 Dispose，不撤销已提交显示。此用法尚未在 Unity 场景中验收。


### 纹理预览的生成绑定

可运行入口：导入 Navigation 示例后，选择 `Tools/MUI/Samples/Open Resource Binding Scene` 创建独立场景；也可在空 GameObject 上手工挂 `ResourceImageDemo`。菜单会选中示例组件，便于在 Inspector 设置 synchronous、delayMilliseconds 和 ignoreCancellation，再手动进入 Play。播放或切换播放状态时不创建场景，已有场景的未保存修改沿用 Unity 保存提示。它会创建 Canvas、Image、RawImage、按钮和状态文本，并通过示例加载器生成两个颜色不同的纹理，不需要准备 Prefab 或图片资源。示例在 View 上配置一次加载器，再显式驱动 BeginChildActivation/CommitChildActivation；图标、纹理与字体控件首次绑定 Source 时自动建立本次激活的资源槽。主文件负责绑定与清理，Layout 文件负责原生层级，Loader 文件负责样例资源及凭证，示例逻辑不进入框架 Runtime。

默认 `synchronous=true`：Next 同步切换 Warm/Cool，Clear 清空，Close 同步解绑和释放。关闭 synchronous 后再进入运行模式，资源加载会按 delayMilliseconds 延迟；ignoreCancellation 默认开启，用来演示后端交出迟到凭证的情形。模式在 Start 固定，运行时修改 Inspector 不切换当前生命周期模式。

失败与重试：先等待图片稳定显示，再点 Fail。下一组图标/纹理/字体请求会在加载器中主动失败，预期保留原图片与已提交 Source，并增加 Failures 计数。故障在请求启动时分配，不会被更早的在途请求消费。点 Retry 后，ViewModel 的 RefreshIcon/RefreshPreview/RefreshFont 显式重新发出当前键的属性通知，预期恢复到刚才请求的键；它不依赖同值 setter 重新触发绑定。同步模式分别捕获三次更新的错误，一项失败不会跳过后续项；异步模式经 UIErrors 接收本加载器的预期错误。诊断订阅只统计当前示例实例，退出时撤销，不接管其他界面的错误。尚未运行验证这些预期。

顶部 Font resource preview 通过 TextElement.FontSource 显示字体资源。Warm/Cool 的字体键借用同一 LegacyRuntime.ttf，因此不改变字形外观；原生内置字体不被示例销毁。Font 状态显示已提交键，可用于区分模型最新请求与实际完成的资源。

状态区显示当前已提交的 Source、在途数量及创建/归还计数。预期稳定显示时持有三个凭证；快速连续点击 Next 后以最后一次键为准；Clear 并等待在途结束后持有数应为零；Close 的最终日志应显示在途和持有均为零。计数仅反映凭证，不代表 Unity 帧末 Destroy 已完成，也不证明实际内存占用。以上是待运行检查的预期，不是已验证结果。

异步模式请用 Close 完成清理后再退出场景；OnDestroy 会启动或沿用清理，但不能阻塞 Unity，也不能保证停止 Play 后编辑器仍执行延迟续体。示例只销毁自己创建的资源、Canvas 和 EventSystem；已有 EventSystem 会被复用。没有现成输入系统时，它使用 StandaloneInputModule，项目须启用旧输入或预先提供与项目输入配置匹配的 EventSystem。当前仅完成离线编译，未在 Unity 启动或确认画面与交互。

需要模型直接提供资源键时，可使用 `ResourceImageViewModel`：Icon 节点为 ImageElement，Preview 节点为 RawImageElement。View 接入代码在绑定前分别调用 ConfigureSynchronousSpriteSource/ConfigureSynchronousTextureSource，或两个异步 Configure 入口；随后只需修改 IconKey/PreviewKey。必须提供与实际显示期一致的 Lifetime，结束时先解绑再清理资源。详细同步配置与清理顺序见[资源键生成绑定](../../docs/RESOURCE-SLOTS.md#通过生成绑定更新资源键)。此示例的两个属性已实际生成并编译；尚无对应 Prefab 或 Unity 运行验收。

`TexturePreviewViewModel` 是独立的绑定示例，可供头像纹理或 RenderTexture 预览使用。为它创建带根 View 的 Prefab，在名为 `Preview` 的子节点挂原生 RawImage 与 RawImageElement；节点需具备可见的 RectTransform 尺寸。三个绑定分别为 Texture → Texture、Tint → Color、SamplingRect → UVRect。示例没有附带 Prefab、摄像机或纹理资源。

`GraphicResourceViewModel` 演示 Image/RawImage 继承自 GraphicElement 的属性绑定：Icon 与 Preview 共用 MaterialKey，Label 使用独立 TextMaterialKey，三个控件共用 Tint、ReceiveRaycasts 和 UseMask。Label 挂 TextElement；需要在绑定前为三个控件分别配置适用的同步或异步 MaterialSource。它是独立的生成绑定样例，不与 ResourceImageDemo 自动合并，也没有附带材质或 Prefab；已检查生成器将继承属性解析到实际 ImageElement/RawImageElement/TextElement；可选 TMPTextElement 使用相同图形接口，但由独立 TMP 构建验证。材质与遮罩视觉尚未运行验收。

Label 还演示 LabelFontSize、LabelAlignment、LabelRichText 的生成绑定。TextElement 提供整数 FontSize、TextAnchor Alignment、RichText、FontStyle、LineSpacing、HorizontalOverflow、VerticalOverflow 和 BestFit。TMPTextElement 使用浮点 FontSize 和完整 TextAlignmentOptions，另提供 CharacterSpacing、WordSpacing、LineSpacing、ParagraphSpacing、AutoSize、WordWrapping、OverflowMode、MaxVisibleCharacters、RightToLeft。字号要求大于零，浮点排版值拒绝 NaN/Infinity，可见字符数要求非负；TMP 间距允许负值。属性设置直接同步调用原生控件，没有异步任务。

IconImageType 和 IconPreserveAspect 演示图片类型与保持比例的生成绑定，默认 Simple 与保持比例。ImageElement 还支持 FillCenter、FillMethod、FillOrigin、FillClockwise；FillAmount 可先于 ImageType 写入，Filled 时才影响画面。FillMethod 沿原生行为重置起点，动态切换时须先设置方法再设置起点；固定样式可直接保存在 Prefab 中。

LabelFont 演示 TextElement.Font 的直接对象绑定，绑定前应赋予实际字体。可选 TMPTextElement 对应 FontAsset，接受 TMP_FontAsset。两者只借用字体，不自动加载或销毁，拒绝已销毁的 Unity 对象；持有方须覆盖界面显示和使用期间。TMP 设置 null 会沿原生默认字体回退，不保证清空引用。TMP 换字体可能替换共享材质，因此 FontAsset 与 Material/MaterialSource 在同一绑定会话中被视为同一写入目标；材质资源槽持有期间直接设置 FontAsset 也被拒绝。需要自定义字体材质时先配置字体，再建立材质绑定，动态整体切换须由项目协调旧持有权结束后重新配置；本接口不提供字体和材质的原子切换或字体资源键槽。

自动字号上下限可通过 MinFontSize/MaxFontSize 绑定，旧 Text 使用非负整数，TMP 使用非负有限浮点数，零值沿用后端语义。两个边界独立写入，不按另一端的中间值自动裁剪，调用方应保证一轮更新后的范围有效；跨帧修改时可先关闭自动字号再调整边界。示例新增 LabelMinFontSize、LabelMaxFontSize 和 LabelBestFit 三个生成属性，默认范围 12–32，默认关闭 BestFit。需要精确字号时关闭 BestFit/AutoSize。避免同时以主题字号绑定和 ViewModel 绑定写入同一原生字号，现有绑定冲突检查不覆盖项目直接操作原生组件的代码。排版属性不会自动加载字体、选择回退字体或完成 RTL 语言塑形；原生布局和字体渲染仍需 Unity 验收。

这些属性同步更新，不需要 Task；模型仅借用外部提供的纹理。结束预览时先将模型 Texture 清空或销毁已经初始化的 View，再由原资源所有者释放纹理。不能让多个预览各自 Destroy 同一纹理。编译已验证实际生成 BindingContext 与 Manifest，Unity 画面尚未验收。

### 纯同步就绪和结果通知

`SynchronousNavigationDemo` 对普通打开、替换目标和批次页面统一订阅 `ObserveReadiness` / `ObserveResult`。两种订阅都不创建任务；已发布的状态立即通知，尚未发布则由导航器在安全的通知阶段直接回调。Ready 只表示首次激活就绪，不保证当前可见或输入门控开放。

示例用同步 `Lifetime` 管理订阅，以激活句柄去重，避免重复打开已有页面时重复通知；最终结果到达后移除登记。缓存重开属于新的激活，会重新订阅。销毁示例时先同步释放订阅生命周期，再调用宿主 Shutdown，防止关闭通知访问正在销毁的组件。回调内如需打开下一页，使用 `Navigator.PostOpen`，由后续同步 Pump 执行，不重入当前导航事务。

这里的等待用户操作是事件订阅，不是阻塞主线程，也不需要 Task、async/await 或每帧轮询结果。实际 Unity 通知时序尚未运行验收。


## 纯同步必需共享依赖示例

在独立场景对象上挂 `SynchronousDependenciesDemo`，设置空闲的 UIHost、`SynchronousNavigationView` Prefab，以及已有的 ThingItem Prefab（含 ItemLabel/TextElement）。两个 Prefab 都使用 View 根节点和各自生成的绑定契约；共享状态布局由项目放在适当位置。不要与其他示例同时初始化同一宿主。

启动会打开两个不同路由的父页面，它们声明同一个字符串参数、`Unit` 结果的状态依赖，参数均为 `default`。日志预期为共享创建次数 1、父拥有者数 2。“尝试单独关闭共享依赖”预期返回 InUse；关闭一个父页面后仍有 1 个拥有者；关闭第二个后共享节点退出。重新打开两页应重新创建共享状态；“强制关闭共享依赖及全部父页面”用于观察整个父链退出。

所有操作均直接调用同步导航，不使用 Task 或 async/await。示例及依赖仅完成离线编译；上述日志与运行顺序为待执行的验收步骤，尚未在 Unity 验证。


共享依赖示例还提供显式持有菜单：先“显式打开共享依赖并独立持有”，关闭两个父页面后共享界面应继续存在；再“撤销共享依赖的显式持有”才关闭。若父页面仍在，撤销只移除显式关系，日志仍显示父拥有者数不变。`Status=Released` 不等于界面关闭，应结合 `CloseOutcome` 和共享状态查看。以上仍为待运行验收步骤。


共享依赖示例增加“尝试打开依赖参数冲突的父页面”：在前两个父页面打开后执行，第三个父页面以 `other` 参数请求同一依赖，预期返回 Rejected/ConflictingData，原有两个拥有者及状态界面保持有效。该步骤仅提供可运行示例，尚未在 Unity 执行。


启动共享示例前可设置 Shared Placement：RequiredBefore 将共享状态放在两个父页面下方；AttachedAfter 放在上方。依赖和父页面使用同一 Layer，实际布局仍由所配置的 Prefab 决定。两种模式都在父显示前准备好依赖，切换配置需要重新启动示例。相对顺序仅离线源码检查，尚未执行 Unity 可视验收。


“校验共享示例的静态路由图”菜单读取 RouteGraphValidator 的诊断结果，不加载额外资源、不调用依赖参数工厂。参数冲突示例应静态通过，实际打开时再拒绝不同参数；静态图合法不代表运行时任意共享组合都合法。该菜单尚未在 Unity 执行。


共享示例提供两个换绑菜单：父页面换绑应 Applied，并保留共享状态实例及其两个拥有者；直接换绑仍被父页面持有的共享模型应 Rejected/InUse。新父模型为借用，不由导航器作为工厂模型缓存。相关显示和失败恢复尚未运行验收。


父页面现在使用字符串标题参数。“同步更新父标题并保留依赖参数”通过原标题/新标题候选提交父显示，依赖仍使用 default；“尝试通过父参数改写共享依赖”使参数工厂要求 other，应在父更新器执行前返回 DependencyChangeRequired。重绑父模型后 Presenter 使用当前 Args 恢复标题。示例只完成离线编译，未执行 Unity 操作。

## 可选依赖示例

同步共享示例增加“同步打开可选依赖示例”：一个父页面可选持有已有共享状态；另一个父页面必需持有共享状态，同时可选加载一个准备时故意失败的界面。失败项的激活资源、实例资源应先释放，父页面随后成功打开，首次 Ready 带降级标记。强制关闭共享状态后，可选父页面应保留，仍经必需关系依赖共享状态的父页面应关闭。“同步更新已降级父页面标题”保留缺席状态，不重试失败项。

`AsynchronousDependenciesDemo` 使用独立的空闲 UIHost、同一套 SynchronousNavigationView 与 ThingItem Prefab。在 Inspector 配置加载、准备及清理延迟，启动后执行“异步打开两个依赖父页面”。使用同步示例的 ViewModel 和绑定不妨碍选择异步宿主，异步能力由宿主、提供方和 Presenter 契约决定。

异步示例的预期流程：

1. 第一个父页面等待可选共享状态准备完成后打开。
2. 第二个父页面复用共享状态，并加载会在 `OnOpenAsync` 中失败的可选项；日志应先出现该项“物理清理完成”，再报告父打开成功。
3. 在加载或准备尚未完成时执行“取消当前异步打开”；当前候选应回收，不影响先前已经提交的父页面。
4. 启动前打开 `simulateNonCooperativeLoading`，重复取消操作；示例提供方故意迟到返回凭证，框架应接管后回收，不提交该候选。该模式只演示取消边界，不能作为生产资源后端实现。
5. 两个父页面都打开后执行“异步强制关闭共享依赖”；可选父页面保留并收到降级事件，必需父页面关闭。Ready 为一次性结果，后续降级应通过事件和 `GetDependencyFailures` 查询。

示例销毁时取消尚未结束的打开请求、撤销事件订阅，并观察宿主异步退出。延迟参数在初始化时固定，修改后需重启示例。以上仅完成离线编译，日志顺序、迟到资源回收和 Unity 销毁尚未运行验收。

## 编辑器依赖图浏览

运行并成功初始化 `SynchronousDependenciesDemo` 后，使用组件右键菜单“查看路由依赖图”。窗口可切换五个示例路由根，搜索节点，点击父拥有者或直接依赖跳转，查看必需/可选策略与静态错误，并复制报告。参数冲突示例应静态通过，因为冲突由运行时参数工厂的结果决定。

项目自己的编辑器程序集引用 `MUI.Navigation.Editor` 与 `MUI.Navigation`，即可调用 `MUI.Navigation.Editor.RouteGraphWindow.Show(rootRoute)`；也可以传入不超过 32 个根。`Tools/MUI/路由依赖图` 可打开或恢复窗口，首次没有数据时会显示接入说明。路由元数据只在显式调用时采集，窗口不自动调用项目代码。

工具使用独立 UXML/USS 以及 UI Toolkit。已完成编辑器 C# 离线编译和 XML 结构检查；Unity 实际模板导入、明暗主题、关系跳转和域重载后快照恢复仍需运行验收。

## 运行快照与准备期退出

同步共享示例提供“同步打印导航状态快照”；异步共享示例提供“打印异步准备与清理快照”。输出包括准备中的隐藏候选、显示索引、导航可见/交互、覆盖来源、共享父拥有者、显式持有及当前操作。快照不会触发任务或项目回调；实际控件输入锁需要另外检查，不能仅凭导航交互为 true 就断定按钮一定可点击。

异步示例还提供“准备期间强制关闭可选依赖”。将准备延迟调长后启动“异步打开两个依赖父页面”，等待日志出现失败依赖开始异步准备，再执行强制关闭菜单。该入口从快照查找 `demo.async.failing` 的准备中句柄，调用正式 `ForceCloseAsync`。预期先取消并退出依赖准备，再完成物理清理，第二个父页面以降级状态继续打开；已经就绪的共享兄弟依赖保留。此步骤尚未在 Unity 运行。

## 局部输入诊断

选中带 `View` 的对象，在现有检查器展开“输入与可见性诊断（只读快照）”，点击“采集输入快照”。可在未初始化、正常显示、存在输入阻挡和旧画面保留时分别采集；点击不会初始化或修改 View。界面显示采集时间，修改状态后需再次采集。

原导航输入演示同时输出 `View.CaptureInputSnapshot(maxBlockerReasons: 1)`，用于观察多个阻挡时的真实数量和截断标记。关闭界面后通过保留的托管 InputGate 引用查询释放状态，应为 `IsDisposed=true`、`IsOpen=false`，而不是因为阻挡数归零就认为可输入。该引用不保留 View 或 Unity 对象。

根 CanvasGroup 属性用于和 View 的门控结果对照；输入资格为 true 仍不能证明某个按钮能被点到。本示例及检查器仅完成离线编译，实际输入、祖先遮挡和 Unity 展示尚未运行验收。

## 导航生命周期追踪

播放模式中选择 UIHost，在“导航生命周期追踪”中点击开始，再执行示例打开/关闭/参数更新等操作。点击采集后可复制报告；停止保留已有记录，停止并清除会释放缓冲区。默认最近 512 条，旧条目覆盖数量与通知队列丢弃数量分别显示；关闭检查器不自动停止记录。

代码入口是 `host.Navigator.StartLifecycleTrace()`、`CaptureLifecycleTrace().ExportText()` 和 `StopLifecycleTrace()`，同步模式不创建任务。报告记录事件顺序和相对时间，Open/OpenAsync 另有请求编号、结束结果及总耗时（包括失败、取消、提前拒绝），不等于各准备阶段耗时；未启用前的历史不会补记。受理的异步 PostOpen 包含排队时间，同步 PostOpen 从实际执行 Open 开始计时。停止或重启后不将旧在途请求的结束写入新一轮记录。Unity 内的操作与显示仍待实际验收。

追踪报告另有候选准备阶段：前置依赖、模型创建、资源创建、激活准备、后置依赖。每个阶段显示候选句柄和可关联的打开请求编号，缓存命中不产生资源创建阶段。父依赖阶段包含子候选时间，不能把所有阶段简单求和；阶段“未完成”可能是异常、取消或同步能力不足，请结合请求结束结果。绑定和 Presenter 钩子暂合并为激活准备。

独立 `Replace/ReplaceAsync` 同样记录请求编号和总耗时，准备阶段沿用该编号；开始行定位源页面，结束行定位目标页面并保留关联源句柄。打开触发的超限替换复用打开请求编号，不重复生成替换请求。同步源清理直接展示 `SourceClose`，异步源清理标记为未采集，复制报告不会触发兼容任务或额外等待。

关闭追踪包括普通/强制关闭、Complete 结果完成以及显式 `WaitForCleanupAsync`。报告保留原始关闭和清理状态：`WaitCancelled/Pending` 是等待取消，`ClosedWithCleanupPending/Pending` 是逻辑关闭后仍有清理，均不能当作资源全部归还。诊断不会自动调用清理等待，也不会记录业务结果内容。Back 与按层/全部批量关闭也已接入请求边界。

`UpdateArgs/Rebind` 及异步入口也记录请求边界，结束行分别展示状态、拒绝原因、清理状态和恢复失败。可使用示例已有的参数提交失败/恢复场景查看报告；已提交但清理失败不会被压缩成普通成功，诊断不会保存参数值或新旧模型。

Back 报告保留实际返回状态，仅发生目标关闭时填写目标句柄，局部处理或策略阻挡不会猜测关闭对象。批量关闭报告包含总状态、AllClosed 和逐项状态；未请求项明确标记。逐项结果采集发生在批次返回时，不用于测量单页关闭耗时；明细只保留环形容量减一的尾部，省略数在汇总中显示，容量为一时仅保留汇总。

预加载请求报告包含 `ReusedReservation`：已有占位的复用为 true，可能仍在加载，不能等同于物理缓存命中；异步加入者等待取消时仍可为 true。导航器退出也记录请求编号及完成/异常，外层 UIHost 的提供方释放和 Unity 延迟销毁不包含在“导航器退出完成”含义内。可在显式退出后、仍处于播放模式时采集保留的时间线。

## 本地自动化接入

针对已经显示且激活为同步模式的 SynchronousNavigationView，可显式创建会话并触发真实 Confirm 按钮：

```csharp
var automation = new UIAutomation(view, LifetimeMode.Synchronous);
var before = automation.QueryState();
var dispatch = automation.InvokeCommand("Confirm");
// Accepted 只表示派发；按钮命令请求自身关闭后，由正常 Pump 派发关闭。
// 随后查询原页面句柄的结果，确认是否已返回 42，不把 Accepted 当作业务成功。
```

`SetInput` 的 string/bool/float/int 重载分别适用于 InputField（含 TMP）/Toggle/Slider/Dropdown（含 TMP）；赋相同值可能不产生通知，应读取真实控件或模型确认。会话只操作指定 View 当前绑定边界内的元素，不初始化界面，不模拟射线、焦点、输入法或编辑结束。示例用法尚未在 Unity 中运行验收。

自定义控件由 `Element` 实现 `IUIAutomationInput<T>`，通过 `InputControl` 暴露实际接收输入的 `Selectable`，在 `TrySetInput` 中同步校验并触发原生变化通知，再用 `automation.SetInput<T>(name, value)` 派发。元素和原生控件必须属于会话的同一 View；不要直接调用适配方法绕过会话校验。同步适配方法不得启动异步工作。

TMP 文本赋值保留其原生 `text` 语义，不模拟逐字输入校验或字符数限制，也不触发提交/编辑结束。TMP 下拉框自动化只接受真实选项索引，不接受 `-1` 占位值。自动化返回 `Accepted` 后仍应检查业务结果。

条件等待使用无任务帧轮询，可在同步或允许异步的会话中使用：

```csharp
// condition 只读取业务结果，不能在其中点击按钮或启动业务。
var wait = automation.WaitForCondition(() => model.IsConfirmed, TimeSpan.FromSeconds(3));
// 在项目已有的 Update 驱动中每帧调用一次；不要用 while 循环等待。
var status = wait.Poll();
// Satisfied 才表示条件成立；Failed 可读取 wait.Error。
// 不再驱动时取消等待并释放条件引用；不会取消原业务。
wait.Dispose();
```

等待不会自动跟随 View 关闭取消；可传入所属 Lifetime 的 Token，或在拥有者销毁时 Dispose。超时和令牌取消在 Poll 时观测，零超时立即检查一次；同步条件若执行过久，框架无法中断它。以上示例展示调用步骤，实际项目应将句柄保存在驱动组件中，直到终态或退出时释放。

截图与追踪导出：

```csharp
// 必须放在项目确认该帧渲染已结束的回调中，不能直接放在 Update 中。
using (var snapshot = automation.CaptureSnapshot())
{
    byte[] png = snapshot.EncodePng();
    // 项目按需保存或展示 png；快照释放后字节数组仍有效。
}
string trace = automation.ExportTrace(navigator);
```

截图为整个 Game View，不裁剪到当前 View，可能含其他界面和可见文本。项目负责导出内容与存储策略；默认及最大像素预算为 16777216，PNG 编码另有内存成本。截图、编码和 Dispose 均同步完成其托管逻辑，不启动 Task；原生纹理 Destroy 仍由 Unity 在帧末执行。方法不会自动等待合适的渲染时机，详见 [Unity 截图时机要求](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ScreenCapture.CaptureScreenshotAsTexture.html)。此入口的实际图像与释放时序尚未在 Unity 中验收。

## 构建前校验登记

在项目 Editor 程序集中显式登记实际页面目录，并引用 `MUI.Editor`。下面以页面向导生成的 Inventory 为例：

```csharp
[UnityEditor.InitializeOnLoadMethod]
private static void RegisterUIBuildCatalog()
{
    MUI.Editor.UIBuildValidation.RegisterCatalog("GameUI", catalog =>
    {
        var route = InventoryPage.CreateRoute();
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
            "Assets/UI/Inventory/Prefabs/InventoryView.prefab");
        catalog.AddPage(route, prefab, InventoryViewModelBindingFactory.Manifest);
        // 有依赖时，继续登记 route 实际引用的相同路由对象、对应 Prefab 和 Manifest。
    });
}
```

手动菜单为 `Tools/MUI/Validate Build Catalogs`，Player 构建前自动调用同一检查。项目目录必须覆盖全部依赖；不同宿主的互不相关路由集合使用不同目录标识。目录回调应只提供静态声明和资产引用，不能打开界面或创建业务模型。

检查路由图、跨根重复键、资源键/版本到 Prefab 的冲突及绑定资产。错误阻止构建；完全未登记时只报告未覆盖，不表示通过。校验不会安装或修改资源提供方，项目必须保证运行时映射与目录一致。报告可通过 `UIBuildValidation.Validate().ExportText()` 获取；实际 Unity 构建回调尚未运行验收。

## 少量奖励条目的同步回收列表

导入 Navigation 示例后，选择 **Tools → MUI → Samples → Open Recycling List Scene**，保存当前场景后进入新示例场景，再手动进入 Play。菜单使用唯一文件名保存场景，不覆盖已有示例，也不会自动开始运行。

`SynchronousRecyclingListDemo` 未配置父 View 与列表时，运行时自动创建 Canvas、布局、条目模板和按钮，使用 `RewardsViewModel` 的实际生成绑定驱动 `RecyclingListElement.Items`，条目使用带同步删除命令的 `RewardItemViewModel`。可以点击 Remove 删除对应条目，点击 Add reward 再添加；也可以按下面的结构手工制作并填写引用。只填写其中一个引用会明确报错，不混用手工场景与自动生成节点。

自动层级使用现有示例的 legacy uGUI 字体与 StandaloneInputModule，要求项目启用旧输入后端或 Both；已有 EventSystem 时直接复用。内置字体的演示文字使用拉丁字符，不包含中文字体配置。自动创建的 Canvas 和输入节点由示例 Lifetime 持有，退出时释放；项目提供的 View 与 EventSystem 不由示例销毁。

```text
RewardsView（View、CanvasGroup）
└── Lst_Rewards（RecyclingListElement）
    ├── Content（RectTransform、HorizontalLayoutGroup / VerticalLayoutGroup / GridLayoutGroup）
    └── ItemTemplate（非激活、RectTransform、NestedViewElement、按需 LayoutElement）
        └── RewardItem（View、CanvasGroup）
            ├── Icon（Image、ImageElement）
            ├── ItemLabel（Text、TextElement）
            └── Remove（Button、ButtonElement）
```

Content 初始为空；模板与 Content 互不包含。把 Content 和 ItemTemplate 指定给列表组件，把父 View 与列表指定给示例组件。模板包装节点负责条目尺寸，RewardItem 可拉伸填满包装节点；使用布局组时在包装节点配置 LayoutElement 的首选尺寸，子 View 的首选尺寸不会自动穿透包装节点。普通 ScrollRect 可以包住 Content，回收列表不控制滚动位置。

Content 与 ItemTemplate 到列表根之间不能经过其他 View 或 NestedView/DynamicView/列表等独立绑定边界，非激活节点也受此约束。Content 本身不能带 View 或独立容器；ItemTemplate 的 NestedViewElement 是允许的模板端点，内部 RewardItem 仍是独立 View。错误配置会在编辑器检查及运行时初始化时报错。

运行后初始三个奖励。组件菜单可同步删除首项，再添加奖励，观察 `MaterializedCount` 复用已有节点。集合支持 Add、Remove、Replace、Move 和 Reset 通知，列表按最新完整快照协调；池按位置复用，不提供稳定键选择或滚动锚点。移除项先完成解绑，隐藏后留池，最多保留配置容量。超过容量明确报错，不截断奖励；大量条目应使用 VirtualListElement。

点击条目自身的 Remove 按钮时，同步命令只按模型身份从集合移除该项，随后直接返回。此调用链仍在使用条目绑定，列表保留待刷新状态，在下一次可执行的 LateUpdate 中同步回收，不创建任务。普通外部集合修改仍可在调用内完成刷新。异步宿主由内部准备接口等待旧绑定退出；业务命令不要等待自身所在列表的 PendingChange，框架会拒绝这类循环等待。

父激活结束后退订来源，由父 ChildViewScope 结束条目绑定及激活资源，最终销毁列表时销毁池节点；模型由项目持有。同步路径不读取 PendingChange，也不创建或等待任务。异步宿主可等待 PendingChange；准备完成前对应条目隐藏。这里不是整批条目的原子替换，失败会保留已完成的部分并记录 Error，可在修正来源后调用 Refresh 重试。

图标通过 `RewardItemViewModel.IconKey → ImageElement.SpriteSource` 的生成绑定加载。示例只为父 RewardsView 配置一次 RewardResourceLoader，子 View 通过默认继承取得加载器，但各自独立持有资源凭证。该加载器仅实现 ISynchronousResourceLoader，直接生成 Warm/Cool 两种小图标，并借用 Unity 的 LegacyRuntime.ttf 内置字体，不需要外部图片、异步方法或任务。字体通过 FontKey → TextElement.FontSource 绑定，DefaultFont 是示例加载器的字体键；字体凭证归还不销毁 Unity 持有的共享字体。手工模板也需添加 Icon 节点并保留子 View 的 Inherit Parent Resources 设置。

界面显示 Items、Pool、Live leases 和 Released。预期初始三个条目各持有一个图标和一个字体凭证，共六个凭证；全部删除并完成同步帧刷新后，池节点可以保留，但 Live leases 应为零；再添加时复用节点并重新加载。按位置换绑可能导致多次创建/归还，因此累计创建数不等于当前条目数。组件菜单“同步关闭奖励页并输出资源计数”会先释放 Lifetime，再输出最终计数；成功清理后 Live leases 应为零。这些是待运行验证的预期，不是已测量结果；凭证归还计数也不表示 Unity 的帧末原生 Destroy 已完成。

当前完成离线编译及生成代码检查，真实布局、增删复用、异步清理和 Unity 运行表现尚未验收。

自动创建的演示界面底部提供 Add reward、Clear font、Retry font、Fail font 四个按钮。后三个按钮作用于当前首项；列表为空或已关闭时不执行操作。按钮使用同一套同步方法，监听随示例生命周期清理。

奖励列表示例的组件菜单还提供“同步清空首项字体”“同步恢复或重试首项字体”“模拟首项字体加载失败”。等待初始条目显示后模拟失败，预期旧字体继续显示，Font failures 增加但 Live leases 不变；再执行重试，显式通知同一 FontKey 重新加载。清空首项字体后该项文字不可见，预期少一个字体凭证，图标不受影响；恢复后重新持有字体。若先清空再模拟失败，预期保持空字体。以上操作不使用异步方法，预期行为仍待 Unity 运行检查。

## 字体资源绑定契约

资源绑定场景的 `ResourceImageViewModel` 使用 `[OnChanged("FontKey")]` 更新 FontCaption，再由生成绑定更新预览文字。这展示属性变化回调驱动派生表现状态；文字中的请求键不代表字体已成功加载。同键重试仍使用显式属性通知，不会触发 OnChanged。

`FontResourceViewModel` 是独立的 uGUI Text 生成绑定示例，不自动加入演示场景。为项目 View 添加名为 Label 的 TextElement，在激活前配置可加载 UnityEngine.Font 的资源加载器，并设置模型 FontKey 为有效键；用生成的 FontResourceViewModelBindingFactory 创建绑定。Content 控制文字，FontKey 控制字体资源；空键会清空字体，不能作为保留 Inspector 默认字体的标记。

该示例没有附带字体资产或 Prefab，目前验证生成绑定及离线编译，字体画面、替换与销毁尚未运行验收。

资源绑定示例还提供 `waitForInitialResources`：在播放前关闭 synchronous、启用此项并设置加载延迟，预期初始图标、纹理和字体准备完成后才显示 View。默认关闭时仍渐进显示；启用同步模式时直接加载，不使用等待任务。加载等待与界面销毁的实际交互仍需 Unity 验收。

### 键盘与手柄返回接入

交互模式下，页面 Confirm、Close 按钮会配置 `UIBackInput` 并连接当前宿主。选中其中一个按钮后，输入模块派发的 Cancel 会在 LateUpdate 转给导航的 Back 逻辑，沿用关闭守卫和当前顶层页面选择。默认 StandaloneInputModule 使用项目 Input Manager 的 Cancel 映射；项目换用其他输入模块时需自行配置对应动作。自动演示模式禁用这两个适配器，避免人工输入打断演示流程。

这是接线示例；当前只完成离线编译，尚未在 Unity 中验证键盘/手柄、焦点迁移和局部取消消费。输入框、下拉框与软键盘不在此示例的接入范围内。

字体资源示例的 FontCaption 现在是计算属性，由 FontKey 字段上的 NotifyPropertyChangedFor 生成刷新通知；它显示请求键，不代表资源已加载完成。

闲置 UI 内容由项目显式清理：导航调用 ClearInactiveContent/ClearInactiveContentAsync，Tab 调用 ClearCache/ClearCacheAsync。框架不订阅平台低内存事件或协调项目对象池。Navigation 示例需先导入 Resource Integration 接入示例。

## 泛型条目模型

`ThingItemViewModel` 继承 `LabeledItemViewModel<string>`，沿用基类的 ItemLabel 绑定，仍由示例的程序集注册入口登记。泛型基类的 Item 只借用业务数据；它不加载或管理业务资源。

直接使用泛型模型时，在项目启动层显式登记需要的闭合工厂：

```csharp
LabeledItemViewModelBindingFactory<string>.Register();
```

也可直接构造带绑定工厂的路由：

```csharp
var route = LabeledItemViewModelRoute<string>.Create(
    () => new LabeledItemViewModel<string> { Item = "Wood", Label = "木材 × 10" });
```

开放泛型不会自动注册。此示例路由只是类型接线，实际打开仍需项目把 ThingItem 资源交给提供方，并按用途配置页面策略。类型参数不代表业务资源所有权。

## 共享依赖参数冲突

SynchronousDependenciesDemo 中，“尝试通过父参数改写共享依赖”应被拒绝，原有共享状态保留。使用“以独立依赖变体打开不同参数的父页面”可以创建使用同一 Prefab 的独立依赖实例；“同步关闭独立变体父页面”归还这组关系，原共享实例继续由其原有父页面持有。

这两种行为分别表达共享契约和项目明确选择的独立上下文。框架不根据单个父页面的新参数推断其他拥有者的业务意图，也不自动协调服务器或共享数据仓库。示例已离线编译，实际实例身份与拥有者变化仍需 Unity 运行验收。
