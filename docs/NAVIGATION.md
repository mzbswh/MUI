# Presenter 与导航接入

候选创建失败时，提供方未交出凭证也不代表没有清理责任。SynchronousResourceLoadException 的回滚错误会记录到候选激活 Lifetime，最终关闭报告清理失败。异步准备遇到 ResourceLoadException 时，在原准备事务中等待 CleanupCompletion；回滚成功后恢复原始取消语义，回滚失败保留创建与清理两类错误。允许异步的宿主执行普通同步 Open 时，可先失败返回，由候选关闭等待该回滚任务；纯同步宿主不读取或等待异步回滚，只报告违反契约及清理失败。

## 宿主返回输入

界面内的菜单、筛选面板、子界面临时态可向所属顶层导航 `View` 登记局部返回处理器：

```csharp
// activation 为该临时内容的激活 Lifetime，navigationView 由组合层传入。
// closeLocalState 返回 true 表示已消费返回；false 表示交给其他处理器或页面策略。
var registration = navigationView.RegisterLocalBack(activation, closeLocalState);
// 提前移除时调用 registration.Dispose()；否则由 activation 自动释放。
```

后登记的处理器先执行；已取消、已释放或提前移除的登记不会参与派发。本轮新增登记留到下一次 Back，回调中移除的登记立即跳过。Navigator 只对当前可交互的候选 View 调用局部处理器，然后才应用该页面的 BackBehavior；处理器异常返回 Failed，不能继续关闭下层。临时内容与页面本身都能使用同步处理器，两种导航模式共用该顺序。

子控件应向所属导航 View 显式登记，而不是向任意嵌套 View 登记后期待自动冒泡；框架不按 Transform 或子实例创建顺序猜测局部焦点优先级。组合层负责按临时内容的打开顺序登记，并传入子内容的激活 Lifetime。输入法和控件原生编辑态仍由项目输入适配优先处理。

将返回按钮的 onClick 或项目输入动作的按下回调连接到 `UIHost.RequestBack()`。需要知道输入是否接纳时，在主线程调用 `TryRequestBack()`：返回 false 表示宿主不可用、同帧已接纳一次返回或上次返回尚未完成；返回 true 不代表关闭成功。通过 `BackInputCompleted` 读取 `CloseOutcome`，例如仅在 `NotFound` 时执行项目大厅或退出逻辑，`Blocked`、`Denied`、`Handled` 不应继续返回下层。

纯同步宿主直接执行 `Navigator.Back()`，完成通知也在当前调用栈发布；异步宿主执行 `BackAsync()` 并观察异常。完成通知期间仍保持输入占位，订阅者不能递归返回；退出宿主时清除订阅，不再发布迟到结果。输入去重只作用于该宿主入口，不改变直接调用 Navigator 的语义。

框架不自动轮询 Escape，也不依赖特定 Input System 包。项目先处理输入法组合、控件编辑态、子界面临时态，再把未消费的返回交给宿主。`UIHost.RequestBack`、`DialogCancelInput`、`ContextMenuItemInput` 通过 `BackInputConsumption` 共用同一 EventSystem 的帧消费标记，同帧只接纳一次。项目自定义取消适配也可在确认能够处理之后、执行回调之前调用 `TryConsume(system)`；失败则停止派发。此策略合并同帧输入，不识别跨帧按键重复，也不使任意第三方控件自动参与；输入动作仍应只在按下阶段触发。平台输入法与手柄行为尚需目标平台运行验收。

当前已实现基本导航闭环：类型化 Route/Args/Result、同步与异步打开、隐藏准备、结果关闭、历史返回、顺序请求、有界终态记录和宿主清理。完整窗口策略仍在实现中，具体缺口见文末。

## 1. 业务类型与 Route

```csharp
var route = new Route<InventoryViewModel, InventoryArgs, InventoryResult>(
    "inventory.default",
    new ViewResource("InventoryView", "1"),
    () => new InventoryViewModel(),
    vm => new InventoryPresenter(inventoryService),
    InventoryViewModelBindingFactory.Create);

var opened = await host.Navigator.OpenAsync(route, args, cancellationToken);
if (opened.IsSuccess)
{
    var result = await opened.Handle.WaitForResultAsync();
    if (result.IsCompleted) UseSelection(result.Value);
}
```

上述业务类型为示意；`Samples~/Navigation` 提供可编译的完整示例。Route 实例是不可变的页面配方，应复用同一声明；相同 Key 的另一份定义不会静默覆盖既有定义。ViewResource 仅描述资源键与版本，不依赖 Navigation，因此 Resources 程序集没有反向引用导航。

未提供 Presenter 工厂时使用 EmptyPresenter；未提供绑定工厂时查显式注册的 BindingRegistry。VM 默认由工厂创建并归实例所有，如果实现 IDisposable/IAsyncDisposable 则在销毁时释放；assignedViewModel 是借用，不由 View 释放。

默认 PreparationMode 为 AsyncOnly。明确支持完整同步生命周期的 Route 显式声明 `SupportsSynchronousLifecycle`；如果实例 Presenter 实际实现 IAsyncOpenPresenter，同步入口仍在取得 ViewLease 前拒绝，不跳过异步钩子。生成器保留这一显式声明，不从不完整的静态类型信息推断同步能力，避免把运行时资源、子树或绑定能力误判为同步。

## 2. Presenter 生命周期

```csharp
public sealed class InventoryPresenter :
    Presenter<InventoryViewModel, InventoryArgs, InventoryResult>,
    IAsyncOpenPresenter<InventoryArgs>
{
    protected override void OnOpen(InventoryArgs args)
    {
        ViewModel.Title = args.Title;
    }

    public async ValueTask OnOpenAsync(InventoryArgs args, CancellationToken token)
    {
        var data = await service.LoadAsync(token);
        Context.Apply(() => ViewModel.SetItems(data));
    }
}
```

可选异步钩子以 `IAsyncOpenPresenter<TArgs>`、`IAsyncClosePresenter` 显式声明。调用顺序仍保持 FUI 主线：

```text
取得隐藏 View → OnCreate → 绑定 → OnOpen → OnOpenAsync（如声明）
→ 内部状态提交 → 初始反向写入 → 显示/输入/焦点

关闭提交与取消 → 隐藏并恢复下层焦点
→ OnCloseAsync（如声明）→ OnClose → 解绑和激活清理
→ OnDestroy → 实例资源清理 → ViewLease 释放
```

当前没有进入/退出动画，所以视觉退出立即完成；不能将此误认为已实现设计中的完整转场协议。

打开准备失败且 OnOpen 已成功时执行同步 OnClose，然后清理；不执行正常关闭的异步结束钩子。OnCreate/OnOpen 自身失败依靠已登记的 Lifetime 资源与 OnDestroy 清理，避免假设未完成的初始化成功。

Presenter 的 `InstanceLifetime` 管实例级资源，`Context.Lifetime` 管本次激活。异步打开参数 token 还关联打开请求取消；需要跨准备阶段继续的工作使用 Context 的激活 Token 与 Lifetime，不保留打开请求 token 来代替页面生命周期。

## 3. 结果与来源

命令通过 CommandContext.Complete(result) 或 RequestClose 发出非等待请求。每个 BindingContext 注入独立的 ViewInstance 来源，共享 VM 不需要持有“当前窗口”的 Handle。Complete 的实际结果类型必须与 Route<TResult> 一致。

同步打开失败可能启动异步回滚，OpenOutcome.Cleanup 会明确标记 Pending；清理仍由 Navigator 监管，Shutdown 会等待它。成功打开没有回滚工作时为 NotRequired。

关闭提交后结果候选不再被后续请求覆盖。正常等待结果在清理后结束，Completed、Dismissed、Faulted 分开；资源清理错误另带 CleanupStatus。已有关闭守卫：候选仅在许可有效且正式提交关闭时接受，拒绝/许可过期保留页面，结果任务不完成，可再次提交。

CloseAsync 的调用方取消返回 WaitCancelled，真实关闭继续；WaitForResultAsync 的取消只取消这次等待并抛 OperationCanceledException，不关闭页面。

一次销毁后旧 Handle 不可操作新实例。终态记录同时受容量和 TTL 限制，淘汰后返回 UnknownOrExpired；Handle 的结果 Task 独立持有，不随账本淘汰丢失。默认保留最多 256 条、10 分钟；Navigator 和 UIHost 的创建入口可配置容量与正数 TTL。查询时按单调时钟惰性淘汰，宿主帧维护每秒清理一次，不启动计时任务。

## 4. 队列、重入与关闭

正常 OpenAsync 按有界顺序队列执行。同步 Open 只做非阻塞队列占用检查，不等待异步任务。生命周期钩子中的普通 Open/OpenAsync 返回 Reentrant，操作上下文跨 await 保留。

钩子中安排后续打开使用 PostOpen，只返回 Accepted/Busy/HostClosed，不提供可等待的完成任务。请求立即保留队列位置，由 UIHost 每帧 Pump 允许执行；来源激活已失效时丢弃。独立使用 Navigator 的宿主也必须调用 Pump。

关闭不排在普通打开队列后等待。为了避免自等待，生命周期钩子或来源命令直接等待自己的 Close/Back 会收到 Reentrant；应使用 Context.RequestClose/Complete 后返回。相同上下文不能等待自身 Host.ShutdownAsync。引擎销毁 UIHost 时使用内部非等待关闭启动方式并观察实际清理。

宿主关闭会取消请求，并等待所有已接受导航请求退出，包括排队、准备、Replace 确认及回滚阶段，然后完成剩余实例清理。判断依据是独立请求计数，不是导航队列锁是否空闲；确认阶段释放锁不会使宿主提前报告完成。同步 Open 已启动但尚未结束的异步回滚仍由实例清理任务跟踪。

队列容量默认 64。当前取消会等待不合作提供者/业务任务最终交还资源，尚未实现超时后逻辑完成、隔离预算和告警；因此不能宣称任何第三方任务都能在有限时间内关闭。

## 5. uGUI 接入

UIHost 显式 Initialize，便于先安装生成注册和项目服务。默认 provider 从 Inspector 的 Prefab 表创建；也可注入 IViewProvider，并显式说明由宿主拥有还是借用。

PrefabViewProvider 面向已驻留的 Prefab。实例先在 inactive 暂存节点创建，再初始化隐藏 View 并移入宿主。它支持立即完成的同步/异步创建；不承担 Addressables、远程加载或预加载。

ViewLease 释放会解绑/释放控件并隐藏 GameObject，然后调用 Unity Destroy。原生对象的实际销毁遵循 Unity 帧尾规则；这不等同于已经完成真机内存回收验证。

排序按 Layer 与实例顺序，IOrderedView 实现渲染移动。焦点先 OnUnfocus 再 OnFocus；覆盖规则见下文。BackBehavior 已接入；键盘/手柄选择与平台输入适配仍待完成。

## 6. 下一阶段必须补齐

- Dialog 运行验收与扩展类型、守卫超时隔离、Replace 运行验收与转场、缓存版本兼容、预算与运行验收、共享依赖与多实例溢出运行验收。
- 完整模态输入连续性、转场、readiness、超时隔离与释放预算。
- 具体远程资源适配、资源预算、Tab 缓存与虚拟列表；基础预加载、资源槽和子视图已实现。
- Route/Presenter 工厂生成、重绑定导航入口、完整诊断快照。显式参数更新见文末。
- 目标平台构建、并发/失败/内存长稳验收。

## 预加载

Navigator.PreloadAsync(Route) 托管资源预加载，ClearPreloadsAsync 清理驻留和在途工作，Shutdown 等待其最终回收。默认容量 32，包含退役资源；详见 [预加载协议](PRELOADING.md)。该入口不执行页面生命周期，也不保证页面可以同步打开。

## 覆盖、焦点与基础模态屏障

`RoutePolicy` 在 Layer 排序后决定对下层页面的作用：

| 配置 | 下层可见 | 下层输入 | 覆盖通知 |
|---|---|---|---|
| `CoveragePolicy.None` | 保持 | 保持 | 不因此触发 |
| `CoveragePolicy.BlockInput` | 保持 | 禁用 | OnCovered |
| `CoveragePolicy.Hide` | 隐藏 | 禁用 | OnCovered |

`TakesFocus=false` 可用于不抢焦点的装饰层。焦点选择最上层、允许输入且要求焦点的页面；关闭覆盖层后重新计算门控并发送 OnRevealed。多个覆盖层按累计效果计算，移除其中一层不提前恢复仍被覆盖的页面。框架先提交内部状态，再处理门控和通知；通知内请求关闭会触发重新计算。

```csharp
var dialogPolicy = new RoutePolicy(layer: 100, modal: true);
var fullScreenPolicy = new RoutePolicy(coverage: CoveragePolicy.Hide);
var decorationPolicy = new RoutePolicy(takesFocus: false);
```

`Modal=true` 默认将 None 提升为 BlockInput，要求 View 实现 Core 的 `IModalView`。uGUI View 创建与页面同父级、紧邻其下的全宿主区域 Image 屏障；页面关闭或销毁时停用并释放。宿主须为 RectTransform，并具有正常 Canvas/GraphicRaycaster/EventSystem 配置；页面内部不允许 overrideSorting Canvas 绕过同级顺序。

Unity 示例中，新建屏障同帧立即 RaycastAll 未命中，下一帧 Canvas 更新后命中；当前同步 Open 不保证新 Graphic 已进入射线查询，完整首帧 readiness 协议仍待补齐。

这是宿主区域内的 uGUI 射线屏障，不拦截项目直接读取的原始输入或其他 Canvas 的更高排序对象。尚未实现关闭手势跨帧消费、输入事件级焦点陷阱、转场期间屏障保持；局部隐藏 View 不等于关闭模态导航实例。

## 返回行为

`RoutePolicy.BackBehavior` 提供明确行为，独立于 EnterHistory：

| 行为 | 处理 |
|---|---|
| Close（默认） | 关闭候选页面，结果原因 Back |
| Ignore | 跳过当前候选，继续查找 |
| Block | 返回 Blocked，消费本次返回，不关闭页面 |
| HandleByPresenter | 调用 Presenter 的 IBackHandler.HandleBack |

`IBackHandler` 是 Navigation 的可选业务能力，不加入所有 Presenter 的继承契约。返回 `BackResponse.Close / Ignore / Handled`；Handled 表示业务已处理（例如退出局部编辑态），页面继续打开。处理器为同步契约，不在这里等待弹窗；异步确认由关闭守卫和注入的 ICloseConfirmationService 协调，标准确认页与有界服务见下文。声明 HandleByPresenter 却未实现接口会在准备阶段拒绝打开。

BackAsync 先筛选 Open 且通过宿主输入门控的候选，优先模态，再按 Layer/实际实例顺序从前向后处理，不以历史插入时间决定最上层。不进入历史的普通 Close 页面跳过，不进入历史的模态仍响应返回；显式 Block 或 HandleByPresenter 的非历史页面也可消费返回。被上层覆盖而禁用输入的页面不参与本次候选；Ignore 不绕过覆盖门控。

处理器内递归 BackAsync 返回 Reentrant；处理器已经请求关闭时，本次返回标记 Handled，不再传给下层。处理器异常报告为 Failed，保留页面；普通返回关闭仍沿用 CloseAsync 的等待取消规则，WaitCancelled 不撤销真实关闭。项目可仅在 NotFound 时接管返回，Blocked/Handled 均已消费。

候选先读取 Navigator 门控，再检查可选 IInputView.IsInputEnabled，排除局部隐藏/禁用页面；局部禁用的模态返回 Blocked，避免落到项目级返回。输入法、原生控件编辑态、子视图临时态和平台返回键订阅尚未接入。这些属于完整输入路由的后续工作，不能把调用 BackAsync 的示例等同于全部平台输入验收。

## 按来源禁用输入

Presenter 通过激活上下文取得令牌，无需拿到原生 View：

```csharp
using (Context.BlockInput("Submitting order"))
{
    await service.SubmitAsync(Context.Token);
}
```

`InputGate` 位于 Core/Input，不依赖 Unity；`IInputView` 是渲染能力契约。uGUI View 将 InputGate 与宿主、局部可见/可交互条件共同计算，再传给子视图。每次 Block 拥有独立令牌，同一令牌重复 Dispose 无副作用；仍有其他令牌时不会恢复输入。BlockerCount/BlockerReasons 可查询来源，销毁 Gate 后始终关闭且拒绝新令牌。

`Context.BlockInput` 同时登记到当前激活 Lifetime，忘记手动释放也会在激活清理时释放。每次激活只登记一个管理器，提前 Dispose 的令牌会立即从其活动链表移除，不保留到激活结束。直接调用 View.InputGate.Block 则由调用者管理，View 最终销毁会结束 Gate。所有 Gate 操作和令牌释放须在所属 UI 线程进行。

Button、Slider、Toggle、InputField Element 的原生事件入口共同检查 Element/Selectable 是否启用、Selectable.IsInteractable 及所属 View.IsInputEnabled。被禁用或覆盖时，手动 Invoke 原生事件不会转发点击或属性通知。控件所属 View 在事件时读取，支持节点复用后重新挂载。生成绑定的命令入口仍再次检查 IInputView。此规则不取消已经运行的命令，也不限制业务代码直接执行 VM 命令。

门控只应用于这些原生输入事件；显式设置 Element.Value、VM 属性更新、OneWayToSource 初始提交和列表选择失效清理仍按绑定契约同步。直接修改原生控件数据不属于框架数据同步入口：禁用时原生值可能改变，但事件不会写回 VM，框架不替项目回滚这类直接写入。自定义输入 Element 应在转发原生输入前使用受保护的 CanReceiveInput。模态射线屏障独立存在，禁用弹窗控件不取消屏障。

Gate 变化传播控件及子视图门控，并通过 IInputView.InputStateChanged 重新计算焦点；Back 在调用时读取有效状态。EventSystem 选择维护已接入下述基础协议，上述四类原生控件事件已统一门控，其余自定义控件及输入事件级路由仍需分别适配。转场模块尚未接入，示例中的动画来源只是令牌使用演示。

## 原生选择与有效焦点

Navigation 先应用覆盖和渲染门控，再读取 IInputView.IsInputEnabled 选择焦点。输入状态变化触发重新计算；计算期间的变化只请求下一次重算，不递归进入半完成状态。Presenter 仍按先 OnUnfocus、后 OnFocus 的顺序收到通知。

可选 `IFocusView` 将原生选择实现留在渲染层。uGUI View 失焦时记录并清除属于自身子树的选择；获得焦点时优先恢复仍有效的旧选择，其次使用 Inspector 的 Default Selection，最后按层级顺序查找启用且可交互的 Selectable。原目标被删除、隐藏或禁用时自动回退。没有可用目标则清空选择，不构造虚假的选中对象。

Navigator.Pump 每帧维护最上层可见模态的选择约束；没有模态时维护当前焦点页。被 InputGate 禁用的模态清除选择，并继续通过独立屏障阻断下层。UIHost 已调用 Pump，独立宿主也必须按帧调用。EventSystem 正在分发选择变化时跳过原生选中写入，避免递归选中；后续 Pump 会再次协调。

这已经提供基础选择恢复和帧级约束，但不是输入事件级的完整模态焦点陷阱：外部脚本在本帧 Pump 之后改选其他对象，直到下一次维护前仍可能短暂越界。多 EventSystem/多输入用户、完整方向导航图、输入法与失焦编辑提交行为还需专门适配。示例使用 EventSystem API 验证，没有执行人工键盘/手柄验收。


## 关闭守卫、确认与结果提交

Presenter 可选实现 `ICloseGuard`：`CanCloseAsync(CloseContext, token)` 返回 `CloseDecision.Allow / Deny / Confirm(...)`，`CloseVersion` 在影响许可的数据变化时递增。守卫用于 Close、Back 和完成结果请求，准备回滚、错误清理、宿主关闭及 `ForceCloseAsync(handle)` 绕过守卫。强制关闭结果的原因是 Forced；宿主关闭仍是 HostShutdown。

```csharp
// Presenter 中，业务修改未保存数据时同步递增 CloseVersion。
public long CloseVersion { get; private set; }
public ValueTask<CloseDecision> CanCloseAsync(CloseContext context, CancellationToken token)
{
    token.ThrowIfCancellationRequested();
    return new ValueTask<CloseDecision>(hasUnsavedChanges
        ? CloseDecision.Confirm(new CloseConfirmation("离开页面", "尚有未保存内容，确定离开？"))
        : CloseDecision.Allow);
}
```

上例业务变量仅为示意。通过 `new Navigator(provider, closeConfirmationService: service)` 或 `UIHost.Initialize(..., closeConfirmationService: service)` 注入确认服务。守卫在生命周期回调上下文中执行，不能从守卫直接 await 普通 Open；确认服务在该上下文之外执行，可打开对话框并等待回答，不占普通导航打开队列。服务必须响应来源激活 token 并清理自己拥有的对话框。未配置服务返回 ConfirmationUnavailable，不默许关闭。现有 DialogService 与 ConfirmationDialog 提供标准确认页实现，见下文。

待决请求期间原页面仍 Open，历史、绑定和激活保持有效。对同一页面重复 Close/Back 共享该请求，避免重复弹确认；CompleteAsync 遇到已有关闭或待决请求返回 AlreadyClosing，不覆盖候选。先发请求决定本轮候选，拒绝/版本过期后清除本轮请求，后续请求可重新竞争。批准后，守卫返回、确认返回及真正提交前分别检查激活/业务版本，旧许可返回 Superseded，不自动反复弹窗。

`Navigator.CompleteAsync(typedHandle, result, waiterToken)` 可等待本轮批准/拒绝和关闭结果；结果仅在关闭提交点接受，Denied/Superseded/ConfirmationUnavailable 不完成页面的 WaitForResultAsync。CommandContext.Complete 仍是发起请求、不等待的命令接口；待决期间再次 Complete 被取消拒绝，不能用它观察审批结果。异步外部 CompleteAsync 会拒绝来源命令/守卫等待自身关闭；来源命令应使用原有请求式 Complete。可变结果对象由调用者保证审批期间不被修改。

Close/Complete 的调用者 token 仅取消该调用者的等待，返回 WaitCancelled，不撤销共享请求或停止已提交清理。需要无条件关闭时使用独立控制流程的 ForceCloseAsync；不能从受本页面生命周期跟踪的守卫/确认服务中 await 自己的关闭或宿主清理，这类等待返回 Reentrant 或异常。

守卫与确认的评估任务登记在激活 Lifetime；强制关闭会取消评估，实际资源销毁等待它收敛。评估结束后才开始等待正式关闭，避免 Lifetime 自等待。没有超时隔离，不合作的守卫/确认服务仍可能使清理等待，不能把用户取消等待当作资源释放成功。

Navigation 与现有 Navigation 示例（含 UGUI 宿主）编译通过，零警告、零错误。没有新增测试；未进行关闭守卫、确认对话框重入、强制关闭竞态和结果重试的运行验收。


## 标准确认对话框

通过 `Tools/MUI/Create Confirmation Dialog Prefab` 创建原生 uGUI Prefab，再在 UIHost Inspector 的资源目录中注册 `ConfirmationDialogView`。默认模板为 640×420，标题、可滚动正文、确认/取消按钮使用同一份 ConfirmationDialog.Manifest 校验；正文支持换行，按钮提供显式左右导航，默认焦点明确设为取消按钮。两个按钮均挂载 `DialogCancelInput`，将原生 Cancel 事件转交取消按钮，复用绑定命令与关闭守卫。取消入口检查事件是否已消费、当前选中对象、源和目标控件是否可交互、是否属于同一个 View，以及父界面的输入门控；通过后先消费事件再执行按钮回调。模态背景仍由 View 的统一屏障生成，不额外创建独立 Canvas。小屏/字体/主题适配由项目调整模板，尚未做设备布局验收。

已有确认框 Prefab 不会自动迁移：在每个可选中控件上添加 `DialogCancelInput`，将 `CancelButton` 指向本 View 的取消按钮，并在 View 的 Default Selection 中指定默认控件。新增输入框等可选中控件时也需配置此入口；取消事件不会自动从选中对象冒泡到根节点。此适配只处理 EventSystem 的 Cancel，不监听全局 Escape，不替代 Navigator.Back。

标准 ConfirmationDialog 路由现支持完整同步生命周期，确认/取消按钮使用 SynchronousCommand；异步宿主也可使用同一份 VM、Presenter 和绑定。纯同步宿主初始化后直接打开：

```csharp
var route = MUI.UGUI.ConfirmationDialog.CreateRoute(
    new MUI.Resources.ViewResource("ConfirmationDialogView"));
var opened = host.Navigator.Open(route,
    new MUI.Navigation.CloseConfirmation("删除配置", "确定删除当前配置？", "确定", "取消"));
// 检查 opened.Status 后，用发起页面的激活 Lifetime 订阅结果。
opened.Handle.ObserveResult(activation, result =>
{
    if (result.Status == MUI.Navigation.ViewResultStatus.Completed && result.Value)
    {
        // 执行已确认的业务操作；根据项目需要同时检查 result.Cleanup 和 result.Error。
    }
});
```

Completed 且 Value=true 表示确认；Completed/false 是取消按钮，Dismissed 表示返回或直接关闭，Faulted 与 Cleanup=Failed 应作为错误处理。同步打开不会同步等待人的选择；结果通过句柄订阅、查询或导航事件获取，不引入任务。按钮内部请求结束自身命令时，由宿主 Pump 在命令退出后同步关闭。连续确认框请求受路由 MaxInstances=1 限制，纯同步流程由项目按结果安排下一次打开，不能使用下面的异步串行服务模拟同步等待。

`ObserveResult(lifetime, callback)` 返回可提前 Dispose 的订阅，Lifetime 结束后不再进入回调，通知前自动解除登记与业务引用。已发布结果会立即回调；等待中的结果在终态登记或超时结果提交后通知一次。一个回调撤销另一个订阅会阻止后者执行，回调异常通过 UIErrors 隔离。调用和业务回调遵循 UI 线程约束，撤销不能中断已进入的回调。

订阅通过 Lifetime 的取消令牌登记，结果通知或主动撤销时立即解除，不在父 Lifetime 的资源释放列表中积累已完成订阅。登记时已结束的 Lifetime 或错误 UI 线程会被拒绝；登记与取消竞态会回收尚未挂接的取消登记。取消与派发并发时，以回调取得执行资格的时刻为界，不抢占已取得资格的回调。

导航主动发布结果时，订阅回调处于导航回调上下文；在回调中继续导航应使用框架的延后请求入口，不能递归修改当前导航事务。异步关闭超时可能通知 Cleanup=Pending，此订阅仍只通知一次，不会在后来物理回收时重写业务结果；需要最终清理状态时使用既有清理查询/等待接口或 Closed 事件。

不需要等待任务的 Ready 接入使用 `handle.ObserveReadiness(activation, readiness => { ... })`。它与结果订阅共用令牌撤销、一次通知、线程检查和异常隔离机制。Ready 在表现状态、焦点和覆盖回调收敛后通知；就绪前关闭则在关闭提交及输入/显示退役处理后通知 ClosedBeforeReady 或 ActivationFailed，不等待后续资源回收。已经收到 Ready 后关闭不会再次通知就绪订阅。

Ready 表示本次进入准备已完成，不能用来绕过 InputGate；界面仍可能被其他模态或业务条件阻止输入。就绪回调同样遵循导航回调重入规则，需要继续导航时使用延后请求入口。

Confirm/Cancel 属性类型改为 ISynchronousUICommand。通过绑定使用无需调整；直接执行这些命令的代码应使用 Execute，并传入有效界面命令上下文，不再调用 ExecuteAsync。

```csharp
var route = MUI.UGUI.ConfirmationDialog.CreateRoute(
    new MUI.Resources.ViewResource("ConfirmationDialogView"));
var dialogs = new MUI.Dialogs.DialogService<MUI.Dialogs.ConfirmationViewModel>(
    () => host.Navigator, route);
host.Initialize(closeConfirmationService: dialogs);

// 业务直接调用，也可由关闭守卫返回 NeedsConfirmation 后自动调用。
bool accepted = await dialogs.ConfirmAsync(
    new MUI.Navigation.CloseConfirmation("删除配置", "确定删除当前配置？"), token);
```

服务以 Navigator 访问器解决初始化依赖，不使用静态全局单例；一个宿主通常共享一个服务。默认最多 16 个在途/排队请求，串行呈现。达到容量明确拒绝，取消排队不打开页面，取消已显示请求会无条件关闭自己创建的页面并等待清理后才释放串行资格。服务的 DisposeAsync 取消并排空全部工作，拥有服务的项目生命周期负责调用；建议先关闭服务，再销毁宿主。

Route 必须 Modal、TakesFocus，且 AllowMultiple=true/MaxInstances=1。此组合让并发或外部占用返回实例上限拒绝，禁止通过单实例复用借用另一调用者的对话框。标准按钮通过既有命令目标 Complete(true/false)，返回键正常关闭映射 false；打开、运行或清理错误向调用者抛出。结果任务已表明清理结束时不重复清理；取消等待后仍持有 Handle 的路径走 ForceCloseAsync，并聚合原始异常和清理异常。

标准业务状态/服务位于无 Unity 依赖的 MUI.Dialogs；uGUI 显式 BindingContext/Manifest 和 Route 工厂位于 Rendering/UGUI.Dialogs。业务项目依然可以生成自己的绑定并用具有相同 CloseConfirmation/bool 契约的 Route 创建 DialogService。服务自己的执行链不能递归等待同一服务，也拒绝活动对话框的关闭守卫通过同一服务确认自己；任意独立任务或自定义对话框业务之间的等待环不保证检测，嵌套交互应使用明确独立的服务/流程。

Tab 离开守卫已有委托入口，可复用同一服务：

```csharp
canLeaveAsync: (context, token) => dialogs.ConfirmAsync(
    new CloseConfirmation("切换标签", "放弃当前标签的未保存修改？"), token)
```

父取消由 Tab 控制器传递，仍需遵循该控制器的最新意图与版本检查。当前完成 Dialogs/UGUI/Editor 编译，零警告、零错误；没有新增测试，尚未运行验证 Prefab 创建、长文本布局、按键焦点、队列取消、父关闭或 Tab 联动。Alert/多按钮选择、TMP 标准模板与超时隔离仍待实现。


## 事务式 ReplaceAsync

```csharp
var replacement = await navigator.ReplaceAsync(current.Handle.Identity, nextRoute, nextArgs, token);
if (replacement.IsCommitted)
{
    // 原页面已经退出导航；新页面激活是否成功通过 Destination.Status 判断。
    var destination = replacement.Destination;
    // 可按需观察旧页面清理；不要在旧页面自身的命令/生命周期中等待自身销毁。
    var cleanup = await replacement.SourceCleanup;
}
```

`ReplaceAsync` 显式指定本宿主的源 Handle。每个源最多一个替换请求；源已在关闭、无效、调用会等待自身或队列已满时拒绝。候选页面独立创建 VM/Presenter/绑定并保持隐藏，准备失败只释放候选，保留源页面。相同 Route 替换可借用源的实例名额；其他准备中/打开/清理中实例仍计入上限，不会偷偷聚焦另一个实例。

准备完毕后运行源的关闭守卫，原因是 `DismissReason.Replaced`。确认阶段释放导航队列，允许确认 Dialog 打开；确认后重新进入队列，复核源状态、守卫版本和实例容量。因此确认期间其他请求可以推进，替换不承诺跨确认阶段严格 FIFO。普通关闭、强制关闭、宿主关闭均可使替换失效。

提交在不调用外部代码的区间完成源退出历史与候选进入历史。表现重算延迟至新绑定激活之后，避免中途把下层页面作为最终焦点。当前没有退出动画，旧视觉立即退出；完整转场、readiness 和跨帧输入连续性仍待实现。

`ReplaceStatus.Committed` 表示替换已提交，不代表新页面激活或旧页面清理必然成功。提交后取消不恢复旧页面。新页面状态见 `Destination`，旧页面的清理任务见 `SourceCleanup`；旧清理异常由现有导航错误渠道报告。提交前拒绝时可查看 `Rejection`；准备失败及取消详情在 `Destination`。本入口仅异步；参数更新使用独立 UpdateArgsAsync，见文末；目标页面可命中下述停用实例缓存；CloseOldest 由 OpenAsync 的超限入口复用本事务。资源提供者和守卫必须合作响应取消；当前没有超时隔离，不合作任务仍可能拖延回滚或宿主清理。

此实现仅完成离线编译与静态检查，未执行 Unity 中的守卫、取消、回调重入、替换失败和焦点运行验收。


## 实例超限 CloseOldest

```csharp
var policy = new RoutePolicy(
    allowMultiple: true,
    maxInstances: 3,
    overflow: OverflowPolicy.CloseOldest);
```

将此策略传入 Route。`OpenAsync` 或 `PostOpen` 在实例数达到上限时，选择该 Route 创建最早、仍打开的实例，复用 Replace 的准备/许可/提交/回滚流程。创建顺序由 Handle ID 确定，BringToFront 不改变顺序。默认仍为 `OverflowPolicy.Reject`。

最旧实例若正在等待关闭许可或已有替换请求，返回 Busy，不跳过它淘汰其他页面；若没有打开实例可替换（名额均被准备或清理占用），返回 InstanceLimit。其他准备中和清理中的实例仍计入容量；事务允许候选临时借用源的一个名额，不能利用连续替换无限累积旧实例。关闭守卫拒绝返回 CloseDenied，缺少确认服务返回 ConfirmationUnavailable，许可/来源过期返回 Superseded。失败或取消均不先销毁旧页面。

同步 `Open` 遇到需要超限替换时返回 RequiresAsync，不阻塞等待守卫或自动启动后台替换。单实例 Route 仍遵循原有语义：相同数据聚焦已有页面，数据冲突返回 ConflictingData；CloseOldest 不隐式执行参数更新。

`OpenOutcome.IsReplacement` 仅在替换已经提交时为 true，此时 `ReplacedHandle` 和 `ReplacedCleanup` 给出旧页面身份和清理任务。新页面最终状态仍在 Status/Handle/Error/Cleanup；即使新页面激活失败，也不丢失已经替换旧页面的事实。PostOpen 不返回可等待结果，旧清理错误仍通过导航错误渠道报告。

内部将当前队列许可交给 ReplaceCore，外层 Open 请求继续负责请求计数，避免再申请一个队列名额及自等待。确认阶段仍允许其他导航推进，结束后复核源状态与容量。当前通过离线编译与静态检查，尚未做 Unity 超限、守卫、连续打开和异步清理运行验收。


## 准备期间的跨线程取消

外部 CancellationToken 可能在计时器、网络或后台线程上触发。导航及子视图的准备注册使用 Core 内部 `UIThreadCancellation`：同一 UI 线程取消立即执行；其他线程取消通过注册时的 SynchronizationContext.Post 投递激活/绑定取消，不同步阻塞取消方。业务传入的 token 自身仍立即取消，异步业务需检查 token 后再写入状态。

准备退出时在 UI 线程排空尚未投递执行的取消，再注销注册，屏蔽已排队的迟到回调。导航在进入异步 Presenter、进入子准备和结束准备时检查 token。界面清理仍走现有回滚/释放流程。

宿主必须提供能调度回创建线程的 SynchronizationContext（Unity 主线程提供）。无上下文或上下文派发到错误线程会报告诊断，不会降级为在后台执行绑定/界面回调；无上下文时只能在注册所有者线程退出准备时补做取消，不能保证及时中断等待激活 token 的任务。本内部适配不是通用业务调度器，也不改变 Lifetime.Cancel 的调用线程契约。

该路径已离线编译；后台取消、Unity 帧调度和迟到回调竞态尚未运行验收。


## 打开请求 BeginOpen

```csharp
var request = navigator.BeginOpen(route, args, cancellationToken);
Debug.Log(request.OperationId);

// 按业务需要在 UI 线程调用：request.Cancel();
var opened = await request.Completion;
if (opened.IsSuccess)
{
    // 只有完成结果才提供已提交页面的 Handle。
    var handle = opened.Handle;
}
```

`OpenRequest<TResult>` 只提供 Guid 类型 OperationId、Cancel 和可重复等待的 Task Completion；它不是 ViewHandle，也不提前公开 View/VM/绑定。BeginOpen 立即调用既有 OpenAsync，共用有界队列、重入拒绝、实例策略、Replace/CloseOldest、宿主取消和回滚，不新增第二个导航任务或请求计数。即使同步完成、策略拒绝或宿主已关闭，仍通过同一 Completion 读取结果；无效 Route 参数仍同步抛出。

`Cancel()` 必须在创建请求的 UI 线程调用；需要后台取消时，取消传给 BeginOpen 的外部 CancellationTokenSource。返回 true 仅表示本次发出了取消信号，不代表回滚已经完成。请求完成或已经取消后返回 false。Cancel 不发起 Close；提交后的界面需要显式使用 Navigator 关闭。单实例复用场景仍返回同一页面实例的结果，OperationId 只区分调用请求。

完成时自动释放请求内部的 linked CancellationTokenSource，调用方不需要 Dispose；如果提供者在取消回调中同步完成，释放延迟到该次 Cancel 返回。没有超时隔离时，不合作任务仍可能拖延 Completion，取消不伪造完成。OperationId 不写入现有页面生命周期事件，也不表示框架维护了全局请求历史。

当前完成离线编译与静态检查；排队取消、重复等待、同步完成、后台外部取消和宿主销毁联动尚未做 Unity 运行验收。


## 按层及全部页面批量关闭

```csharp
var batch = await navigator.CloseLayerAsync(layer: 10, cancellationToken: token);
// 或 navigator.CloseAllAsync(token)
foreach (var item in batch.Items)
{
    // Outcome == null 表示取消时尚未轮到该项；不代表关闭成功。
    Debug.Log($"{item.Handle}: {item.Outcome?.Status.ToString() ?? "Not requested"}");
}
```

两者在调用时对已提交的 Open/Closing 页面建立快照，按 Layer、页面显示顺序从上往下串行关闭，保留每个 Handle 和 CloseOutcome。尚在准备、准备失败回滚的页面，以及调用后新开的页面不包含在内。CloseAll 不等于 Shutdown：不停止宿主、不取消打开请求、不清理预加载。

每项复用普通关闭守卫/确认/释放流程，拒绝一项后继续处理后面的项，不回滚已经关闭的页面。BatchCloseStatus.Completed 表示所有项均已处理，不代表全部关闭；检查 AllClosed 或各项 Outcome。空快照正常完成且 AllClosed 为 true。批量结果只完成一次，不向调用方重复发送完成回调。

取消前不再启动下一项；若已在等待某项关闭，取消只结束等待，该项为 WaitCancelled，其真实关闭继续执行，后续未处理项 Outcome 为 null。调用前已取消时返回 WaitCancelled 且没有关闭副作用。保留原实例清理任务，避免批量等待期间有界终态记录淘汰导致结果丢失。

生命周期/关闭许可回调内调用，或快照包含当前命令自身来源，会在处理任何页面前返回 Reentrant。并发批量数受导航 queueCapacity 限制，超限返回 Busy；批量不持有普通导航队列锁，确认页可正常打开。重入/容量拒绝返回空 Items 和 AllClosed=false。

批量关闭会使用导航所有权账本的当前快照：父页面先于其批次内依赖关闭，并对批次外仍有拥有者的依赖返回 `InUse`，不会切断其他父页面的关系。依赖环在取得关系时拒绝，批量排序发现账本损坏或成环时失败；批次外的父页面不被隐式加入本次快照。共享依赖的取得、释放、可选降级和父链强制收敛由同一所有权账本协调。当前仍只有离线编译与静态检查，守卫拒绝、取消、嵌套关闭、终态淘汰及焦点的 Unity 运行验收仍待执行。


## 提交与绑定激活边界

导航内部区分页面已经进入历史的 Open 状态与绑定激活完成。OpenCommitted 事件发布、CommitSourceWrites 和子绑定提交期间，页面仍没有显示/输入/焦点/Tick 资格。该阶段触发关闭或门控变化时，表现重算延迟到激活段退出；所有步骤成功且页面仍活动后才取得资格。关闭立即撤销资格。

这保证事件观察者看到的是已提交身份，同时不会因为观察者关闭另一个页面而提前显示半激活界面。Open/异步 Open/Replace 共用相同 Activate 实现。资格只代表必要绑定已提交，不是完整的转场 Readiness；进入转场可能使 Readiness 暂时为 `Transitioning`，实现见下方“进入转场”一节，退出转场不改变首次就绪结果；基础 WaitUntilReadyAsync 见下节。

此修复已完成编译和静态检查，事件重入/绑定失败/嵌套关闭的 Unity 运行验收仍待执行。


## 首次就绪结果

```csharp
var opened = navigator.Open(route, args);
if (opened.Handle.IsValid)
{
    var ready = await opened.Handle.WaitUntilReadyAsync(waitToken);
    // Ready / ClosedBeforeReady / ActivationFailed / WaitCancelled
}
```

每个页面激活共享一个就绪完成源，类型化 Handle 可重复等待。`Handle.Readiness` 查询当前结果（未完成为 Pending）；`OpenOutcome.Readiness` 是返回时快照，不在以后修改。非法 Handle 的查询或等待抛 InvalidOperationException；没有提交 Handle 的失败 Outcome 不应被当成待就绪页面。

当前无动画路径在绑定提交、显示/输入门控、覆盖与焦点回调收敛后完成 Ready。普通 Open/OpenAsync、单实例复用、Replace 与 CloseOldest 使用同一个实例结果；Replace 在表现重算延迟结束前不会完成 Ready。首次就绪之前关闭返回 ClosedBeforeReady，激活故障关闭返回 ActivationFailed 并保留错误。就绪完成不等待资源清理；已经 Ready 的页面后来关闭不改写历史就绪结果。

等待令牌只取消当前等待，返回 WaitCancelled，不取消共享完成源或关闭页面。Ready 也不代表当前可点击：页面仍可能被模态层覆盖、隐藏或业务禁用。不要在激活所依赖的回调中等待自身就绪。

这里描述的是基础首次激活协议本身：它复用同一就绪完成源，不另建平行的转场结果。进入转场、`Transitioning`、帧预算降级和取消后的收敛由下方的可选进入转场实现；退出转场发生在首次就绪之后，不改写已发布的就绪结果。编译和静态检查通过；Unity 激活失败、关闭竞态、多等待者及转场运行验收仍未执行。


## 进入转场（当前实现）

View Inspector 的 Enter Fade Duration 配置淡入秒数，默认 0 即无动画；运行时可在准备阶段设置 `View.EnterDuration`。Core 的可选 IEnterTransitionView 定义 EnterDuration、SampleEnter(normalizedTime)、FinishEnter；这些方法必须在调用内同步完成，不得启动自行持有 View 的后台任务。其他渲染器可实现同一接口。

Navigator.Tick 使用调用方给定的非缩放帧时间推进效果；UIHost 默认使用 Unity 的 Time.unscaledDeltaTime 自动驱动 Pump 和 Tick。回放或自定义时钟可保持 UIHost 启用，设置 AutomaticFramePump=false 后每帧调用 AdvanceFrame(delta)，由该入口统一派发请求和推进界面；不能同时保留自动驱动。转场中页面可见、覆盖策略生效，但自身输入及焦点关闭；完成收敛后重新计算门控并完成同一就绪任务。业务 Tick 仍按现有 RoutePolicy 执行，不以视觉进入替代业务激活。独立 Navigator 必须自行持续驱动 Pump 和 Tick；帧时间预算不等于独立墙钟看门狗。

同步 Open 返回已提交 Handle，Readiness 可能为 Transitioning；OpenAsync 与 ReplaceAsync 等待首次就绪后返回。相同实例复用也等待该实例就绪。已提交请求的外部 token 取消会直接收敛进入效果并返回已提交结果，不请求 Close；Handle.WaitUntilReadyAsync 的取消仍只取消单个等待。Replace 提交后不再使用旧页面激活 token 控制新页面转场。

RoutePolicy 的 enterTimeout 默认 5 秒。采样失败、非法时长或超过帧时间预算时，框架调用 FinishEnter 恢复最终画面；成功恢复后 Readiness 为 Ready，IsDegraded=true，Error 保留原因并报告诊断。FinishEnter 自身失败则按激活故障关闭，不能声称降级成功。关闭或宿主退出也先停止进入效果并恢复稳定视觉，旧帧快照不会再次采样失效页面。

可将 UIUserPreferences 传入 Navigator 或 UIHost.Initialize；ReducedMotion 开启时跳过效果，运行中开启则在下一帧收敛。此服务由调用方持有，不由导航释放。

前文“未实现进入动画/Transitioning”记录已由本节替代。当前仍未实现退出转场、关闭屏障跨手势连续性、通用异步动画提供者以及完整运行验收。同步采样合约不能抢占一个阻塞 UI 线程的错误实现。当前通过离线编译，不代表 Unity 视觉、输入和取消竞态已验收。


### 转场配置检查与示例

View 的结构校验和绑定契约校验会检查序列化 Enter Fade Duration 的有限性与非负值，以及根 CanvasGroup 是否存在。校验读取 SerializedObject，不初始化运行时 View。该检查不检测 Animator/第三方 Tween 对 alpha 的竞争写入，也不验证视觉效果。

Navigation 示例新增可选 Show Enter Transition Preview：使用独立 Route 在绑定工厂设置自身 View 的 0.8 秒进入效果，等待 Handle 就绪后停留 1.2 秒并关闭。默认关闭，不改变原有示例的自动流程。源码和 Editor 编译检查通过；Unity Inspector、实际淡入和输入体验尚未运行验收。


## 保留画面的前置能力：绑定冻结

标准 `BindingContext<TViewModel>` 实现可选 `IFreezableBindingContext.Freeze()`。只能冻结已经提交源写入的 Bound 会话；冻结首先令会话失效，随后拆除属性/命令订阅、取消在途命令，但不更改当前 Element 值。命令控件最终禁用写入延后到正式 Unbind，避免冻结瞬间把现有按钮配色改成禁用状态。来自已经排队的绑定回调也因会话失效停止写入。

导航关闭提交和子视图停用现在会在取消激活任务之前，尝试冻结已提交的标准绑定；关闭回调修改模型不会继续通过这些绑定刷新控件。源写入或就绪回调同步关闭时，允许冻结正在提交的会话，提交循环检查会话失效后停止剩余工作。尚未提交源写入、正在重绑、或者没有冻结能力的自定义会话不会被当作可保留画面的已冻结会话，仍走现有取消与最终解绑路径。

状态为 Frozen，不能重新 Commit 或直接 Rebind。调用方必须最终 UnbindAsync；再次使用须在解绑完成后 Bind 并重新提交。冻结不是“暂停然后恢复”旧激活，也不是已经实现了页面缓存。自定义 BindingContext 需要显式支持该能力，框架不能假定任意第三方绑定都可冻结。

冻结时退订失败不会阻断其他退订和命令取消；错误在 Freeze 抛出并保留到最终清理结果。退订/取消回调中请求 Unbind 会延迟到冻结段退出，避免释放尚在拆除的会话。正常解绑仍执行最终控件禁用及命令任务清理。

这只停止绑定层数据流，不取消 Presenter 激活、不停止 Element 自有异步资源槽、不持有额外资源、不复制渲染画面，也不禁止自定义代码直接修改控件。因此尚不能据此宣布退出转场或 KeepPrevious 完成；这些还需要子视图及视觉资源的完整保留协议。当前仅通过离线编译/静态检查。


## 显示门控保留协议

Core 的 `IVisualRetentionView` 提供 `BeginVisualRetention` / `EndVisualRetention`。uGUI View 保存当前有效可见性和 CanvasGroup 透明度，递归保留已提交子视图；保留期间输入立即关闭，后续隐藏门控仍被记录，但不立即抹掉保留画面。结束后应用最新门控，因此父级已经停用的子项仍保持隐藏，不会恢复旧激活。

此接口必须在取消激活之前取得，调用者负责冻结绑定、取消工作，并将实例和资源所有权保留到显示结束。它不复制像素，不阻止任意业务代码直接改控件、销毁节点或主动释放资源。标准列表等元素自己的取消状态显示仍需联动，不能将此接口视为完整静态快照。

子 Scope 对不支持该协议的活动子视图报告失败，并撤销已取得的保留状态；准备中或尚未完成绑定提交的候选不参与保留。保留期间不允许创建新子视图，释放期间不允许重入取得保留；释放会继续尝试所有子项并汇总异常。View 销毁与 Scope 释放也会撤销保留，开始新激活前必须先结束保留。

该协议已接入下述 Navigator 退出转场。虚拟列表的状态提示在显示保留期间仅更新逻辑状态，结束保留后应用节点变化；其他自定义元素可通过 Element.IsVisualRetentionActive 与 OnVisualRetentionEnded 协调异步表现。没有 Unity 视觉与重入运行验收。


## 帧驱动退出转场

可选 `IExitTransitionView` 在显示保留上增加退出时长、同步采样和最终收敛。uGUI View 的 `ExitDuration`（Inspector 的 Exit Fade Duration）默认 0，可在绑定工厂准备阶段设置；`RoutePolicy.ExitTimeout` 默认 5 秒，限制非缩放累计帧时长，不是关闭回调或资源清理的墙钟超时。

普通关闭、Back 和 Replace 对已完成激活且宿主可见的页面执行退出效果。关闭提交立即撤销历史、命令和业务 Tick 资格；画面取得保留后冻结绑定、取消激活。Closing 页面仍留在原渲染序列，继续按 Layer/Order 排序和参与覆盖计算；自身没有输入或焦点，模态屏障持续到视觉退出，下层也不能通过 Back 绕过该模态屏障。新打开的更高页面仍可覆盖退出中的页面。

动画结束后移除渲染序列、隐藏页面、结束显示保留并撤销模态屏障，重算下层焦点，随后执行原有 OnCloseAsync、OnClose 和资源清理，最终完成关闭结果。关闭等待者取消只停止自己的等待。ForceClose 和宿主 Shutdown 直接收敛已存在的退出效果，不依赖后续 Tick；打开失败回滚、未激活页面、零时长和 ReducedMotion 也不等待退出采样。运行期间启用 ReducedMotion 会在下一次 Tick 结束退出效果。

采样异常、无法取得显示保留或超出帧预算通过 UIErrors 报告并跳过剩余动画；隐藏、结束保留和释放屏障仍分别尝试，收尾错误进入最终清理结果。必须由宿主持续驱动 Navigator.Tick；未调用 Shutdown 又停止驱动时，不承诺帧驱动动画自行结束。

Navigation 原有可选 Show Enter Transition Preview 现在同时演示 0.8 秒淡入、模态覆盖和 0.4 秒淡出，取消演示时强制清理。源码及离线编译验证不等于实际视觉、焦点或输入验收。触发关闭的同一次 Pointer 手势持续消费仍未实现；非合作任务超时隔离、Tab KeepPrevious/RestorePrevious 和缓存复开也不由本次转场提供。


## 顶层页面关闭缓存

RoutePolicy(cacheMode: ViewCacheMode.KeepAlive) 与 Presenter 实现 IReusableViewPresenter 同时启用实例复用。Presenter.OnOpen 必须按本次参数重置激活状态，OnClose 清理本次激活；任务、订阅与临时资源归激活 Lifetime，跨关闭保留的资源归实例 Lifetime。外部 assignedViewModel 属于借用模型，不进入或命中此缓存。

正常关闭、Back 或 Replace 的旧页面在退出转场结束、关闭回调、解绑及激活资源排空全部成功后才进入缓存。取消、准备失败、强制关闭、宿主退出及清理失败均销毁实例。缓存保留 View、VM、Presenter、实例 Lifetime 和 ViewLease；旧 Handle 及业务结果仍结束。重新打开同一个 Route 对象时分配新 Handle、参数和激活 Lifetime，重绑并调用 OnOpen，不重复 OnCreate；旧句柄不能控制新激活。

Open/OpenAsync/ReplaceAsync 都可使用缓存，命中后不重复向 Provider 创建 ViewLease。同步打开仍检查 Route/Presenter 和必需子界面的同步准备能力。缓存内容的宿主回调与输入/Tick 监听重新连接到新导航实例；旧激活的绑定已释放。

Navigator/UIHost.Initialize 的 cacheCapacity 默认 16，0 禁用。满时按最近使用后归还的顺序淘汰最旧停用实例：命中移出，关闭后重新放到末尾。上一批淘汰尚未结束时不再开启新批次；若目录已满，新关闭内容直接销毁。可复用目录和自动淘汰批次各不超过 cacheCapacity，清缓存会移出整个目录并与在途淘汰一起排空，因此缓存所属内容最多占用两倍数量上限；这不包含活动页面或普通关闭中的实例，也不是物理内存预算。

CachedViewCount 统计目录条目（可能包含尚未扫描移出的过期项），RetiringCachedViewCount 统计已移出但尚未释放完成的数量。ClearCacheAsync 移出整个目录，等待目录最终释放及已开始的淘汰，重复调用共享在途清理；清理期间不接收新缓存。活动页面保持活动；ShutdownAsync 等待缓存释放。清理回调不可等待自身清缓存或宿主关闭。历史清理失败最多保留 terminalCapacity 条聚合诊断，超出只累计次数；每次清理等待者仍收到本次完整异常。

缓存模式为 ViewCacheMode.None / KeepAlive / Timed；Timed 必须指定正的 cacheDuration，其他模式禁止指定时长。TTL 从旧激活排空并实际入缓存开始，以 Stopwatch 单调时钟计时；同步预检与真正取出时均拒绝过期项。UIHost 通过 Navigator.Tick 每秒扫描一次，无帧驱动的宿主可调用 RefreshCache；维护不会累积计时任务，上一批清理未完成时暂停扫描，但过期实例始终不能命中。淘汰失败通过 UIErrors 报告，显式清缓存与宿主退出仍能观察释放错误。

当前限制：路由热更新、底层业务上下文的自动协调和整组子实例跨父激活复用不属于本框架。缓存视图被外部销毁后，复开准备会失败并清理，而非自动重试 Provider。需要结束账号/资源上下文时，项目应协调活动业务、打开队列与资源提供方，并使用下述 InvalidateCacheAsync；不能把缓存失效当成完整的账号切换协议。Navigation 示例包含可选 Show Cache Walkthrough；目前只有离线编译和调用链检查证据，尚未进行 Unity 缓存复开运行验收。


### 停用缓存字节预算

Navigator 与 UIHost.Initialize 可传 maxCachedEstimatedBytes（null 不限估算字节，非负值启用预算），Route 构造可传 estimatedRetainedBytes。该估算应覆盖单个停用实例实际保留的 View、VM、Presenter、实例资源和图片/字体凭证；项目应使用保守值，框架不把静态估算宣称为进程内存测量。启用预算后，未给估算的 Route 不缓存；单实例估算大于总预算也直接销毁。未启用预算时允许未知估算，诊断字节仅包含已知部分。

容量和字节条件共同决定缓存准入，按最近使用顺序移出足够的旧内容。旧内容必须真正完成释放协议后才归还估算额度；异步淘汰尚未完成时新关闭内容可以被拒绝缓存并正常销毁，不提前使用在途释放的额度。如果即使移出全部可复用目录也无法弥补在途/失败占用，保留原有缓存并拒绝新内容。减法比较避免整数溢出。

ReservedCacheEstimatedBytes 包含目录、在途释放以及释放失败尚未归还的额度。缓存命中转为活动实例时扣除其缓存额度；之后关闭再重新申请。FailedCacheEstimatedBytes 表示释放失败而保守保留的额度，只是诊断/准入账本，不自动重试失败资源，也不伪造释放成功。ClearCacheAsync 全部成功后本次内容额度归零；历史释放失败的额度仍保留，可能导致该 Navigator 后续缓存准入被拒绝，宿主退出也报告历史错误。

此预算不覆盖活动页面、普通关闭中的对象、独立预加载、其他缓存/池或 Unity 延后原生销毁的实际内存。它只是 UI 停用实例的准入估算，不是项目资源预算或进程内存限制；全局资源统计、共享计数、下载和卸载由项目资源系统负责。


### 缓存失效与绑定注册表重建

ClearCacheAsync 只清理当时的停用缓存，活动页面稍后正常关闭仍可再次入缓存。InvalidateCacheAsync 先同步切换内部缓存代际，再按同一清理协议释放目录及在途淘汰；失效前创建的实例内容即使仍活动、准备中或关闭中，之后也无法重新入缓存。实例在模型/Presenter 工厂执行前捕获代际，缓存命中与关闭准入均校验；淘汰回调后再校验，避免回调改变上下文留下检查空隙。重复失效各自切换代际，物理清理仍共享已有任务；无需业务自己维护版本计数。

BindingRegistry.Reset 同步切换绑定代际。框架缓存会拒绝命中旧代际内容，定期维护或 RefreshCache 将其移出释放；旧活动内容以后关闭也不能缓存。即使某 Route 显式提供绑定工厂，仍按保守原则使其缓存失效。Reset 不自动重新注册工厂，也不终止现有绑定，项目仍需正确安排注册重建流程。

Route 为不可变对象，缓存按同一对象匹配；因此不同 ViewResource.Version、VM/Presenter 变体或工厂定义不会共享缓存。同 RouteKey 的不同定义仍被注册表拒绝，本次不引入自动路由热替换。自定义 Provider 在同一 ViewResource 身份下替换资源时可实现下述 IVersionedViewProvider；显式绑定工厂改变内部捕获状态等无法自动观察的变化，仍由项目调用 InvalidateCacheAsync。

失效入口不关闭活动页面、不取消准备/排队请求，不清资源提供方自身缓存或预加载。因此它防止旧内容重新进入导航缓存，但不保证旧业务数据不再显示；账号切换等场景仍需协调关闭、取消业务任务及替换数据来源。失败清理的字节额度继续保留，不因切换代际而归零。


### 资源提供方内容代际

可选 IVersionedViewProvider.ContentVersion 使用非空对象身份作为全提供方内容代际：内容未变返回同一对象，失效时更换新对象，不重复使用旧令牌。读取必须同步、快速、无副作用；导航读取时设置回调重入保护。不实现接口的固定提供方按自身身份处理，原有 IViewProvider 实现无需更改。

ViewContent 创建前捕获提供方代际；缓存命中、关闭准入和维护都复核。准备过程每次外部调用后的资格检查也复核，Open/Replace 提交前再次检查，尤其防止 Replace 等待关闭确认期间资源已失效却仍提交旧候选。资源代际变化不主动销毁活动页面，但旧内容以后不能缓存；过期内容由现有有界维护回收。提供方代际读取失败会导致相应操作失败并清理，维护错误通过 UIErrors 报告。

PreloadAsync 在复用 Ready 记录前检查提供方代际，变化时切换预加载批次并取消/清理旧批；Tick 的每秒维护也会观察失效。迟到凭证先接管再校验，旧结果释放后返回 Superseded 或 Cancelled，不作为新批的 Ready。待释放预加载仍占原容量，新的预加载可能暂时返回 CapacityExceeded；不会为了新版本提前归还容量。预加载释放失败仍由宿主退出报告。

LoadedPrefabViewProvider 实现该契约。项目更新加载器/资源键映射后在 Unity 线程调用 Invalidate：同步换代，旧驻留项不参与查找；在途旧加载即使成功也只释放不发布。既有页面和预加载凭证继续持有各自资源，直到所有权结束，不因失效提前卸载。关闭提供方也换代。Prefab 实例化前后检查版本，原生初始化回调触发失效时清理部分实例；等待原生销毁后才释放资源凭证。ContentViewProvider 装饰器透传底层代际，并在挂载交出前复核。

该接口不自动监听 Addressables/项目热更新系统，也不检测任意 Prefab 内存修改；适配器必须在真正的资源内容切换时更新代际。当前代际覆盖整个提供方，按单资源精细失效和子视图/Tab 缓存端到端传播仍需项目按实际资源系统扩展；全局资源预算不属于框架职责。


### 资源加载边界

MUI 只定义 `IViewProvider`、资源凭证和预加载协议，不实现共享加载、下载、重试、全局资源预算、低内存监听或物理卸载。`maxCachedEstimatedBytes` 仅约束 Navigator 自己保留的停用 UI 实例；它不计量项目资源池，也不监听 `Application.lowMemory`。项目可以在自己的低内存或场景管理流程中显式调用 `ClearPreloadsAsync`、`ClearCacheAsync` 或 `InvalidateCacheAsync`，并自行协调资源后端和业务状态。详见 [资源预算职责](RESOURCE-BUDGETS.md)。

## 显式更新已打开页面的参数

`Navigator.UpdateArgsAsync(handle, route, args, cancellationToken)` 通过原有有界顺序导航队列更新实例参数。默认不支持，Presenter 必须实现 Core 的 `IArgsUpdatePresenter<TArgs>`，渲染器须实现 IInputView。handle 必须属于已激活且进入转场完成的页面，route 必须是该实例实际使用的同一个 Route 对象；关闭中、等待关闭守卫或未就绪的实例拒绝更新。

```csharp
var update = await navigator.UpdateArgsAsync(
    opened.Handle.Identity, route, nextArgs, cancellationToken);
if (update.IsApplied)
{
    // 参数已经提交；仍要检查 Cleanup 和 Error，不能把提交成功等同于候选清理成功。
}
```

更新保持 View、ViewModel、Presenter、Handle、导航结果和激活 Lifetime，不调用 OnOpen/OnClose，不重建绑定。成功后实例参数和 Presenter.Context.Args 同步更新，后续单实例 Open 的参数相等判断使用新参数。Open 遇到冲突仍拒绝，不会隐式执行更新。Args 应为不可变值或快照；框架不能回滚项目对同一个可变参数对象的原地修改。

### 候选协议

`PrepareArgsUpdateAsync(nextArgs, token)` 返回一次性 `IPreparedArgsUpdate`，准备期间只计算隔离的新状态、加载候选资源，不得修改当前 VM。方法抛错或取消而没有返回候选时，业务实现必须自行释放局部资源；一旦返回候选，框架取得唯一释放权，迟到返回也会被回收。

候选的 `Commit()` 同步应用新状态，期间 Context.Args 已是新参数。提交抛错时，框架先恢复旧参数，再调用 `Rollback()` 恢复业务模型；Rollback 必须能够处理只执行了一部分的提交。它不能只是空实现，除非业务能证明任何失败前都没有可观察变化。该协议提供补偿恢复，不隐藏单次同步提交中属性通知的中间值，也不能撤回已发送的网络请求或其他外部副作用；不可补偿的业务更新应使用 Replace 或在业务层另行事务化。

候选无论提交成功、准备后取消还是回滚，最终都执行 `DisposeAsync()`，不传入已经取消的操作令牌。成功提交所需资源应由 Commit 交给合适的项目/激活生命周期；DisposeAsync 释放未移交资源及约定的旧资源，不能释放仍由页面显示的资产。准备、提交、回滚和清理回调都不能等待新的导航、等待自身关闭或再次更新自身，框架以回调重入规则拒绝这些入口。

### 取消、输入与关闭

更新执行期间阻挡当前 View 的输入，保留已有画面与绑定，候选清理后解除阻挡。已经开始的命令、Tick、其他业务异步任务并不会因此自动停止。Presenter 必须用自己的参数代际或局部 Lifetime 协调受参数变化影响的任务，避免旧业务任务在新参数提交后回写；准备阶段也不能依赖用户输入才能结束。

队列等待与候选准备响应调用令牌、宿主关闭和当前激活取消。提交前检查线程、令牌和实例资格；成功执行同步 Commit 后，随后的取消或关闭不再把结果改成未提交。正常带守卫关闭先登记关闭请求、取消当前更新，再等待它收尾后执行守卫；守卫拒绝关闭也不会恢复已取消的更新。强制关闭立即撤销页面资格并取消激活，但 OnClose 与最终资源释放等待更新和候选清理退出。

回滚失败或输入阻挡释放失败时，实例标记失败并触发关闭，禁止缓存；尚未开始关闭时原因是 ArgsUpdateFailed，已经进行的关闭保留其原原因。关闭已开始后才发生的更新错误，在等待完成后再次读取，防止失效内容进入缓存。候选释放失败单独标记 Cleanup=Failed，不把已成功提交的新参数回退。项目取消回调错误会归入尚未完成的更新结果；若更新在取消调用内同步结束，后续取消错误仍由 UIErrors 报告。

### 结果与边界

ArgsUpdateOutcome、ArgsUpdateStatus、ArgsUpdateRejection 与 ArgsUpdateCleanup 统一位于 Core 的 MUI 命名空间；返回的 RecoveryFailed 表示宿主必须关闭实例。ArgsUpdateStatus 包括 Applied、Rejected、CancelledBeforeCommit、PreparationFailed、CommitFailed。Rejected 细分 HostClosed、Reentrant、Busy、SourceUnavailable、RouteMismatch、Unsupported、InputControlUnsupported。CommitFailed 表示发生了提交异常，可能已成功回滚，也可能由于恢复失败而触发关闭，应结合 Error 和句柄状态处理。Cleanup 使用 ArgsUpdateCleanup，包含 NotRequired/Complete/Failed，独立反映候选及输入收尾，不代表实例最终关闭清理。

顶层页面与子视图目前都提供异步参数更新入口；同步候选可以使用已完成的 ValueTask。子视图入口见 CHILD-VIEWS.md。VM Rebind 见下一节；参数更新专用超时隔离尚未实现；完成诊断事件见下文。不合作的准备/清理可能拖延导航队列和关闭，框架不会提前宣称资源已经释放。

NavigationDemo 的 Show Args Update Walkthrough 默认关闭。开启后演示成功更新且 OpenCount 仍为 1、用新参数再次打开复用原句柄、准备失败保留旧数据、部分提交失败回滚，以及关闭时回收忽略取消的迟到候选。示例与依赖已离线编译，未启动 Unity，交互、重入、关闭守卫与资源异常仍需运行验收。

## 显式 ViewModel 换绑

```csharp
// nextModel 由调用方持有；成功后也不会转移所有权给框架。
var outcome = await navigator.RebindAsync(handle.Identity, route, nextModel, token);
if (!outcome.IsApplied)
{
    // 检查 Rejection、Error、RecoveryFailed 和当前句柄状态。
}
```

仅支持活动且已完成首次激活的实例，Route 必须为原对象。换绑保留 View、Presenter、Handle、参数、激活 Lifetime 和结果任务，不调用 OnOpen/OnClose。新模型应已具备展示状态；不能依靠再次 OnOpen 初始化它。共享 Core 执行器阻挡输入、暂停框架 Tick、退订并排空旧绑定命令，同步模型与 Presenter 引用，执行 OnViewModelChanged，再建立新绑定、等待必要子项准备并提交源写入。

Presenter 可重写 `OnViewModelChanged(TViewModel previous)`：撤销 previous 的模型订阅，通过当前 ViewModel 连接新订阅。钩子在失败恢复时也会反向调用，须能够处理上一轮只完成了一部分工作的情况。实例级资源可保留；绑定之外的模型相关异步任务须由 Presenter 自行取消、排空或用代际隔离。框架不会自动撤销网络请求、共享数据修改或任意属性通知。

失败时先清理候选绑定，再恢复旧模型引用和 Presenter 钩子；若界面仍活动，重新绑定旧模型。调用者取消也执行恢复，强制关闭或停用则不重新激活旧 UI。恢复或输入门控清理失败会标记实例失败并关闭，原因 RebindFailed；已有关闭保留原原因。成功提交后才释放原工厂模型，优先异步释放；原模型是借用对象时不释放。新模型始终借用，因此换绑后的导航内容不进入实例缓存。再次传入同一模型不会重建绑定，也不改变原有所有权。

`RebindOutcome` 区分 Applied、Rejected、Cancelled、Failed。`Cleanup` 独立报告 NotRequired/Complete/Failed；Applied 且 Cleanup=Failed 表示新模型已经生效，但旧资源释放等收尾失败，不会倒退到已释放的原模型。首个清理失败保留至实例结束，后续成功换绑不能把它抹掉。RecoveryFailed 表示实例不能继续使用；普通失败可能已恢复旧绑定，应结合该字段和当前句柄状态处理。

换绑复用导航有界队列，与参数更新串行。源实例在排队阶段已有占位，同一源重复请求返回 Busy；自己的命令、生命周期回调和关闭守卫不能等待换绑。旧命令在源换绑占位存在时发起新的排队导航会被拒绝，防止队列与旧命令互相等待。正常关闭取消换绑、等待收尾后再运行关闭守卫；强制关闭立即撤销资格，但 OnClose 和最终资源归还等待换绑及原模型释放。换绑完成诊断事件已接入，尚无换绑超时隔离，不合作的命令/资源释放会持续阻塞收尾。

NavigationDemo 的 Show Rebind Walkthrough 默认关闭。示例包含部分钩子失败恢复、订阅迁移、打开次数保持为 1、工厂模型释放和借用模型不释放，以及原模型异步释放期间强制关闭。当前仅有源码检查与离线编译证据，Unity 实际交互及故障时序未验收。

## 参数更新与换绑完成事件

`LifecycleChanged` 增加两类完成事件：`ArgsUpdateFinished` 携带可空的 `ArgsUpdateOutcome`，`ViewModelRebindFinished` 携带可空的 `RebindOutcome`。只有对应事件的结果字段有值，旧 Open/Close 事件的这些字段为 null。事件不附带参数值或模型字段，复用现有最多 256 项的事件队列、溢出计数及观察者异常隔离。

这些事件表示实例操作已结束，包括候选或旧模型的清理完成；不是单独的提交时刻通知。Applied 与 Cleanup=Failed 仍按原结果报告，失败或取消也会发布。入队前拒绝、排队取消、找不到来源或路由不匹配等尚未进入实例执行的请求不发布该事件，调用方应读取返回值。同模型换绑若到达实例入口，会发布 Applied/NotRequired 的完成结果。

并发关闭时，CloseCommitted 甚至 Closed 可能早于操作完成事件；观察者不能据此假定页面还处于活动状态。Sequence 是事件入队顺序，CommitVersion 是入队时实例的最新版本，可能已包含关闭提交。观察者在导航回调保护中执行，不能等待新的排队导航或自身关闭；诊断异常不会改变操作结果。NavigationDemo.Diagnostics 展示按类型读取结果和输出中文日志。

参数更新回滚失败时，框架先通知宿主关闭，再释放临时输入门控，避免门控变化回调接触仍被标记为活动的故障实例。原生门控事件、完成与关闭的交错时序仍需 Unity 运行验收。

回滚失败的关闭通知先于候选异步释放，释放期间该实例不再拥有 Tick 和新命令资格，但实际资源归还仍等待更新排空。更新执行器在完成信号发布前将完整结果交给宿主，关闭会读取其中的恢复失败与清理失败；提前开始关闭后发生的释放错误不会遗漏。每个实例仅保留首个历史清理异常，后续成功更新不会消除它，关闭结果继续报告错误且内容不能入缓存。一般提交失败但成功恢复、成功清理，不会因为保留诊断而永久污染实例。

## 关闭超时与待清理实例

`RoutePolicy.CloseTimeout` 默认为 30 秒，可配置正 TimeSpan，最大为 int.MaxValue 毫秒。计时从视觉退出结束后开始，覆盖更新排空、关闭钩子、解绑及实例资源释放；退出动画继续使用独立的 ExitTimeout。超时不会中断同步阻塞代码；异步计时和取消在所属 UI SynchronizationContext 上继续执行，因此宿主必须持续调度该上下文。

```csharp
var policy = new RoutePolicy(closeTimeout: TimeSpan.FromSeconds(10));
var closed = await navigator.CloseAsync(handle.Identity);
if (closed.Status == CloseStatus.ClosedWithCleanupPending)
{
    // 这里只取消本调用者的等待，不会停止后台清理。
    var physicallyClosed = await navigator.WaitForCleanupAsync(handle.Identity, token);
}
```

超过预算时，CloseAsync/ForceCloseAsync 返回 ClosedWithCleanupPending，Cleanup=Pending，并携带 TimeoutException。页面结果也只完成一次：已经接受的业务完成值仍为 Completed，普通关闭为 Dismissed，已有关键生命周期失败为 Faulted；Cleanup 标记 Pending。框架随后取消专用清理令牌，合作式 OnCloseAsync 可响应取消；模型和资源释放仍由原清理流程继续执行，不强行归还仍在使用的对象。

超时实例继续留在宿主 entries 中，状态仍为 Closing，保留原 View、模型和资源所有权；它已退出显示、输入与 Tick，不再挡住下层页面。`PendingCleanupCount` 统计这些实例；它们仍占路由 MaxInstances，也继续持有资源提供方的实际资源份额。达到 Navigator/UIHost 的 `cleanupCapacity`（默认 16）后，创建新顶层实例的 Open/Replace 返回 CleanupCapacity；聚焦已有活动实例不受此限制。已经活动或在途的页面仍可关闭，所以该值是新实例准入阈值，不是拒绝清理已有资源的硬上限。当前未增加独立的隔离字节估算预算。

真实清理结束后减少隔离计数、移入有界终态记录并发布 Closed。曾超时的内容即使最终成功，也不会进入缓存。`WaitForCleanupAsync` 等待实际结果，尚未开始关闭返回 Blocked；终态记录过期后返回 UnknownOrExpired。它不重写此前已返回的页面结果；需要最终资源状态的调用方应保存该等待任务或及时查询。

`CloseCleanupPending` 事件使用 CloseOutcome 字段发布超时快照，随后 Closed 才表示物理清理结束。ReplaceOutcome/溢出替换中的 SourceCleanup 保持真实清理语义，不因关闭响应超时提前完成。ShutdownAsync 也等待实际清理后才允许 UIHost 释放提供方；不合作且永不结束的任务仍会阻塞宿主销毁，框架不会宣称资源已经释放。

当前只接通关闭阶段的超时隔离。尚未关闭时的 Prepare、关闭守卫、参数更新及 VM 换绑仍需各自的超时策略；它们占据的导航队列不会仅因 CloseAsync 返回 Pending 自动释放。Show Close Timeout Walkthrough 演示忽略取消的关闭钩子、容量拒绝、迟到清理及清理令牌取消。仅离线编译与源码检查，Unity 运行时序仍未验收。


## 标准单按钮提示框

`AlertDialog` 用于展示通知并让用户明确表示已阅读。通过 `Tools/MUI/Create Alert Dialog Prefab` 创建模板，资源键使用 `AlertDialogView`，在宿主的提供方配置对应资源。契约包含 `Title`、`Message`、`AcknowledgeLabel` 三个 TextElement，以及 `Acknowledge` ButtonElement；正文可滚动，默认焦点为已阅读按钮。模板与确认框共用编辑器布局生成流程，保存前分别校验自己的 Manifest。

纯同步宿主可以直接调用：

```csharp
var alertRoute = MUI.UGUI.AlertDialog.CreateRoute(
    new MUI.Resources.ViewResource("AlertDialogView"));
var opened = host.Navigator.Open(alertRoute,
    new MUI.Dialogs.AlertRequest("提示", "背包已满，请先清理背包。", "知道了"));
if (opened.IsSuccess)
{
    opened.Handle.ObserveResult(ownerLifetime, result =>
    {
        if (result.IsCompleted)
        {
            // 用户明确点击了已阅读按钮；业务只在需要时响应。
        }
        // 返回、外部关闭与宿主退出是 Dismissed，不视为已阅读。
        // 如需在此处打开下一页，使用 PostOpen 避免重入当前导航事务。
    });
}
```

`ownerLifetime` 由业务所有者持有，纯同步项目使用 `LifetimeMode.Synchronous` 并同步 Dispose。订阅取消只撤销通知，不关闭提示框；页面所有权仍由 Navigator 管理。打开失败须检查 `opened.Status/Rejection/Error`，结果还应按业务要求检查 `Error/Cleanup`。

路由使用 `Unit` 类型结果：Completed 表示已阅读，Dismissed 表示未通过按钮完成。按钮使用 SynchronousCommand，等待用户操作无需任务或阻塞；异步宿主也能使用同一模板及路由。模态、输入屏障、关闭守卫和焦点恢复沿用导航器。该路由最多一个活动实例，重复打开拒绝，不借用他人的结果；不提供隐式排队。项目返回输入接到 `UIHost.RequestBack`，模板不把 Cancel 转成已阅读按钮点击。

新模板只完成离线编译与源码契约核对，尚未验证 Unity 生成 Prefab、焦点和运行输入行为。


同步关闭内部以直接状态登记开始及完成，不创建关闭 TaskCompletionSource；同步替换的 `SourceClose` 可直接读取，构造替换结果也不读取清理任务。只有调用者显式访问 `ReplaceOutcome.SourceCleanup` 才创建兼容任务，多次访问及结构体复制共用同一个完成信号。纯同步业务应读取 `SourceClose`，无须访问这个异步兼容属性。异步关闭仍保留逻辑完成与实际清理完成的独立信号，以表达清理超时。


局部返回的 `RegisterLocalBack` 通过激活令牌自动撤销，不向 Lifetime 的释放列表追加长期保留的记录。手动 Dispose、生命周期取消及 View 清理都会移除处理器、释放回调引用并撤销令牌登记。登记与派发仍在 Unity 主线程进行；后台取消只操作托管登记，不调用 Unity 对象接口。派发采用快照，并发撤销不会抢占已取得执行资格的回调；回调中的界面操作仍须遵守原有导航重入限制。


## 必需共享依赖

路由构造参数 `dependencies` 接收 `RouteDependency<TParentArgs>` 的不可变声明快照。`Required` 表示依赖必须在父页面提交前准备成功：

```csharp
var shared = new Route<StatusViewModel, StatusArgs, Unit>(
    "shared.status", statusResource, () => new StatusViewModel(),
    bindingFactory: StatusViewModelBindingFactory.Create,
    policy: new RoutePolicy(enterHistory: false, takesFocus: false),
    supportsSynchronousLifecycle: true);
var page = new Route<BagViewModel, BagArgs, int>(
    "bag", bagResource, () => new BagViewModel(),
    bindingFactory: BagViewModelBindingFactory.Create,
    supportsSynchronousLifecycle: true,
    dependencies: new[]
    {
        RouteDependency<BagArgs>.Required(shared,
            args => new StatusArgs(args.PlayerId))
    });
```

上例类型为项目示意；可编译示例见 `Samples~/Navigation/SynchronousDependenciesDemo.cs`。无参数依赖使用 `RouteDependency<ParentArgs>.Required(unitArgsRoute)`。共享目标必须是 `AllowMultiple=false`、`MaxInstances=1`、结果为 `Unit` 的路由。每个路由最多 64 个直接依赖，递归准备最多 64 层；参数工厂应无副作用，参数应视为不可变值。

同步与异步 Open/Replace 均在原导航事务内递归准备，不递归调用公开 Open 排队。依赖候选先取得父所有权再执行可失败准备；失败时由父回滚逆序释放。已准备或已打开的同一路由仅在参数相等时共享，不抢占正在关闭、换绑或更新参数的实例。异步能力不满足时同步入口返回 RequiresAsync；依赖参数冲突返回 ConflictingData，资源尚未驻留返回 RequiresPreload，不支持同步创建返回 SyncCreationUnsupported，清理隔离容量不足返回 CleanupCapacity，目标正在变更或退出返回 Busy；依赖成环与容量/深度超限分别返回 DependencyCycle、DependencyLimit。

默认 RequiredBefore 在父页面之前登记及激活，AttachedAfter 在父页面之后登记及激活；隐式依赖不进入独立历史，焦点与覆盖策略仍由依赖路由配置。后续显式打开同一依赖且参数相符会增加显式拥有关系，此后关闭所有父页面也保留该依赖。`GetDependencies(handle)` / `GetOwners(handle)` 返回独立关系快照，不改变所有权。

普通 Close/Complete/Back 和批量关闭遵守 InUse；父 EndActivation 完成后释放依赖，末父释放且没有显式拥有者时才自动关闭。强制关闭依赖会先收敛父链，异步父清理等待真实完成。回收错误纳入父清理结果。同步全过程不启动异步准备或清理任务。

当前限制：只接入必需依赖；可选依赖降级及编辑器的可视依赖图界面尚未实现；静态校验入口已提供。涉及依赖的参数更新在依赖参数保持一致时可执行，需要改写依赖时返回 DependencyChangeRequired；父页面模型换绑可保留现有依赖，正在被其他父页面持有的共享实例则返回 InUse。父 Ready 已合并必需依赖的首次就绪，见下文。以上接口已离线编译，真实共享、取消、强制关闭及动效时序尚未在 Unity 验收。


### 撤销显式持有关系

`ReleaseExplicitOwnership(handle)` 与 `ReleaseExplicitOwnershipAsync(handle, token)` 撤销用户显式打开形成的独立关系。`HasExplicitOwnership` 可直接查询；它与 `GetOwners` 的父页面集合分开计算。

仍有父拥有者时，撤销立即提交并返回 Released，`CloseOutcome` 为 null，页面、模型和实例级结果都保留。操作移除独立历史与焦点资格，并派发 ExplicitOwnershipReleased 生命周期事件；后续显式 Open 恢复该资格。不会因此隐藏依赖或解除其模态/覆盖策略，项目应为共享展示选择合适的 RoutePolicy。

没有父拥有者时，撤销请求走正常关闭守卫。同步生命周期不支持或守卫拒绝时返回 Rejected，原关系仍在；真正提交关闭时撤销关系，结果携带 CloseOutcome。异步取消只结束本次等待：尚未知道是否会提交时返回 Pending；已经提交关闭则返回 Released，但仍须检查清理状态。重复撤销返回 AlreadyReleased。`IsReleased` 只说明独立关系已撤销，不代表页面已消失或资源已清理。

保留实例的关系撤销不需要异步准备或任务；纯同步项目调用同步接口。涉及当前回调的重入、关闭中、换绑中、参数更新中等状态会明确拒绝。表现更新在提交后失败会保留 Released 并携带 Error，不把已经发生的撤销伪装成失败回滚。

示例新增“显式打开共享依赖并独立持有”“撤销共享依赖的显式持有”：显式打开后关闭两个父页面，共享界面应保留；再撤销显式关系应关闭。也可先在两个父页面仍打开时撤销，观察父拥有者数保持 2。接口与示例已离线编译，焦点、守卫、等待取消及 Unity 行为尚未运行验收。


### 必需依赖与父页面 Ready

表现重算在外部回调结束后，按依赖优先顺序记录就绪，再通过原表现快照统一发布通知。父页面自身激活及进入效果完成后，还要求所有直接依赖处于有效打开态且已经 Ready；依赖的 Ready 又覆盖其下级必需依赖。此判断只读直接状态，不创建任务，不按每个父页面重复等待同一依赖。

依赖进入效果发生可恢复降级时，父 Ready 同样标记 IsDegraded 并携带依赖错误；本页也有降级时合并两者。不可恢复的依赖失败在父链收敛前传递给尚未关闭的父页面，未 Ready 的父页面报告 ActivationFailed，而非无原因的提前关闭。

OpenAsync/ReplaceAsync 与实例复用等待这份完整 Ready。已提交后的调用者取消仅结束自己的等待、收敛本页进入效果，不跳过依赖就绪，也不提前结束共享依赖动画。此时可返回已提交的页面，但 Handle.Readiness 仍可能为 Transitioning；后续 ObserveReadiness/WaitUntilReadyAsync 继续反映真实就绪。纯同步宿主直接完成进入效果，依赖 Ready 判断和通知仍完全同步。

尚未在 Unity 运行多层依赖、不同进入时长、共享等待取消及故障级联；当前验证仅为源码路径核对与离线编译。


依赖准入拒绝由 Open、Replace 和 CloseOldest 的事务边界转为 Rejected，并保留 Error 中的具体说明及 Cleanup。拒绝会回滚本次已取得的候选和关系，不将预加载需求或参数冲突标记为生命周期故障；真正的模型、提供方、参数工厂或生命周期异常仍走 PreparationFailed。Replace.Rejection 与 Destination.Rejection 保留对应含义，超限替换再转换回 Open 时也不丢失依赖拒绝原因。

同步 CloseOldest 现在直接传递源清理的延迟结果存储，构造 OpenOutcome 不读取 SourceCleanup。业务读取 ReplacedClose 即可取得直接结果；只有显式读取 ReplacedCleanup 才创建兼容任务，同一结果及其结构体副本共享完成信号。


### 依赖位置与显示约束

`RouteDependency<ParentArgs>.Required(route, argsFactory, placement)` 的 placement 默认为 `DependencyPlacement.RequiredBefore`，也可选择 `AttachedAfter`。无参数依赖同样接受 placement。两种方式都属于必需依赖，不意味着后台懒加载。

| 位置 | 新候选准备顺序 | 提交和绑定激活顺序 | 同一 Layer 的显示位置 |
|---|---|---|---|
| RequiredBefore | 先准备依赖，再执行父准备 | 依赖在父之前 | 依赖在父下方 |
| AttachedAfter | 父准备完成后准备依赖，父仍隐藏 | 父在依赖之前 | 依赖在父上方 |

父页面和整棵必需依赖树全部准备成功后才能开始提交。相同位置内按声明顺序处理；每个声明的参数工厂只在所属阶段解析一次。已经打开的共享依赖只增加拥有关系，不重放其生命周期。父 Ready 仍等待全部必需依赖，与位置无关；关闭时仍先结束父生命周期再释放依赖。

Layer 是显示硬边界。不同 Layer 的依赖继续服从 Layer，同层才通过依赖约束稳定排序；没有约束的项目沿用原显示顺序。共享实例同时承受多个父页面的约束，不能通过简单“移到父节点后面”实现。框架在取得关系前检查同层显示图；成环或同一父页面对同一依赖声明不同位置时返回 Rejected/DependencyOrderConflict，并回滚当前打开候选。

纯同步共享示例的 Shared Placement 在启动前选择位置，共享状态与父页面位于同一层，可观察相对显示顺序。接口及示例已离线编译，同层显示、共享冲突、替换和 Unity 生命周期顺序尚未运行验收。


### 静态路由图校验

`RouteGraphValidator.Validate(rootRoute)` 返回只读结果，可由项目启动检查、编辑器工具或构建校验调用：

```csharp
var validation = RouteGraphValidator.Validate(rootRoute);
foreach (var issue in validation.Issues)
{
    // issue.RouteKey、issue.Rejection、issue.Message 可用于编辑器诊断。
}
```

校验覆盖从根可达的路由键冲突、共享目标单实例约束、重复位置声明、所有权环、同层显示环及最长路径超过 64 层。问题默认最多记录 64 项，超出时 IsTruncated 为 true；IsValid 为 false 时不能把部分诊断误当作完整问题列表。RouteCount 表示本次发现的不同路由定义数量。

Route.DependencyDescriptors 提供不含参数工厂的只读目标/位置信息。校验不会创建 VM、Presenter、绑定或 Prefab，也不会执行参数工厂；因此无法判断 `default` 与 `other` 这样的运行时参数冲突，或发现两个分别合法的根路由同时打开后才产生的共享显示冲突。运行时所有权检查仍保留。

Navigator 在首次登记一张不可变路由图时先校验，再检查图中全部键是否与宿主现有定义一致；全部通过后登记，失败不留下半张定义表。已登记的同一不可变定义直接复用。Open/Replace 的校验错误走类型化 Rejected；预加载沿用其注册阶段的异常报告契约。

共享示例提供“校验共享示例的静态路由图”菜单。参数冲突示例本身静态合法，因为校验刻意不执行业务参数工厂。校验器和示例已离线编译，未新增测试；编辑器可视图界面及 Unity 实际操作尚未验收。


### 带依赖页面的模型换绑

`Rebind` / `RebindAsync` 可以为持有依赖的父页面换绑新模型。此操作不改变 Route、Args、依赖参数或拥有关系，不重跑依赖参数工厂，也不重新创建共享界面；失败恢复与错误关闭仍沿用原有换绑事务。

若被换绑对象本身正在被其他父页面持有，则返回 RebindRejection.InUse，防止未经拥有者协同替换共享模型身份。换绑期间的有效性检查同时确认共享资格和直接必需依赖仍有效；依赖发生不可恢复失败时，原有父链关闭机制仍负责取消换绑并释放。

参数更新需要另一套依赖事务。当前共享目标的 UpdateArgs 返回 InUse；父页面只有在新参数要求改写依赖时才返回 DependencyChangeRequired，拒绝发生在业务参数更新器执行前。依赖参数保持一致的父页面和普通页面使用既有参数更新流程。这个限制不等于依赖参数更新已完成。

共享示例增加“同步换绑第一个父页面并保留共享依赖”：预期 Applied、父句柄保持不变、共享创建次数及拥有者数量不变。“尝试换绑被共享的依赖模型”预期 Rejected/InUse。示例及依赖已离线编译，实际显示、恢复失败和并发退出尚未在 Unity 验收。


### 更新父参数并保留依赖

同步与异步 UpdateArgs 在执行父参数更新器之前，根据新父参数解析每条依赖声明，并用依赖路由的 ArgsEqual 比较当前依赖参数。全部一致时继续原有提交/回滚事务，依赖句柄、模型、资源和拥有关系均保持同一份，不重新加载依赖。

参数工厂与比较器在导航回调保护中执行，每条声明在此次检查只解析一次；返回后复核父页面和依赖的直接存活状态。依赖工厂抛错仍是 PreparationFailed，依赖失效返回 SourceUnavailable，新参数需要不同依赖参数则返回 DependencyChangeRequired。正在被其他父页面持有的目标本身仍拒绝参数更新并返回 InUse。

该能力要求依赖参数按不可变值使用，工厂和相等比较器无副作用。真正更换依赖参数及候选所有权的更新事务仍待实现，不会为了接受一次父更新而改写共享实例。已保留的依赖在父关闭前受 InUse 保护；强制退出会取消父参数操作，沿用既有清理流程。

共享示例改用字符串父参数和标题更新 Presenter。“同步更新父标题并保留依赖参数”预期 Applied，共享创建次数及拥有者数不变；“尝试通过父参数改写共享依赖”预期 Rejected/DependencyChangeRequired，保留此前标题及参数。当前仅离线编译，Unity 提交、回滚和退出时序未验收。

## EventSystem 与平台返回输入

`UIBackInput` 是可选的 uGUI 输入适配组件，不新增输入系统依赖。把它挂在需要将 Cancel 转给页面的可选中控件上，并显式设置 `Host`。EventSystem 不会自动把 Cancel 冒泡给父节点，所以仅挂在 Canvas 上不能接收子按钮的取消事件。键盘/手柄的 Cancel 映射由项目正在使用的输入模块配置。

组件仅在接收事件的同一帧 LateUpdate 转发尚未消费的 Cancel，并记住接收时已消费的状态，防止输入模块重置复用事件后误派发；焦点改变、组件禁用或宿主失效会丢弃待处理事件。现有 DialogCancelInput、菜单取消处理器先执行，已消费的事件不会继续返回页面。项目自己的局部取消处理器应调用 eventData.Use；如果还会同时通过全局输入动作派发返回，应在执行局部动作前使用 BackInputConsumption.TryConsume 合并同帧输入。

输入动作或平台返回回调也可在主线程的按下阶段连接 `UIBackInput.RequestBack()`，由同一个延后入口转发。组件不自行监听 Android 返回键或设置 InputAction，也不处理应用退出；平台接线仍由项目完成。同步宿主沿用直接 Back 调用，不创建任务。

不要给原生输入框或下拉框直接添加通用返回适配器：它们可能用 Cancel 结束编辑或收起列表，却没有参与框架的消费标记。需要由项目的控件取消适配明确区分“结束编辑”和“返回页面”，避免一次操作同时关闭页面。当前组件不代表完整平台输入协议或软键盘适配已经完成。
