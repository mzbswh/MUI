# 锚点浮层：Tooltip / ContextMenu 的显示基础

`ContextMenuController` 在每次菜单显示时自动向最近的 View 登记局部返回处理器，因此通过 `UIHost.RequestBack` 或 Navigator 返回时，先关闭菜单再考虑页面。登记使用独立纯同步 Lifetime，隐藏、禁用、失去可交互资格或本次打开失败时撤销；重新显示重新登记，不在页面生命周期中积累旧菜单登记。独立于导航的菜单继续支持 EventSystem 局部取消。

菜单位于嵌套 View 内时，在 Inspector 的 `Back Navigation View` 指定所属顶层导航 View；目标必须是菜单的祖先。菜单内部的 `ContextMenuItemInput` 与 `UIHost.RequestBack` 共用 EventSystem 帧消费标记，同帧第二次派发会被拒绝；跨帧重复及第三方输入适配仍需项目统一接线。原生输入和焦点恢复尚未运行验收。

现有 ViewContractValidator 同步检查显式返回目标的祖先约束，错误引用会出现在 Prefab 校验结果中；留空仍使用最近的 View，不作为缺失引用报错。

`AnchoredOverlayElement` 为已有 View 或子视图提供锚点跟随、边界避让与关闭入口。它没有自己的全局窗口栈，不调用 Navigator.Open，不持有外部目标或投影 Handle。

## Prefab 布局

```text
Page / Overlay View
└── OverlayBounds                RectTransform + AnchoredOverlayElement
    └── Content                  直接子 RectTransform，指定为 content
        └── 实际提示／菜单内容
```

OverlayBounds 的 Rect 为可用区域，可在上层接入 SafeAreaFitter。它应保持激活，且不要拦截射线；Content 由适配器控制显隐和局部位置。Content 布局根要求单位缩放和无旋转，动效应作用于内部子节点。父 LayoutGroup 不得同时控制 Content 的位置。

设置 `Anchor` 为目标 RectTransform；目标必须与浮层处于同一个 root Canvas，且不能位于浮层 Content 子树内。传入 null 隐藏并解除目标，不触发业务 Dismissed。每个 LateUpdate 根据目标世界角点换算到边界局部坐标，重新计算位置，支持目标移动、边界变化和当前内容尺寸变化。

## 放置协议

`AnchoredOverlayPlacement.Calculate` 是独立计算入口，接受目标矩形、浮层尺寸、边界、首选方向和间距。

- 首选 Below/Above/Left/Right；比较首选和相反方向的越界距离，越界更少时翻转；相同时保留首选。
- 再沿边界夹取位置；边缘 padding 从 OverlayBounds 中扣除。
- 内容尺寸大于边界时居中，不静默缩放。项目应通过最大宽高、换行或 ScrollRect 限制内容。
- 目标旋转时使用其在边界坐标中的轴对齐包围盒，不模拟任意形状的边缘。
- 不自动提高 Canvas sortingOrder，也不改变 sibling 顺序；项目通过现有宿主层级安排菜单位置。

## 输入与关闭

```csharp
// Presenter 的返回处理示意；需配置 Route.BackBehavior = HandleByPresenter。
public BackResponse HandleBack()
{
    return overlay.HandleBack() ? BackResponse.Handled : BackResponse.Close;
}

// 由已有指针路由传入屏幕位置和正确的 Canvas 事件相机。
bool consumed = overlay.HandleOutsidePointer(pointerPosition, eventCamera);
```

上述变量由业务持有。屏幕 Overlay Canvas 的 eventCamera 为 null；Camera Canvas 使用该 Canvas 的事件相机。不在组件中轮询旧 Input API，因此不会偷偷绑定特定 Input System。

只有浮层当前可见且所属 View 允许输入时，返回／外部点击才生效。点击 Content 或 Anchor 内部不关闭；命中外部时隐藏并返回 true。路由必须消费这个输入，不能把同一次点击继续交给底层按钮。同 View 背景点击可由下述 OverlayDismissArea 接入；自动全局指针路由、Pointer capture 和跨手势屏障尚未在此组件实现。

目标销毁、失活或移动到不同 root Canvas 时，组件隐藏内容并发出 `Dismissed(TargetUnavailable)`；返回和外部点击分别携带 Back、OutsidePointer。订阅者通过原有 Presenter／投影所有者发起清理，禁止直接丢弃其 Handle。组件事件不等待异步清理；项目应使用已有 Lifetime 跟踪清理任务并处理异常。Dispose 只断开目标、清除事件、隐藏借用内容。

目标仅移出屏幕或被 Mask 裁剪，尚不视为失效。锚点换绑有版本检查，避免显隐回调里的新目标被旧调用重新显示；重入期间新布局可能在下一次 LateUpdate 才完成。

## 当前边界

当前是 Tooltip / ContextMenu 共用的显示基础，尚不是完整业务服务。延迟悬停与键盘选中触发已由下述 TooltipTrigger 提供；单层按钮菜单导航与条件焦点恢复由下述 ContextMenuController 提供；仍需补齐动态菜单数据、子菜单、异步投影准备提交、自动全局外部点击路由；原生 Prefab 模板向导见下文。Content 激活前的最终布局测量与首帧 readiness 也尚未验收，不能把 LateUpdate 跟随当成首帧无跳动保证。

相关 UGUI、TMP 与 Editor 程序集通过离线编译检查；尚未完成 Unity 的边缘翻转、Canvas 缩放、目标失效、输入和销毁运行验收。


## TooltipTrigger：延迟悬停与键盘选中

在目标控件上添加 `MUI.UGUI.TooltipTrigger`，配置预先准备好的 `AnchoredOverlayElement`。它是独立 MonoBehaviour，可以与 ButtonElement 等绑定控件共存，不额外占用 Element 名称索引。

- PointerEnter 或 EventSystem Select 建立显示意图，默认经过 0.4 秒非缩放时间后设置 Anchor。启用触发器时会检查目标是否已经被选中。
- PointerExit 只移除对应 pointerId；仍有指针或键盘选中时保留意图。所有意图离开才隐藏。最多同时登记 16 个指针。
- PointerDown 隐藏并抑制重新打开，直到全部悬停/选中意图离开，再进入。返回、外部关闭和共享浮层被其他目标接管也采用相同抑制规则。
- 所属 View 输入门控关闭时立即隐藏自己持有的提示并重置延迟；门控恢复且意图仍存在时重新计时。Selectable 禁用、浮层禁用或失效也会在推进时隐藏并停止计时。
- 多个触发器可引用同一浮层。旧触发器只清除 Anchor 仍等于自身目标的显示，不关闭后来目标的提示；它也不会不断抢回被接管的浮层。
- 禁用触发器解除浮层和 View 事件订阅、清空状态、释放自己的显示；不会销毁借用浮层。浮层组件禁用时会隐藏 Content，重新启用后恢复有效 Anchor 的布局。
- 默认在 Update 推进。手动回放时取消 Inspector 的 Automatic Clock，由唯一驱动调用 `Advance(delta)`；不要同时自动和手动推进。

Tooltip 内容应是被动展示：在 Content 的 CanvasGroup 关闭 Blocks Raycasts，避免提示挡住目标后产生反复 Enter/Exit；不自动抢焦点，也不要把交互菜单放在这个悬停触发器里。目标需具备 EventSystem 的射线/选择条件；组件不会自动创建 RaycastTarget 或 Selectable。

触发器只控制已经准备好的内容何时显示，不为不同目标自动换 VM 或文本。同一浮层共享不同内容时，业务仍须通过现有绑定完成数据更新；异步准备服务、焦点语义关联与动态 Tooltip 模板尚未提供。当前触发/共享/门控代码仅编译检查，未完成 Unity 实际输入验收。


## 编辑器检查

在所属 View Inspector 执行 Validate View Structure（或绑定契约校验）可检查浮层 Content 直接子层级、单位缩放/旋转、与父 LayoutGroup 的位置控制冲突、方向枚举和有限非负间距。TooltipTrigger 作为普通 MonoBehaviour 也会在当前 View 边界内检查：浮层引用、延迟、目标不能处于提示内容内部、已知 Canvas 不一致，以及未禁用射线的提示 Graphic。

射线检查遍历非激活内容以覆盖会被展示的模板分支，识别内容范围内的 CanvasGroup.blocksRaycasts 与 ignoreParentGroups；它检查作者配置，不代替真实射线和 Mask 验收。无 Canvas 的 Prefab 允许等待宿主挂载，运行时仍会验证同一个 root Canvas。嵌套 View 及 Element 边界内部遵循已有校验边界，需要分别校验。

本次检查器已通过 Editor/TMP.Editor 编译；尚未在 Unity Inspector 中运行验收。


## ContextMenuController：已编排菜单的键盘与焦点

在 AnchoredOverlayElement 同一对象添加 ContextMenuController；其 ContentRoot 内用原生 Button / ButtonElement 编排单层菜单，沿用现有 VM 命令绑定。`Show(anchor)` 打开，业务动作确认结束后调用 `Hide()`；返回与外部指针分别转发给控制器的 `HandleBack()` / `HandleOutsidePointer(position, camera)`。

- 首次显示记录打开前的选择，按层级顺序收集最多 128 个 Button，选中第一个可用项。每次打开重新收集；打开期间增删或重排后可调用 RefreshItems，见下文。
- 每帧根据激活、Interactable、所属 View 输入门控刷新上下导航，跳过不可用项；默认循环，可关闭 Wrap Navigation。左右键保留当前项，不自动跳到页面控件。
- 菜单动作仍由 Button 的 Submit/点击及现有绑定执行。控制器不提前关闭、不吞掉业务错误，也不把异步动作当作已经完成；是否执行期间禁用及何时关闭由业务命令决定。
- 会话内约束焦点到可用菜单项；没有可用项时关闭，避免与导航宿主争夺空选择。被移出 ContentRoot 的按钮立即恢复原来的 Navigation。
- 关闭或失去输入许可时恢复所有记录的按钮 Navigation。若当前焦点仍在菜单或为空，且原选择仍可用，则恢复原选择；新页面/对话框已选中其他对象时不抢回焦点。
- 禁用控制器会隐藏菜单并恢复导航配置。正常关闭若遇到 EventSystem.alreadySelecting，会在下一次 LateUpdate 重试焦点恢复；控制器在该回调内直接被禁用/销毁时不再保证下一帧重试，由现有 Navigator 的焦点约束接手。
- 控制器只负责同一个已准备浮层的单层按钮菜单；支持刷新已提交的动态按钮层级，但不自动创建菜单项目、生成数据绑定或拥有异步投影。不支持子菜单与水平展开，菜单层级不要嵌套另一个 ContextMenuController。

ContextMenuController 没有独立全局菜单栈。它应随所属 View 创建和销毁；当前打开期间不支持搬迁到另一个 View。原生输入模块必须正确配置 Move/Submit；原生 Cancel 可通过下述 ContextMenuItemInput 接入，平台返回仍使用已有路由；组件没有全局键盘轮询。

当前代码通过 UGUI/TMP/Editor 离线编译，真实键盘/手柄、按钮状态变化、焦点重入和清理尚未在 Unity 验收。


## EventSystem Cancel 与外部按下

每个菜单 Button 同节点添加 `ContextMenuItemInput`。Unity InputModule 会将 Cancel 发送到当前选中对象，此组件找到最近的 ContextMenuController，调用 HandleBack；确实处理后标记事件已使用。原生 Submit/Move 仍交给 Button，未新增全局按键轮询或 Input System 依赖。

外部关闭背景使用 `OverlayDismissArea`，添加到 AnchoredOverlayElement 的同一对象，配套同节点 Image；它应位于 Content 的父节点，按默认 UI 层级绘制在菜单内容后面。

```text
OverlayBounds       AnchoredOverlayElement + ContextMenuController
                    + Image + OverlayDismissArea
└── Content
    ├── ActionA     Button + ButtonElement + ContextMenuItemInput
    └── ActionB     Button + ButtonElement + ContextMenuItemInput
```

- Bounds 的矩形决定拦截范围；需要覆盖页面时将其铺满相应区域。背景颜色由作者决定，可透明或半透明。
- 适配器独占背景 Image 的 enabled/raycastTarget，浮层关闭、输入门控关闭或适配器禁用后均设为 false，避免留下无形阻挡或背景颜色。不要给这两个属性再加业务绑定。
- 实时 ICanvasRaycastFilter 检查当前浮层和 View 状态，避免等待 LateUpdate 才停止旧射线拦截；子内容同样受这一门控约束。
- 背景处理 PointerDown，先消费事件，再调用锚点浮层的外部判断。原生 EventSystem 会记录按下目标，后续释放不主动转发到底层控件。点在 Anchor 内部不关闭，但若已命中背景也不会穿透给 Anchor。
- Content 的交互图形必须能命中射线；空白区域如需视为菜单内部，应有相应背景 Graphic。不要将此背景 Surface 放在所有内容之后的兄弟节点，也不要用覆盖 Canvas sorting 的子画布绕过预期排序。
- 不把 Selectable 放在背景同节点；关闭 Image 的 alpha hit test，避免透明像素造成孔洞。CanvasGroup、Mask、自定义 Graphic RaycastFilter 和多 Canvas 排序仍需按实际项目验证。

View 结构检查会检查菜单容量、嵌套菜单、Button 的 Cancel 转发器，以及背景 Image/Selectable/alpha hit test 的明显错误。运行时编译和编辑器编译已通过；实际输入模块的按下/释放、Cancel、Canvas 排序和焦点顺序尚未在 Unity 验收，不能据此宣称完整跨手势输入协议已验证。


## Prefab 创建入口

- `Tools → MUI → Create Context Menu Prefab`：创建 ContextMenuView，完整铺开的 OverlayBounds、AnchoredOverlayElement、ContextMenuController、透明 Image/OverlayDismissArea、Content 背景和两个示例按钮。按钮附带 ButtonElement、TextElement、ContextMenuItemInput 和静态无障碍名称。背景射线在资产中默认关闭，运行时只在菜单打开且可交互时启用。
- `Tools → MUI → Create Tooltip Prefab`：创建 TooltipView、锚点布局、Message TextElement 和被动内容 CanvasGroup；提示不拦截射线，不取得交互焦点。TooltipTrigger 应添加到项目的实际目标控件上，模板中不创建虚假的目标。

向导使用临时 Preview Scene 构造，保存前调用结构和基本语义检查，退出时清理临时对象；只允许新路径，不修改已有资产。生成的 Prefab 不带独立 Canvas，依赖项目现有 UIHost/投影挂载，运行时 Anchor 仍要求同一个 root Canvas。

这两个入口生成的是可编辑表现模板，不生成 VM、业务命令或 Route。项目以 FUI 风格通过 Message、Action1、Action1Label、Action2、Action2Label 等明确名称建立已有 Bind/Command 声明，并将 Prefab 接入既有投影生命周期。菜单操作为占位显示，不自动执行项目业务。菜单和 Tooltip 都使用固定内容尺寸；长文本需按项目需求配置换行、滚动或最大尺寸。

已完成向导源代码及相关 Editor 编译；尚未在 Unity 中点击菜单生成资产、检查实际布局或验证 Prefab 导入结果。


## 打开期间的项目更新

动态列表或投影提交菜单按钮层级后，调用 `ContextMenuController.RefreshItems()`。通常在 UI 线程、子视图准备和绑定提交成功后调用，不要在只有数据变化但按钮尚未物化时把它视为刷新完成。

- 重新按当前层级顺序收集 Button，并在写导航前检查容量。保留仍存在的按钮对象及其最初 Navigation 快照，新增按钮单独记录快照。
- 已移出或销毁的项目移出记录；存活但移出的按钮恢复原 Navigation。交换 sibling 顺序后上下导航随之更新。
- 仍有效的当前选中 Button 保持选中；选中项被移除或禁用时选中首个可用项。全部不可用时关闭，恢复原焦点规则与普通关闭一致。
- 焦点回调中调用刷新只标记待处理，下一次 LateUpdate 合并执行，避免递归改写集合。普通调用同步执行；关闭时无需刷新，下次打开自动读取完整层级。
- 超出容量或刷新异常时关闭菜单，恢复已经接管的导航，并向直接调用方抛出错误；延迟合并刷新发生的错误交给 UIErrors。失败不会保留已更新但尚未纳入导航管理的可交互菜单。

刷新不自动给新按钮添加 ContextMenuItemInput，也不生成业务命令或布局。项目的动态模板仍应包含 ButtonElement、Cancel 转发器和语义名称。保留选择基于同一个 Button 对象；把旧按钮销毁再创建，即使业务 key 相同也不视为同一个选择。数据 key 到按钮的复用仍由现有列表/投影负责。

这项 API 已编译检查；动态层级提交、选择变化和故障清理尚未在 Unity 运行验收。
