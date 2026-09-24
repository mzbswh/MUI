# 通知队列与显示

`MUI.Notifications` 提供不依赖 Unity 的 `NotificationQueue`。它维护当前通知和有界待显示队列，不拥有页面、Prefab 或独立全局窗口栈；uGUI 通过 `NotificationElement` 借用队列进行显示。

```csharp
var notifications = new NotificationQueue(capacity: 32);
hostLifetime.OwnDisposable(notifications);

var result = notifications.Post(new Notification(
    message: "道具已领取",
    key: "inventory.reward.claimed",
    priority: 10,
    displayDuration: 3,
    expiresAfter: 15));

// 唯一宿主帧循环中推进；不能让多个显示组件重复推进。
notifications.Advance(unscaledDeltaTime);

// 如需主动移除，携带返回的 ID，避免误关后来显示的通知。
notifications.Dismiss(result.Id);
```

示例中的 Lifetime 与帧参数由项目提供。队列适合由宿主持有，页面局部提示也可交给激活 Lifetime；Cancel 不等于 Dispose，资源清理时才清空队列。业务需要取消时立即清空可显式调用 Clear。

## 排队与过期协议

| 行为 | 当前约定 |
|---|---|
| 容量 | 当前项和待显示项共同占用；满时返回 CapacityExceeded，不偷偷丢弃已有通知 |
| 去重 | 非 null key 按 Ordinal 比较，在当前项和队列内查找；返回 Duplicate 与原 ID；不更新消息、优先级或寿命 |
| 无 key | 每次 Post 都是独立通知，即使文本相同 |
| 优先级 | 整数越大越优先；当前项不被抢占；相同优先级按入队顺序显示 |
| 展示时长 | 从提升为当前项开始计算，必须是有限正数 |
| 过期 | 从入队开始计算，覆盖排队和展示时间；待显示项过期后直接移除 |
| 大帧间隔 | 所有项先扣除过期时间，再选择未过期项；新提升项从当前帧开始展示，不扣除它尚未展示的时间 |
| 关闭 | Dismiss(id) 只移除对应项；不存在/过期/已关闭返回 false |
| 清空 | Clear 可重复调用，后续仍可 Post；Dispose 清空并终止队列，之后 Post 返回 Disposed |
| 接受结果 | Accepted 表示入队已提交；监听回调可能立即关闭它，不代表返回时仍在展示 |

`Advance` 由唯一的非缩放帧时钟驱动；它不是系统墙钟，暂停推进也会暂停寿命计算。全局通知应由持续运行的宿主推进，不放在可能因覆盖而暂停的页面 Tick 内。

## 显示层

```text
NotificationAdapter           NotificationElement，持续激活
└── Indicator                 显隐容器
    ├── Message               Text，必需
    └── Dismiss               Button，可选
```

将队列赋给 `Source`，可使用已有 VM 属性绑定。切换 Source 会解除旧订阅；销毁组件会隐藏容器并解除订阅，不销毁借用队列。原生关闭按钮受当前 View 输入门控约束，组件不自动获取焦点。布局、显示位置与安全区由项目 Prefab 决定；装饰 Graphic 应关闭 Raycast Target，交互按钮按需求保留。

不需要为每条通知调用 Navigator.Open，因此不会增加普通页面历史或触发页面焦点迁移。多条同时可见、通知动作按钮、图标模板、TMP 适配和出现/退出动效不是当前组件已经提供的能力。

所有队列操作和订阅呈现要求所属 UI 线程。通知数据不可变；Changed 在状态变化时发布，不逐帧广播倒计时。回调异常上报 UIErrors，重入最多排空 32 次；回调内销毁队列仍发送最终空状态后解除订阅。

当前完成 Notifications、UGUI 及相关 Editor/TMP 程序集的编译检查；尚未完成 Unity 提示展示、计时、优先级、门控、取消和故障路径的运行验收。


View 结构/契约校验已检查 indicator 严格子节点、必需 Message Text，以及消息/关闭按钮是否属于提示容器。它不会启动队列或初始化显示组件；Unity Inspector 执行尚未验收。


## 跨项目替换调度策略

`NotificationElement.Source` 现在接收 `INotificationSource`，而不是具体 NotificationQueue。契约只包含 Snapshot、Changed 和按 ID 的 Dismiss；不要求队列、时钟、Post、容量或 IDisposable。显示控件借用来源，不负责推进时间、清空或销毁服务。NotificationQueue 实现该接口，继续作为默认的不抢占优先级队列。

项目需要“紧急通知抢占”“同类奖励合并”等规则时，可以自行实现 INotificationSource，公开自己的业务入站方法，同时复用现有 NotificationElement。Snapshot 的构造器已公开：显示内容必须使用正 ID，空内容使用 ID=0，PendingCount 不得为负。`default(NotificationSnapshot)` 是合法空状态。ID 在可能存在迟到点击期间不能复用，Dismiss 不得把旧 ID 对应到新通知。

例如项目服务在完成自己的调度后，发布 `new NotificationSnapshot(currentId, new Notification(message), pendingCount)`。服务在 UI 线程更新，Snapshot 读取无副作用；事件访问器只增删订阅，Changed 逐订阅者隔离异常。实际计时、入队与资源生命周期由项目或默认实现管理。

本契约针对单个显示位置，不能直接表示同时显示多条通知；多条视图需要集合协议/独立适配，不应向这个接口不断塞入布局方法。默认队列的容量、排序和去重规则没有因此变成所有项目必须采用的规则。

兼容性：现有 NotificationQueue 可继续赋给 Source；从 Source 读取后直接调用 Post/Advance 的代码需要改为持有业务服务本身的引用，因为显示控件不再承诺来源就是队列。生成绑定若把 Source 双向写回 NotificationQueue 类型，应改成单向绑定或使用 INotificationSource 类型。

相关程序集编译通过；第三方来源与 Unity 显示的运行联动尚未验收。UGUI 主程序集仍引用 Notifications，该变化实现策略替换，不代表已经完成程序集按需拆分。


## 程序集接入更新

控件已移动到 `Runtime/Rendering/UGUI.Notifications/`，位于独立 `MUI.UGUI.Notifications` 程序集，命名空间保持 `MUI.UGUI`。使用控件的项目 asmdef 请引用此程序集；直接使用业务服务的代码另引用 `MUI.Notifications`。基础 UGUI 不再依赖本模块。原脚本 GUID 已随 .meta 保留，Unity Prefab 重新导入仍待验证。

校验位于 `Editor/Notifications/MUI.UGUI.Notifications.Editor`，通过基础 Editor 的扩展接口注册，不由基础 Editor 反向引用。离线构建校验请使用 `Tools~/Build/MUI.UGUI.Notifications.Editor.csproj`，仍传入既有 UnityManagedPath/UnityUIAssemblyPath。前文“尚未拆分”描述由本节替代；同一 UPM 包中源码仍随包提供，并非已经实现独立包安装。
