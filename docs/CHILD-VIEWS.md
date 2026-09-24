# 子视图与 ThingItem

`ChildView` 表示 View 在父界面中的使用角色，不是另一种 View 组件基类。顶层页面与子界面共用 `View`、`ViewModel`、`Presenter`、`BindingContext` 和 `Lifetime`。`ChildViewHandle/Scope/Slot/Template` 分别负责实例控制、父级所有权、局部替换和创建配置；它们不承担顶层导航历史、模态栈与全局焦点。

公开 API 与程序集已由原 Projection 命名统一为 `ChildView*` / `MUI.ChildViews`，View 的子视图入口为 `View.ChildViews`。这是源码级破坏性改名，引用旧命名的项目需更新；Unity 元数据 GUID 保留。

当前实现：顶层 Navigation 内部的 `ViewInstance` 与 `ChildViewHandle` 组合相同的 Core `ViewPresenterLifecycle`，统一执行 OnCreate、建立绑定、OnOpen、异步打开、OnClose 与 OnDestroy。执行器持有绑定和回调阶段标记，重新激活沿用原 Presenter 并重建绑定，不重复 OnCreate。`ViewPreparation` 负责隐藏准备和每次外部调用后的资格检查；`ViewActivationCleanup` 统一关闭回调、解绑和激活排空；`ViewInstanceCleanup` 统一实例资源排空、逐项移除监听和归还视图资源。Core 仅依赖标准异步释放契约，不引用具体 ViewLease。

模型和 Presenter 工厂执行、模型所有权登记、Tick 唯一性与间隔初始化同样由公共执行器完成。ViewModelOwnership 在模型工厂调用前归实例 Lifetime 所有，返回模型先由该对象接管再检查取消，后续工厂失败仍由实例清理。外部传入模型只借用；工厂模型同时实现同步与异步释放接口时只调用异步释放，重复释放共享完成结果，清理清除模型引用。子句柄直接读取公共执行器的模型，不另存模型字段。导航与子视图各自提供工厂配置、宿主状态、资源获取、回调重入保护及结果发布；全局导航策略与局部父子门控不进入公共执行器。宿主 RebindAsync 已接通，具体协议见末尾；超时隔离与运行验收仍待完善。当前验证为离线编译与调用链检查，不替代 Unity 运行验收。

`MUI.ChildViews` 依赖 Core/Resources，不依赖 Unity 或 Navigation。`ChildViewTemplate` 描述子界面工厂，`ChildViewHandle` 驱动单次绑定与 Presenter，`ChildViewScope` 负责一组子界面的父激活、门控与清理。子视图不进入历史、不抢顶层焦点，不需要定义 Route。

## 为什么保留独立的子视图宿主

业务界面统一使用 `View + ViewModel + Presenter`。是否作为子界面由挂载方式决定，不由业务 View 的继承关系决定。复用同一个界面时，Presenter 应通过参数、状态或业务接口交流，避免直接依赖顶层导航身份。

| 职责 | 顶层界面 | 子界面 |
| --- | --- | --- |
| 创建、绑定、打开、关闭、销毁 | 共用 Core 生命周期执行器 | 共用 Core 生命周期执行器 |
| 实例操作入口 | 导航 Handle | ChildViewHandle |
| 所属宿主 | Navigator | 父 View 的 ChildViewScope |
| 显示与输入条件 | 导航层级、模态与焦点策略 | 父级有效状态与局部状态共同决定 |
| 关闭影响 | 更新导航栈、返回结果 | 移除当前子项，不隐式关闭父界面 |
| 局部异步替换 | 不承担局部槽位管理 | ChildViewSlot 准备候选并提交替换 |

因此保留的是两种宿主策略，共同生命周期在 Core 中维护。不要分别在顶层与子视图中新增两份 OnOpen、绑定或资源销毁协议；新增公共行为应优先放入公共执行器。宿主各自的状态机与重入检查仍需分别维护，公共执行器并不代表全部调度代码都已合并。

## 静态子节点

父 Prefab 中放置一个独立子 `View`，其名称空间不被父 View 的 ElementIndex 穿透。既可以通过 `NestedViewElement.ViewModel` 声明式绑定，也可以使用下方显式子视图 API。

```text
ParentView [View]
└── ItemSlot [NestedViewElement]
    └── ThingItem [View]
        └── ItemLabel [TextElement]
```

```csharp
[ObservableProperty]
[Bind("ItemSlot", nameof(NestedViewElement.ViewModel))]
private ThingItemViewModel item;
```

打开父页面前注册子 VM 的生成工厂，例如 `ThingItemViewModelBindingFactory.Register()`。Element 默认查找唯一的直接子 View，也可以在 Inspector 指定后代 View；Element 本身必须放在包装节点上。它借用父 VM 提供的模型，内部使用 EmptyPresenter；需要定制 Presenter 的子界面使用显式 ChildViewTemplate。

每个父激活的初始绑定重新提供子模型，Element 不会提前绑定上一激活遗留的模型。手工设置应在父激活建立后进行。设置为 null 会关闭子视图并隐藏节点；更换模型时先关闭旧子视图，等待旧命令和资源清理，再借用同一节点建立新绑定。连续修改按请求版本只允许最后一个有效请求提交，节点不会同时拥有两份绑定。

父 Lifetime 清理时，NestedViewElement 会解除本次模型、句柄和准备任务引用，不必等到缓存节点再次激活或销毁。该回调在 Lifetime 排空托管操作后执行，只清除控件引用；子句柄仍由 ChildViewScope 负责关闭与归还资源，不改写保留中的原生画面。最终销毁控件时，即使请求关闭抛错，也会解除引用；同步准备回调中直接销毁时，已发布的兼容完成信号仍由原准备过程收尾。此调整仅通过离线编译及源码检查，尚无 Unity 内存或缓存复开运行证据。

`PendingChange` 可等待当前换绑，`DisplayedViewModel` 表示实际激活的模型。新的生成绑定失败时，在旧对象清理成功且请求仍有效的前提下尝试恢复旧模型，同时保留原始异常。同步完成的错误直接抛出；异步错误通过 PendingChange 和 UIErrors 报告。旧子命令可以同步修改父状态触发换绑，但不能 await 自身 PendingChange，否则会等待自己结束。

以下是显式方式：

```csharp
var resource = new ViewResource("ThingItem");
var provider = new BorrowedViewProvider(resource, itemView);
var template = new ChildViewTemplate<ThingItemViewModel, string>(
    resource,
    () => new ThingItemViewModel(),
    ThingItemViewModelBindingFactory.Create,
    supportsSynchronousPreparation: true);

// parentView 为通过导航/子视图激活的 MUI.UGUI.View。
// assignedModel 被借用；省略它时使用工厂创建并拥有模型。
var item = parentView.ChildViews.Prepare(template, provider, "Wood", assignedModel);
item.Commit();
```

`BorrowedViewProvider` 在 Lease 释放时隐藏节点并结束独占借用，不 Dispose View 包装，不 Destroy GameObject。父 Prefab 仍拥有节点。一个现有 View 应复用同一个 Provider；不要为同一节点创建多个相互独立的 Provider。旧子视图清理完成后才能重新借用。

动态内容使用配置到内容区域的 `PrefabViewProvider` 或项目自己的 `IViewProvider`。子视图始终释放 Lease；节点销毁、池归还由 Provider 的 Lease 决定，不通过 VM 类型或 View 名称猜测所有权。

## 准备与提交

顺序为：创建/借用 VM → 创建 Presenter → 取得隐藏 ViewLease → 建立子 Scope → OnCreate → 绑定 → OnOpen → 可选 OnOpenAsync → Prepared。

```csharp
var item = await parentView.ChildViews.PrepareAsync(
    asyncTemplate, provider, args, assignedModel, cancellationToken);
item.Commit();
```

准备完成仍保持隐藏。Commit 表示该子项可以参与显示；若父绑定尚未提交，子绑定的初始反向写入继续暂存。父 CommitChildActivation/Scope.CommitActivation 会递归提交这些写入。有效显示还要求绑定已提交，以及 `Active AND 父有效可见 AND 子局部可见`；输入需父/子双方允许。父隐藏不结束子激活；`SetLocalState(false, false)` 的局部意图不会被父隐藏/恢复覆盖。

父同步打开在 OnOpen 后检查声明式子项的准备状态，存在未完成换绑则返回 RequiresAsync 并回滚；异步打开等待最新子项准备完成。若提交中的反向写入又产生新的异步必需子项，则本次提交失败并清理，不能将未准备完成的父页面报告为成功。

同步入口同时检查模板声明、Provider 同步能力和 Presenter 异步能力，不会阻塞等待异步钩子。失败可能涉及异步清理，`ChildViewPreparationException.CleanupCompletion` 可用于等待物理回收；异步入口则在返回失败前等待回滚。物理回收失败会与原始准备失败一起报告。

## 父子生命周期

Navigation 和子视图驱动器通过 Core 的 `IChildViewHost` 在绑定前建立父 View 的子 Scope；uGUI View 自动传播有效门控。Scope 由父激活 Lifetime 拥有，父取消立即使子视图失效，停止业务 Tick 和命令、隐藏子 View；已稳定提交的标准绑定停止数据流并保留当前值。此时不因父取消而主动释放稳定子项，待父 OnCloseAsync/OnClose 完成、父激活 Lifetime 释放 Scope 时再执行子项关闭与资源清理。显式调用 Scope.Cancel 或子项关闭仍立即开始关闭；自定义绑定未实现 IFreezableBindingContext 时，只能保证取消命令与隐藏，不能保证其自定义数据流已经停用。准备中的候选继续由原操作持有，收到迟到 Lease 后清理，不能提前销毁或归池。

关闭会取消激活与命令、隐藏子 View，执行可选 OnCloseAsync/OnClose，解绑并等待任务，执行 OnDestroy，再释放自建 VM/实例资源和 ViewLease。手工传入的 VM 不被销毁。普通子视图使用 `Unit` 结果，条目选择通过项目的业务回调或状态传递；命令 `RequestClose()` 只关闭来源子视图，不会隐式关闭父窗口。

生命周期/命令不能 await 自己或父 Scope 的清理，入口会拒绝可识别的自身等待。命令中使用不等待的 RequestClose。外部手工调用可以 `await item.DisposeAsync()`，多个清理调用共享同一结果。

一般在 Unity 主线程及其同步上下文上操作。来自 Timer/提供者线程的令牌取消先使工作失效，再通过捕获的上下文回到 UI 线程驱动节点清理；无同步上下文的宿主必须在拥有线程完成 DisposeAsync。直接把一个未激活的 View 交给普通 BindingContext 不会自动创建子 Scope；手工宿主需显式 BeginChildActivation、等待子准备、提交父绑定和 CommitChildActivation，并最终清理该 Lifetime。直接创建 ChildViewScope 的宿主同样必须在父提交时调用 CommitActivation。

## 当前验证与未完成项

Navigation 示例展示 ThingItem 的同步准备与提交、父/子门控、借用释放后节点保留、同节点异步复开以及父关闭传播。以上链路已在 Unity 2022.3 Play mode 运行通过。

声明式 NestedViewElement 的初始绑定、更换 VM、清空和再绑定也已在 Unity Play mode 演示通过。故障恢复、自身等待拒绝、提交阶段新异步工作的拒绝等分支尚未完成运行验收。

DynamicViewElement 已接入，见 [动态子视图](DYNAMIC-CONTENT.md)。尚未完成：Tab 切换与恢复策略、缓存停用/恢复、池与隔离预算、完整故障及多层子树运行验收。首帧准备目前会自动检查实现 IChildViewElement 的声明式子项；手工子视图的必需性仍由创建方显式协调。当前 Handle 关闭后不能复开；尚未关闭的 Retained 实例可以显式准备新激活，普通节点复用仍会建立新 Handle。

父取消与子项销毁分离后的顺序目前经过离线编译和调用链检查，尚未重新进行 Unity 多层子树、取消回调重入及失败清理验收。父取消通常隐藏子项；显式取得的显示保留可供退出转场使用，但不代表 Tab KeepPrevious 已接入。


## 子视图单向停用并保留显示

稳定提交后的 Handle 可以调用 `RetainAndDeactivate()`：立即进入 `ChildViewState.Retained`、撤销命令和 Tick 资格，取得子 View 的显示保留，冻结绑定并取消激活任务。VM、Presenter、Lease 和激活资源仍归原句柄所有，直到显式关闭或父级最终释放；Retained 不表示后台任务已经结束。

这是异步替换所需的旧激活退役能力，不是暂停后继续原激活。Retained 状态下不能 Commit、修改局部显示或重新执行业务；重复停用幂等，最终 `DisposeAsync` 先撤销保留画面，再执行原有关闭和资源清理。取得显示或冻结失败后继续关闭同一句柄，不恢复已经部分冻结的 Active 状态，也不复制 Lease 所有权。

父级已经取得子树显示保留时，不允许子项再独立取得保留。父级取得保留时也跳过本来就处于 Retained 的子项，避免父级结束自己的显示保留时，提前撤销子项为另一笔替换保留的画面。

此能力已完成离线编译。Tab 控制器已接入 KeepPrevious 与 RestorePrevious，后者复用有界 Slot 并校验恢复请求版本与定义身份。


## 保留实例的重新激活

`ChildViewScope.PrepareReactivationAsync(handle, cancellationToken)` 接受同一 Scope 内未关闭、没有正在执行调用的 Retained 或 Inactive 句柄。它先隐藏旧画面，执行旧激活的关闭回调、解绑和 Lifetime 排空，再复用原 View、VM、Presenter 和 ViewLease 创建新的激活与绑定。不会重复调用 OnCreate，也不会提前归还资源。参数沿用原实例参数；此入口不是更新参数接口。

成功返回只代表 Prepared，仍需调用 Commit，或将返回值交给 ChildViewSlot 提交。Slot 支持当前句柄重新准备后作为候选返回，不会在提交成功后把同一实例当作旧内容释放。准备回调及异步等待返回后均复核父级、激活令牌和实例状态，关闭后的实例不能重新变成 Prepared。

重新激活期间关闭会立即取消业务资格，但最终销毁等待重新激活代码退出，避免并发访问已经归还的 View。任何旧激活清理失败、新激活准备失败或取消均关闭原句柄，不回滚到已经退役的旧激活。旧 ActivationContext 的写入、输入阻挡、关闭及结果提交入口校验原激活令牌，不能操纵后续激活。

此接口本身不会自动恢复缓存或旧 Tab；Tab 控制器可在重新选择保留内容时显式调用。它不保证不合作任务按时结束；超时隔离仍待实现。当前验证为 Tabs 示例及依赖的离线编译和源码检查，尚未完成 Unity 重新激活、重入和失败清理运行验收。


## 停用并排空后保留实例

`await scope.DeactivateAsync(handle, cancellationToken)` 接受同 Scope 的稳定 Active 或 Retained 子视图。它立即进入 Deactivating，停止输入与 Tick、隐藏画面并取消旧激活，然后等待关闭回调、绑定命令、激活资源和子激活清理。只有全部成功才进入 Inactive；这与 Retained 只请求取消、尚未排空的语义不同。

停用保留原 View、VM、Presenter、实例 Lifetime 与 ViewLease，不调用 OnDestroy。Inactive 可以通过 PrepareReactivationAsync 重新准备，仍需 Commit 才能显示；旧关闭回调与解绑不会重复执行，下一激活使用新的 Lifetime。原参数与局部显示意图保留；Inactive 的局部状态修改仅影响下次显示意图，不会隐式激活。Deactivating 期间拒绝修改局部状态。

停用操作由 Scope 跟踪，父级释放必须等待其退出。显式关闭立即撤销资格，最终销毁等待停用/重新激活过程结束。停用失败或接受请求后的取消会关闭原实例，不将半清理对象放入可复用状态；开始前即取消不改变实例。旧任务不合作仍可能延迟清理，尚无超时隔离保证。

Tab 的 CacheRecent/KeepSelectedTabs 已使用此入口，提供容量与淘汰；停用缓存估算字节额度已接入。准备隔离仍需运行验收；全局资源预算不属于 MUI，由项目资源系统负责。当前仅完成 Tabs 示例及依赖离线编译，Unity 停用/恢复/关闭竞态未运行验收。


ChildViewSlot 可通过 ChildViewPreparationOptions 配置候选准备超时和隔离数量。迟到 Prepared 结果只清理、不提交，隔离容量计入准入；最终 Dispose 等待隔离任务与资源真正结束。该能力不等于清理超时或全局资源隔离，具体边界见 [Tab 准备超时与有界隔离](TABS.md#准备超时与有界隔离)。


## 子视图资源代际

ChildViewHandle 在模型/Presenter 工厂运行前记录提供方 ContentVersion（可选 IViewContentVersion，支持纯同步提供方）与 BindingRegistry.Generation。IsContentCurrent 供缓存宿主在所属 UI 线程检查当前内容兼容性，提供方读取失败会抛出；它不是活跃/可见状态，也不允许跨线程使用。ContentVersion 必须在内容未变时返回同一引用对象，失效后换成新的非空对象。

同步/异步准备的每次公共生命周期资格检查、停用实例恢复、Prepared 到 Active 的提交与父绑定提交均检查创建时的代际。失效后不把同一物理实例重新标记成新版本；准备/恢复/提交失败走原关闭和异步回收协议，迟到凭证也需释放。旧稳定 Active 内容不自动关闭，但不能通过恢复或后续缓存准入继续复用。最终释放清除提供方引用，外部保留旧 Handle 不会因此额外持有整个资源后端。

BindingRegistry.Reset 对显式绑定工厂也保守失效。固定提供方无需实现可选接口，装饰器应透传底层代际；ContentViewProvider 已接入。此能力经过离线编译，尚未取得 Unity 的失效竞态运行证据。

## 活动子视图的参数更新

类型化 `ChildViewHandle<TViewModel, TArgs>.UpdateArgsAsync(nextArgs, token)` 与 Navigator 共用 Core 的 IArgsUpdatePresenter/IPreparedArgsUpdate、ArgsUpdateOperation 和 ArgsUpdateOutcome。子视图 Presenter 同样必须显式声明支持；成功更新保持原 View、VM、Presenter、绑定、Handle 与激活 Lifetime，不再调用 OnOpen。句柄 Args 和 Presenter.Context.Args 一起切换，后续停用/恢复使用最近一次成功提交的参数。

```csharp
var updated = await child.UpdateArgsAsync(nextArgs, cancellationToken);
if (updated.IsApplied && updated.Cleanup == ArgsUpdateCleanup.Complete)
{
    // 新参数已经提交，候选已完成收尾。
}
```

子项必须处于稳定 Active 状态、父绑定已提交、父级仍有效、资源版本仍兼容，且不处于视觉保留中。Retained/Inactive/Prepared、父取消或关闭状态返回 SourceUnavailable；不支持协议或输入门控分别返回 Unsupported/InputControlUnsupported。同一子项只接受一个更新，IsUpdatingArgs 为 true 时后续调用返回 Busy，不建立无界等待队列。自身生命周期与候选回调内重入返回 Reentrant；普通命令可以等待更新，但不能从候选回调等待自身关闭。

关闭、父取消、RetainAndDeactivate 和 DeactivateAsync 都会撤销更新资格并取消激活令牌。EndActivationAsync 先等待更新及候选清理，再执行 OnClose 与解绑；缓存停用只有在这些工作完成后才能成为 Inactive。回滚失败或输入恢复失败时，实例开始关闭；已开始的停用也会收到恢复错误，不能把失败内容放入缓存。更新完成信号在回调之前已登记，关闭可以同步开始而不会提前销毁候选仍在使用的 View/Presenter。

候选隔离、同步提交、补偿回滚、资源移交与错误语义详见 NAVIGATION.md 的参数更新章节。参数更新不自动取消已经运行的普通业务任务；参数相关的旧异步回写仍须由 Presenter 的局部代际/Lifetime 管理。不合作的准备或候选释放会延迟关闭与停用，目前没有参数更新超时隔离。

NavigationDemo 开启 Show Args Update Walkthrough 后，还会在 ThingItem 子视图演示更新到 Iron、停用并恢复使用新参数、Slow 更新期间重复请求返回 Busy、停用取消更新并先释放迟到候选。预期第一次更新不增加 OpenCount，恢复激活才增加；停用后 ClosedDuringArgsUpdate 为 false。示例及依赖离线编译通过，未进行 Unity 重入、缓存、关闭或资源故障运行验收。

## 子视图显式换绑

```csharp
var outcome = await child.RebindAsync(nextItemModel, token);
```

该入口与 Navigator.RebindAsync 共用 ViewPresenterLifecycle 的完整切换、失败恢复与模型释放流程。句柄、参数和 Presenter 不变，不重复 OnOpen；新模型只借用。子句柄直接从共用执行器读取 ViewModel。Presenter 通过 OnViewModelChanged 迁移模型相关订阅，具体补偿和所有权边界见 NAVIGATION.md。

`IsRebinding` 覆盖旧命令排空、候选绑定、恢复及旧模型释放。同一子项与 UpdateArgsAsync 互斥，重复请求返回 Busy。自身生命周期回调或绑定会话命令不得等待换绑。关闭、父取消、停用和保留画面退役通过激活令牌取消换绑，OnClose 与实例资源释放等它排空；不能恢复或清理失败的子项不能作为成功停用的内容复用。框架在换绑期间暂停该子项及其子树 Tick，普通业务任务仍需业务自己管理。

Show Rebind Walkthrough 会让 ThingItem 使用新模型并恢复原借用模型，日志中的 OpenCount 不因换绑增加。该示例尚未在 Unity 执行；目前验证限于离线编译和调用链检查。

参数更新的清理错误也进入子项的关闭和停用结果，不只检查 RecoveryFailed。公共执行器先向宿主记录结果，再发布完成信号；子项有界保留首个历史清理异常，即使之后成功更新，停用也不能把该内容作为正常 Inactive 项复用。回滚失败会在候选释放前撤销子项资格，候选仍由原操作排空，不提前归还视图资源。


## 同步准备的操作登记

`ChildViewScope.Prepare` 已使用真正同步的 `Lifetime.Run`，不再调用 RunAsync 后检查 IsCompleted/读取任务结果。准备操作同时登记在父激活 Lifetime 与 Scope 的操作 Lifetime 中，直到创建、准备以及现有失败处理入口退出。工厂回调试图同步销毁父 Lifetime 时，在释放其资源前被拒绝；允许异步的父销毁仍可等待当前同步准备退出。

`Lifetime.Run` 不调度业务、不调用异步委托；返回值与异常直接交给调用者，内部完成信号只供异步清理等待者观察退出。同步生命周期允许 Run，但禁止 RunAsync。

允许异步的旧 Scope 仍采用原有失败处理和异步收尾协议。纯同步 Scope 的基本创建/关闭链路见下节；同步停用和重新激活见后续小节，宿主级同步换绑仍未接通。


## 纯同步子视图创建与关闭

父 Lifetime 使用 Synchronous 模式，Scope、子实例、子激活和标准绑定均继承这一模式。模板须显式传入 `supportsSynchronousLifecycle: true`，声明工厂、Presenter、模型释放和必需子依赖均支持同步；它同时开启同步准备能力，不等同于旧 supportsSynchronousPreparation。准备时会在调用工厂前拒绝声明类型只支持异步销毁的自有 VM；借用 VM 不受此释放限制；返回派生类型的工厂也必须遵守同步声明，不能借此交出只能异步释放的对象。

```csharp
using (var parent = new Lifetime(LifetimeMode.Synchronous))
{
    var children = new ChildViewScope(parent);
    var template = new ChildViewTemplate<ThingItemViewModel, Unit>(resource,
        () => new ThingItemViewModel(),
        (view, model) => BindingRegistry.Create(view, model),
        supportsSynchronousLifecycle: true);

    var item = children.PrepareSynchronous(template, synchronousProvider, Unit.Value);
    children.CommitActivation();
    item.Commit();
    children.SetHostState(true, true);
    item.Dispose();
    // parent.Dispose 也会同步释放 Scope 及其余子项。
}
```

PrepareSynchronous 接受独立 ISynchronousViewProvider，项目实现无需编写异步接口。已有 Prepare(IViewProvider) 在纯同步 Scope 中只接受同时声明同步创建/归还能力的提供方，并转入同一路径。成功返回前完成全部准备；失败同步关闭候选，原始失败和清理失败同时保留。使用现有句柄状态、代际校验、提交、关闭结果和移除通知，没有另建子视图状态机。

ChildViewHandle.Dispose 和 ChildViewScope.Dispose 在开始撤销资格前检查在途工作、回调、模型和资源能力。纯同步 BeginClose 直接调用公共同步清理，不启动 ReleaseAsync 或后台错误观察器；DisposeAsync 在纯同步 Scope 上只是返回同步释放结果。纯同步 Scope 不支持从后台线程取消并 Post 回 UI 的降级方式，错误由取消调用方接收。

Core 的 ISynchronousDisposable 提供只读 CanDisposeSynchronously；Lifetime 在取消/释放前逐层检查已登记的同步资源。Scope 因正在准备、提交或执行子命令而不能释放时，父 Lifetime 也在修改状态前拒绝，不会先释放其他父资源再发现子项仍忙。普通 IDisposable 仍按其同步释放承诺接入；自定义含在途状态的资源应实现该能力接口。

完整同步支持已经覆盖基本准备、提交、门控、失败回滚、显式关闭、父级释放、同步参数更新、宿主 VM 换绑、动态子视图、虚拟列表组件和 Tab 缓存切换；顶层导航通过 `Navigator.CreateSynchronous` 接入。相应异步入口在纯同步 Scope 上拒绝，不会自动回退执行。Factory/自定义绑定/控件必须遵守同步契约，框架不能把只能异步归还的第三方资源强制变成同步资源。契约违例造成清理能力不足时保留失败与未释放所有权，不声称已完成清理。

Navigation 示例目录提供 SynchronousChildDemo：组件配置 ThingItem Prefab 和内容节点，运行后由同步 Start 创建，组件菜单同步打开/关闭，OnDestroy 同步释放。示例不使用 async/await。当前仅离线编译，未进行 Unity 显示、嵌套关闭和失败回滚的运行验收。


## 同步停用与原实例恢复

纯同步 Scope 现在提供 `Deactivate(handle)` 和 `PrepareReactivation(handle)`，对应已有异步入口的生命周期语义：

```csharp
children.Deactivate(item); // Active/Retained → Inactive，旧激活已清理
// 此时可修改保留的模型，解绑后的 View 不响应这些变化。
children.PrepareReactivation(item); // Inactive/Retained → Prepared，仍然隐藏
item.Commit(); // 提交新激活，按父级和局部门控恢复显示
```

停用保留 View、VM、Presenter 和实例资源，只清理绑定、激活资源与子激活。恢复不重新加载 Prefab，也不调用 OnCreate；建立新的同步激活 Lifetime、重新绑定并调用 OnOpen。旧 ActivationContext 的令牌保持失效。Retained 内容可以直接恢复：先结束旧显示保留并清理旧激活，再准备新激活。

两种模式共用 Scope 的归属/状态/重入检查、句柄过渡信号、新激活构建与错误记录。同步路径不调用异步停用或恢复入口，整个过渡在父 Lifetime 和 Scope 中登记。父取消后不能继续提交；过渡期间请求同步释放父级会因在途操作而提前拒绝。准备返回前复核内容代际、宿主资格和预期状态。

同步停用失败、旧激活清理失败或恢复失败后，同步关闭原实例并聚合清理错误，不恢复已经退役的旧激活。准入失败发生在修改状态前。调用者仍须负责成功恢复后的 Commit；同步缓存和 Tab 控制器尚未接通，不能把本入口理解为已完成同步 Tab 切换。

SynchronousChildDemo 新增“同步停用道具并保留实例”和“同步恢复原道具实例”菜单，停用后更新模型，恢复时展示更新值。Navigation 示例及依赖已离线编译，Unity 同实例复用、回调次数、嵌套子激活与失败清理仍待运行验收。

## 槽内原实例换绑

由 `ChildViewSlot` 管理的实例，需要通过槽换绑时可以使用：

```csharp
// current 是先前由此槽提交的强类型 ChildViewHandle，nextModel 由项目持有。
var synchronousResult = slot.Rebind(current, nextModel);

// 异步槽使用同一请求队列；取消不表示底层清理已经排空。
var asynchronousResult = await slot.RebindAsync(current, nextModel, cancellationToken);
```

两个调用用于各自对应的槽模式，不在同一个同步槽上调用异步入口。目标在实际执行时必须仍是槽的当前活动句柄；排队期间已被替换则返回失败，不悄悄换绑新的实例。模型借用、失败恢复和资源版本检查复用句柄既有换绑逻辑。Ready 表示模型应用成功，Error 非空仍表示存在清理错误；失败恢复不成功时由既有句柄生命周期关闭实例。

异步换绑复用槽的一个活动请求、一个最新待处理请求和排空流程，后续替换/换绑取消旧请求，清空按现有规则立即撤下内容再排空。换绑回调内不得等待同一槽的新工作。准备超时隔离只适用于新候选准备，当前不对原实例换绑作超时隔离，避免把仍在修改当前实例的任务当成可安全并行的候选。完整关闭仍须等待实际换绑收尾。

当前已有代码与离线编译证据，尚未完成换绑/替换/清空交错的 Unity 运行验收。`DynamicViewElement` 已在同提供方、同资源键、版本有效且首次绑定完成时接入此入口；其他情况保留候选替换。
