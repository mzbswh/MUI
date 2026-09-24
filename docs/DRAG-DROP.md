# 拖放会话与提交结果

`MUI.DragDrop` 是只依赖 Core 的标准模块。`DragSession<TPayload>` 管理一次拖放和至多一次业务提交，`DropTarget<TPayload>` 提供目标 Lifetime 与异步提交函数；它们不引用 Unity，也不建立全局窗口栈。

```csharp
var drag = new DragSession<ItemDragPayload>(payload, sourceLifetime, pointerCapture, result =>
{
    // 由项目按结果收尾视觉：成功刷新数据；拒绝/取消/失败恢复拖拽前显示。
    FinishDragVisual(result);
});

var target = new DropTarget<ItemDragPayload>(targetLifetime, async (item, cancellation) =>
{
    return await TryMoveItemAsync(item, cancellation);
});
DropResult result = await drag.DropAsync(target);
```

Payload、捕获对象和业务方法由项目提供。Payload 应使用稳定业务 ID/版本与不可变数据，不用场景对象引用冒充业务身份。`pointerCapture` 必须是可幂等释放的适配器对象；构造成功后由会话接管，构造失败时仍由调用者负责。Source Lifetime 自动拥有会话，最终清理会请求取消并等待在途提交收敛。

## 状态与所有权

```text
Dragging → Dropping → Completed
Dragging ──取消────→ Completed
```

- 创建和公共调用在所属 UI 线程执行，必须提供有效 UI SynchronizationContext（默认当前上下文）。显式 uiContext 必须能将 Post 调回同一 UI 线程。取消来自其他线程时，将结束显示的工作投递回该上下文；若投递失败或落在错误线程，记录诊断并在下一次所属线程 Pump、显式 Cancel 或正常清理时收敛取消。uGUI 来源每帧调用 Pump。
- 异步提交若在其他线程完成，最终结果先暂存于会话，再请求 UI 线程派发；派发失败或落在错误线程时由下一次 Pump 收尾。会话销毁会先等待该提交函数退出，再排空已到达的结果。项目自定义上下文仍须保证常规 UI 回调能回到所属线程。
- Source 取消立即取消会话令牌。尚未投放时会结束会话并释放捕获；正在提交时捕获已释放，等待业务确认后再结束显示。
- DropAsync 首次调用决定目标。之后的调用共享同一 Completion，不向另一个目标重复写入。
- 开始提交前先释放 pointerCapture；释放失败时不执行业务提交，返回 Failed。取消和最终清理也只尝试释放捕获一次，清理异常报告 UIErrors。
- 提交用 Source/会话和 Target Lifetime 的关联令牌，并通过 Target Lifetime.RunAsync 跟踪目标使用。目标清理必须等待该操作，不能提前释放提交依赖的资源。
- 完成回调 `settleVisual` 只调用一次；异常报告 UIErrors，不重写业务结果。回调必须检查其原生节点是否仍有效，不能在源销毁后访问 Unity 对象。
- Commit 或视觉收尾回调不得等待同一个 DragSession.DisposeAsync/DropAsync，也不得等待自己的 Source/Target Lifetime 清理。会话直接自等待有拒绝检查；业务跨对象循环等待不自动检测。

## 提交契约

目标函数返回 true 表示业务已经确认提交；false 表示没有提交。结果分别为 Committed、Rejected、Cancelled 或 Failed（携带异常）。

取消不是业务回滚。函数已经返回 true 时，哪怕取消同时到达，仍保留 Committed。网络请求必须由业务层用幂等请求 ID、版本校验或状态查询解决“不知道服务端是否成功”的情况，不能抛一个超时异常就假定服务端未写入。

失败、拒绝和取消时，显示适配器应恢复拖拽前的布局/显示并移除拖拽影子；成功时从已确认的数据重新呈现。核心模块通过结果回调确定收尾时机，不自动回滚项目领域数据。

不合作的提交会让 DisposeAsync 持续等待；当前没有超时隔离。Source Lifetime 会保留已登记的会话至清理，长寿命宿主应使用任务/交互级生命周期管理，避免无限登记短会话。

## 当前实现边界

当前已编码语义载荷、单次提交、Lifetime 关联取消、目标操作跟踪、捕获释放所有权与结果收尾协议。Unity 原生事件适配及 EventSystem 拖拽归属见下文；OS/跨窗口 Pointer capture、自定义物品移动恢复和编辑器模板尚未实现；目标悬停反馈见下文；标准图标影子见下文。不可把 IDisposable 捕获契约视为某个 Input System 已实现的指针捕获。

DragDrop/Core 离线编译通过；尚未进行 UI 线程派发、取消竞态、业务异常、真实拖放和销毁的运行验收。


## uGUI 事件与绑定

在源节点添加 DragSourceElement，在目标节点添加 DropTargetElement。两者可和其他类型 Element 共存，绑定声明通过各自类型与名称解析。

```csharp
sourceElement.Source = new DragBinding<ItemDragPayload>(sourceLifetime, () => currentPayload);
targetElement.Target = new DropBinding<ItemDragPayload>(
    new DropTarget<ItemDragPayload>(targetLifetime, TryMoveItemAsync));
```

两端的 TPayload 必须相同，不匹配的目标不消费投放；随后原生 EndDrag 取消会话。数据工厂只在实际 BeginDrag 时执行。源和目标必须具备项目配置好的 Graphic 射线区域与 EventSystem/InputModule，框架不全局扫描拖拽目标。

源实现 BeginDrag/Drag/EndDrag，目标实现 Drop。一次源交互只绑定一个 pointerId，同一源在异步提交结束前不接受另一次拖拽；目标默认最多 16 个在途提交，拒绝重入接收。输入受 Selectable 状态与所属 View 门控约束。

`PointerDragCapture` 只拥有对应 PointerEventData 的 pointerDrag/dragging 状态；成功开始投放或取消时清除仍指向本源的归属，不更改其他指针或其他对象的归属。它不是平台 OS capture，不承诺窗口外移动、跨 InputModule、输入设备断开和原生捕获丢失已处理。

源禁用或销毁时取消会话；目标禁用、销毁、换绑时取消仍在处理的投放。View/Selectable 门控变化在每帧推进时也会检查并取消。目标的容量在实际提交收敛前不释放，因此不合作任务不会允许无限继续提交。挂起任务仍可能阻碍所属 Lifetime 清理，超时隔离未完成。

源提供 Dragged(screenPosition) 与 Finished(DropResult) 事件，项目可建立影子跟随与显示恢复。源换绑时先取消旧会话，旧结果不发送到新绑定；销毁后不执行事件监听。动态视觉必须由原绑定自己的 Lifetime/资源凭证管理，不能只依赖仍存活组件的 Finished 回调清理。

适配器当前不移动原始物品、改父节点、隐藏原图或创建影子，因此失败也不会擅自更改这些作者状态。可选的标准图标影子见下文；自定义物品移动恢复仍由项目管理。

UGUI、DragDrop 及相关 Editor/TMP 程序集已离线编译；真实输入模块的事件顺序、指针归属清理、换绑、失效和视觉收尾尚未运行验收。


## 可选图标影子与视觉收尾

DragSourceElement 的 Drag Icon 配置源节点下的 Image，Ghost Root 配置同一个 root Canvas 下、源节点之外的 RectTransform。两者都不配置时维持无影子模式；只配置一个会被拒绝。Ghost Root 的层级排序由项目决定，通常位于内容区域之后；不要将原物品放进影子根中。

开始拖拽时创建一个独立 Image 影子，复制图标当前 overrideSprite、颜色和 Preserve Aspect，按源图标尺寸换算到目标根坐标。影子居中跟随屏幕指针，不复制业务脚本、事件、原对象层级或自定义材质。LayoutElement.ignoreLayout=true 避免父布局重新定位影子；CanvasGroup 和 Image 都关闭射线，避免遮挡目标命中。

Overlay Canvas 使用屏幕坐标转换；Camera/WorldSpace Canvas 需要明确的 rootCanvas.worldCamera。影子保持轴对齐，不复刻原图标旋转、Sliced/Filled 模式或复杂特效；无法投影到目标平面时暂时隐藏。

PointerDragCapture 释放时同步隐藏影子并安排 Destroy，包括投放开始、取消和初始化失败。源换绑、禁用、销毁以及最终结果收尾也执行幂等清理，并用交互代际避免旧回调删除新影子。异步提交等待期间影子已移除，不无限停留在屏幕上；需要提交进度时可使用 Loading。

原物品的 Transform、父级、布局与颜色都未被移动/隐藏，因此失败、拒绝或取消后原显示仍在原位，视觉恢复通过撤掉影子完成。成功后由业务数据绑定更新物品；业务数据失败回滚不是影子组件的职责。

影子借用 Sprite 引用，不自动创建资源 Lease；项目必须让图标资源在拖拽期间有效。需要跨页面释放资源仍保留拖影、动态图集或自定义渲染时，应提供独立资源所有权的视觉适配。

View 结构检查已覆盖引用成对、Icon/Root 层级、已知 Canvas 不一致、相机缺失及目标容量。源码已编译检查，真实图标显示、坐标、资源失效和清理尚未在 Unity 验收。


## 接收条件与目标反馈

DropTarget 构造函数增加可选 `canAccept`：

```csharp
var target = new DropTarget<ItemDragPayload>(targetLifetime, TryMoveItemAsync,
    canAccept: item => CanPlaceItem(item.ItemId));
```

条件必须同步、廉价、无副作用，不发起网络请求、不修改 UI、不取消自己的会话。默认条件为 true。纯会话可通过 `drag.CanDrop(target)` 查询；重复/递归条件查询返回不可接收，条件内尝试提交或等待会话清理会被拒绝。实际 DropAsync 再次求值，拒绝时返回 Rejected 且不执行业务函数；条件异常返回 Failed。最终业务提交仍须校验权限、容量、版本等真实规则，悬停允许不是业务提交保证。

DropTargetElement 记录最多 16 个悬停指针。源拖拽有效、类型匹配、双方可交互、目标容量未满并且 canAccept 成立时，`IsDropAllowed` 为 true；原生指针退出、目标失效、换绑或禁用时撤销反馈。多指针场景下它表达“至少一个悬停拖拽可接收”，不是每个指针各自的显示状态。

可指定 Allowed Highlight 为目标内的装饰子节点，组件随 IsDropAllowed 控制 active 状态。高亮不应包含 Selectable，所有 Graphic 应关闭 Raycast Target，或用 CanvasGroup.blocksRaycasts=false 防止亮起后改变命中。结构检查会检查这些配置；业务也可监听属性变化呈现其他反馈，但不要与组件同时写高亮节点的 active 状态。

悬停条件每帧刷新，使用复用的最多 16 项快照避免通知重入修改正在遍历的集合。条件抛出异常时清空本轮悬停并报告 UIErrors，退出重进后才重试，不持续每帧报告相同异常。

本次条件与反馈代码通过编译；运行时多指针、规则变化、射线和回调重入尚未在 Unity 验收。


## 程序集接入

业务使用 DragSession/DropTarget 时引用 `MUI.DragDrop`；使用 DragSourceElement、DropTargetElement、DragBinding、DropBinding 时额外引用 `MUI.UGUI.DragDrop`。这些控件仍使用 `MUI.UGUI` 命名空间，但不再位于基础 UGUI 程序集中。项目直接使用基础 View/Element 时应继续引用 `MUI.UGUI`。

拖放校验位于独立 `MUI.UGUI.DragDrop.Editor`，通过编辑器初始化注册。移除拖放能力时应同时移除模块及其运行时、编辑器适配；同一 UPM 包中的程序集不会因没有项目引用而自动停止编译。已有脚本元数据随文件迁移，Unity 重新导入和 Prefab 引用仍需实际验收。


## 指针按钮流与输入模块复用

uGUI 来源现在使用输入模块对象身份、pointerId 和 InputButton 共同识别拖放。另一个鼠标键的 Drag、EndDrag 或 Drop 不会结束当前会话；输入模块停用或替换时，来源在更新阶段取消自己的旧会话。真实拖放开始要求事件来自当前有效输入模块。

悬停不再保存 PointerEventData 供后续帧读取，而是保存不可变指针身份，向活动来源注册表查询可投放状态。注册表按按钮流保留一个正在开始或拖动的来源，投放转入异步业务后立即释放指针登记，最终回调按代际处理。关闭、取消、销毁及进入新的运行会话会清理对应登记；业务 CanDrop 回调后重新检查来源、代际和登记，防止重入后显示旧提示。旧 PointerInputModule 为鼠标键使用 -1/-2/-3，查询会做按键 ID 对应；新 Input System 共用指针 ID，不做该转换。

这项调整根据本机 uGUI 1.0.0 的 StandaloneInputModule 与 Input System 1.14.2 的 InputSystemUIInputModule 源码核对：新模块在同一个事件对象上依次拷贝左、右、中键状态。因此捕获释放必须复核模块/ID/按键和来源，不能在事件对象已切到另一个键时清空它的 pointerDrag。身份不匹配时只完成本会话视觉与业务收尾，不改写其他键的状态。

仍需区分逻辑拖放结束与底层输入模块捕获：后者可能把按键状态保存在内部副本中，修改缓存事件不保证同步改写那些副本。这不是 OS 指针捕获，也不是完整关闭手势消费协议；关闭后任意游戏输入轮询仍需统一输入路由。当前完成源码核对及离线编译，多鼠标键、触摸、模块切换、真实悬停与投放竞态尚未在 Unity 运行验收。
