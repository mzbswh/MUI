# 命令与绑定

命令、来源上下文和绑定级取消已经实现。当前设置页直接驱动绑定；Navigation 示例已通过 ViewInstance 接入来源关闭和类型化结果。

## 声明与生成

```csharp
[Command(CanExecute = nameof(CanSave))]
[BindCommand("Save", nameof(ButtonElement.Clicked))]
private async ValueTask SaveAsync(CommandContext context)
{
    context.Apply(() => Status = "Saving...");
    await service.SaveAsync(context.Token);
    context.Apply(() => Status = "Saved");
}

private bool CanSave => HasChanges;
```

生成 `SaveCommand`，并在 BindingContext 中连接 ButtonElement.Clicked、CanExecute 和解绑动作。方法可以返回 void（仅同步）、Task 或 ValueTask，参数可为 CommandContext、CancellationToken、两者或无参数。async void、含其他参数、泛型命令和静态命令会产生 MUI001 诊断。

默认属性名为“去除方法 Async 后缀 + Command”；`[Command("ApplyCommand")]` 可指定名称。CanExecute 可以指向当前 VM 上的 bool 属性或无参 bool 方法。绑定期间 VM 属性变化会触发 CanExecute 刷新；不绑定到 View 时可以手动调用 NotifyCanExecuteChanged。

也可以手工声明稳定的 IUICommand（异步）或 ISynchronousUICommand（同步）属性并添加 BindCommand。命令对象身份在一次绑定期间保持不变，若要替换对象应重新绑定。属性绑定使用 Bind，事件命令绑定使用 BindCommand，不混用两种契约。

生成器会拒绝同一声明中重复绑定一个控件事件，以及命令 CanExecute 与普通绑定同时写同一个 Interactable 属性。业务可执行条件应合并到 CanExecute，而不是让两个绑定竞争控制按钮。

## 纯同步命令与绑定

void 命令方法现在生成 `SynchronousCommand`，Task/ValueTask 方法继续生成 `AsyncCommand`。同步方法不再包装成返回已完成 ValueTask 的异步命令。同步命令只支持立即执行和拒绝重入；为 void 方法配置其他 Concurrency 或显式 Capacity 不等于 1 时，生成器报告错误，省略 Capacity 则按单次执行处理。

```csharp
[Command]
[BindCommand("Refresh", nameof(ButtonElement.Clicked))]
private void Refresh(CommandContext context)
{
    context.Apply(() => Title = "已刷新");
}

// 生成属性可直接同步调用，无需 Task 或 await。
// var outcome = viewModel.RefreshCommand.Execute();
```

`ISynchronousUICommand` 只包含同步 Execute 和公共状态契约 `IUICommandState`，自定义同步命令无需实现任何异步方法。绑定器在订阅事件前检查执行能力：同步模式拒绝只实现 IUICommand 的命令；兼容异步模式支持两种命令，同时实现两种接口时优先同步执行。仅声明 IUICommandState 的属性不满足生成器的执行契约检查。

同步命令沿用 CommandContext 的来源、取消、Apply、RequestClose/Complete 和 CommandOutcome。执行和通知在创建线程直接完成，不 Post、不排队；执行、资格求值或状态通知回调内再次执行同一命令返回 Reentrant。取消请求在进入业务前和业务返回后复核；正常发出关闭请求后直接结束命令，不再要求已关闭的来源仍有效。共享命令的全局 Cancel 与绑定来源取消保持原有区别。

标准生成绑定可显式选择纯同步会话：

```csharp
var binding = BindingRegistry.Create(view, viewModel);
binding.SetLifetimeMode(LifetimeMode.Synchronous);
binding.Bind();
// 宿主提交状态后再开放源写入与命令。
binding.CommitSourceWrites();
binding.Unbind(); // 或 Dispose，同步退订并释放会话资源
```

设置模式只允许在未绑定且清理已结束时进行。同步会话的命令 Lifetime 禁止登记异步工作；绑定失败回滚、解绑和冻结后的清理均走同步释放。自定义 BindingContext 默认不声明同步能力，设置同步模式或调用 Unbind 会明确拒绝，不能调用 UnbindAsync 试探完成状态。

`Unbind` 在构建绑定、冻结、正在清理或有在途异步命令时于修改状态前拒绝；已结束清理可重复观察相同结果。同步 `Rebind` 已改为直接清理旧会话、建立候选和失败恢复，不再调用 RebindAsync。旧会话含异步命令时，即使命令当前空闲也会在退订前拒绝；候选及恢复会话只接受同步命令，避免源写入或就绪回调意外启动异步命令。混合命令绑定使用 RebindAsync。两条换绑路径共用绑定、源提交、代际复核和清理结果发布，所有者的显式解绑始终优先于恢复。

兼容性变化：void 方法生成的属性类型由 AsyncCommand 改为 SynchronousCommand，直接调用时使用 Execute；需要异步并发语义时使用返回 Task/ValueTask 的方法或显式 IUICommand 属性。绑定同步模式约束绑定会话与命令；视图属性设置器触发的子视图/模块准备仍由宿主能力检查。Navigator、ChildViewScope 和 Tab 已提供完整同步生命周期入口，但页面仍须显式声明同步能力，并满足资源、绑定、子树和 Presenter 契约，不能仅设置绑定模式就认定整页无异步。

## 异步执行、并发与错误

| 策略 | 行为 |
|---|---|
| RejectWhileRunning（默认） | 有执行中的调用就拒绝新调用 |
| RestartLatest | 接受新调用后使旧版本失效，并请求取消旧执行；旧结果不能通过 Context.Apply 回写 |
| Queue | 串行执行；排队支持取消，到达队首重新检查 CanExecute |
| Parallel | 允许多次执行，各次拥有独立令牌和来源上下文 |

Capacity 默认 32，限制执行中与排队中的调用总数。RestartLatest 下不合作的旧调用也计入容量，不能用“取最新”掩盖无限后台工作。容量耗尽返回 Rejected，不增加新任务。

Queue 命令在自己的活动执行链中再次调用自身，会立即返回 Rejected，Rejection 为 Reentrant，不进入队列。这同时覆盖直接递归和 A → B → A 的嵌套等待，避免持有队列的调用等待自己释放队列。已经结束的执行上下文不阻止后续调用；普通容量或资格拒绝标记 Unavailable。此规则也拒绝同一执行链内不等待结果的自排队；需要重复执行时，在当前调用完成后由外部调度。它不是通用任务等待图，不能检测独立执行链之间任意互相等待。当前仅编译检查，嵌套异步路径运行验收仍待完成。

每次 ExecuteAsync 返回 CommandOutcome：Succeeded、Rejected、Cancelled 或 Failed。Error 保存最新被接受调用的错误状态；每次调用自己的 Outcome 始终包含该次失败。IsExecuting 表示仍有尚未收敛的调用，包括排队/已请求取消但未结束的任务。

接受调用后先登记执行和队列位置，再通知 IsExecuting/CanExecute/Error，最后进入业务方法。同步完成的命令同样有开始和结束通知；纯同步业务不会因此被强制推迟到下一帧。开始通知中的取消或来源关闭会在执行前被复核，排队顺序也不会被通知回调中的新调用抢占。通知表示当前共享状态，不是不可变的逐次执行日志。此通知顺序修复已编译通过，重入与取消路径仍待运行验收。

绑定发起的错误通过 UIErrors 上报，基础层不引用 Unity 日志。诊断订阅者异常被隔离。命令状态通知也逐个隔离订阅者异常，确保其他视图仍有机会刷新。

## 来源与回写

共享 VM 上的命令不保存某个 View 的 Handle。绑定上下文在 Bind 前通过 SetCommandTarget 取得当前视图的 ICommandTarget，每次调用将其传入 CommandContext。

```csharp
[Command]
[BindCommand("Confirm", nameof(ButtonElement.Clicked))]
private void Confirm(CommandContext context)
{
    context.Complete(new SelectionResult(SelectedId));
    // 完成请求后结束处理，不等待自身绑定的关闭清理。
}
```

RequestClose 和 Complete 发出非等待请求。ICommandTarget 的宿主实现必须验证激活代际与结果类型，ViewInstance/Navigator 已实现这一桥接。没有来源目标的命令调用这些入口会明确失败，不猜测“当前全局窗口”。

命令在 UI 线程发起，await 默认保留调用上下文；AsyncCommand 本身不是主线程调度器。异步业务完成后使用 Context.Apply 或 ThrowIfInvalid 检查版本、取消和来源存活。框架无法阻止任意第三方代码绕过这些入口直接修改共享 VM，也不能撤销网络交易。

## 解绑与换绑

UnbindAsync 先同步停止事件输入并发出该绑定的取消，再等待命令收敛。一个视图解绑只取消该视图传入的令牌，不调用共享命令的全局 Cancel，因此不会取消其他视图发起的并行调用。

清理期间状态为 Unbinding；重复 UnbindAsync 共享同一个完成结果。任务不合作时保持清理待完成，不能把相同绑定对象立即拿去复用。同步 Rebind 在旧会话含异步命令或存在命令任务时拒绝，使用 RebindAsync 等待旧执行结束再换绑。失败时恢复原绑定关系；业务 setter 和服务副作用不属于通用回滚范围。

命令不得 await 自身绑定的 UnbindAsync/DisposeAsync。关闭请求使用 Context.RequestClose/Complete 后直接返回，避免关闭流程等待命令而命令又等待关闭。

## 线程约束

`AsyncCommand` 在所属 UI 线程创建，并记录当时的 SynchronizationContext。`CanExecute` 的业务谓词求值和 `ExecuteAsync` 必须在创建线程调用，线程不符时会在访问来源或业务回调前抛出 InvalidOperationException。排队请求在等待前创建 CommandContext；等待恢复不会重新定义合法线程。

`CommandContext.IsCurrent`、`Apply`、`RequestClose` 和 `Complete` 同样检查原调用线程。后台计算应先切回 UI 线程，再用 Apply 检查取消与调用代际后写入表现状态；命令不会自动派发业务委托。

后台完成或 NotifyCanExecuteChanged 产生的属性通知通过创建时的上下文 Post 回 UI 线程。派发后再次检查线程，只通知属性名，由观察者读取最新值。上下文缺失、Post 失败或回调仍落在错误线程时，通过 UIErrors 报告诊断并放弃该次通知，不能在后台运行绑定观察者。这种配置不具备可靠 UI 通知能力，宿主应在安装 UI 同步上下文后创建命令。

本次为离线编译和调用路径核对，尚未执行 Unity 内后台续接与界面销毁竞态验收。

## 绑定命令不能等待自身换绑

标准 BindingContext<TViewModel>.RebindAsync 会先撤销旧订阅、取消并等待旧会话命令，再建立新绑定。因此由该会话触发的命令不能等待同一会话换绑，否则会形成“命令等待换绑、换绑等待命令”的环。当前在任何退订、取消或模型切换之前拒绝此调用，原绑定保持有效。传入同一个模型仍为无操作；同步 Rebind 原有的活动命令检查保持不变。

BindingBuilder 按 BindingSession 身份记录同步与异步命令执行链，检测包含嵌套调用的祖先会话，不把共享 VM、共享命令或相同导航目标当作会话身份。其他会话可按既有规则换绑并等待它自己的命令。执行标记在命令结束后失效并清除会话引用，已完成命令遗留的异步上下文不会继续判为活动命令。此保护覆盖标准绑定触发的命令链，不承诺识别项目任意后台任务之间的等待环。

需要换绑时，由界面宿主在命令返回后发起；Task.Yield 或另起异步任务不保证命令已经结束，不能用作可靠的规避方式。完整页面换绑使用 Navigator.RebindAsync 或 ChildViewHandle.RebindAsync，统一协调 Presenter.OnViewModelChanged、绑定和模型所有权；底层 BindingContext.RebindAsync 不替代这些入口。导航在入队前为源实例登记换绑占位，源命令在该占位存在时不能再发起排队导航，避免旧命令等待队列、换绑等待旧命令的环。当前仅源码检查和离线编译，未新增测试，Unity 重入与异步运行验收仍待完成。
