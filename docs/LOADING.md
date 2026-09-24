# Loading：操作归属与显示

`MUI.Loading` 管理一组在途操作，`LoadingElement` 负责 uGUI 呈现。Scope 不拥有实际异步任务，也不代替资源 Lease；资源仍交给 ResourceSlot 或 Lifetime 管理。

```csharp
// 由页面或宿主拥有。页面可以把它暴露为 VM 属性并绑定 LoadingElement.Source。
var loading = new LoadingScope(displayDelay: 0.2, capacity: 64);
activationLifetime.OwnDisposable(loading);

// 放在已有异步业务方法中；await 必须回到创建 Scope 的 UI 线程。
using (var operation = loading.Begin(activationLifetime, "正在加载道具…"))
{
    await LoadItemsAsync(activationLifetime.Token);
    operation.Report(1);
}
```

`LoadItemsAsync` 为项目方法。需要禁用操作区域时，将对应 `InputGate` 传给 `Begin` 的 `blockInput` 参数；省略时仅展示状态。阻塞从 Begin 开始，提示经过延迟才显示。普通提示容器应关闭 Graphic 的 Raycast Target，避免把显示本身当作输入策略。

由唯一的外部帧驱动调用 `loading.Advance(unscaledDeltaTime)`，例如页面 Tick 或宿主循环。组件不自行 Update，避免多个组件借用同一个 Scope 时重复计时。隐藏页面可能暂停页面 Tick；需要跨页面持续计时的全局 Loading 应由宿主驱动。

## 约定

- `Begin` 返回独立 `LoadingOperation`；Dispose 幂等，仅释放自己的输入阻塞。Scope Dispose 会释放所有剩余操作并发布隐藏状态。
- `Lifetime.Cancel` 仅请求取消，不立即释放已有操作；操作的 `using/finally` 或 Lifetime 最终清理负责释放。不要用取消请求推断实际任务已结束。
- Lifetime 为最终清理兜底；长期宿主应使用按任务/激活创建的 Lifetime，避免把每个短任务永久登记在宿主 Lifetime 中。
- 进度范围为 0～1；`Report(null)` 表示不定进度，默认也是不定进度。任一活动操作不确定时，整个组显示不定进度；否则按正数权重汇总。已释放令牌的 Report 返回 false。
- 汇总只包含当前活动操作，因此新操作加入或旧操作退出时，进度允许回退。需要单调的批次进度时，使用一个操作报告整个批次的进度。
- 最新活动操作提供 Message。最后一个操作释放后隐藏并重置延迟；活动组中加入新操作不会重新计时。
- Scope、显示订阅、报告与释放均要求所属 UI 线程。Scope 不自动调度线程，也不执行超时取消。
- 监听异常交给 UIErrors，重入最多排空 32 次；监听内销毁 Scope 仍发布最终空状态后再清除订阅。

## Prefab 结构

```text
LoadingAdapter                 LoadingElement；保持激活
└── Indicator                  指定为 indicator
    ├── Message                Text，可选
    ├── Progress               Filled Image，可选
    └── Indeterminate          不定进度节点，可选
```

三个显示分支必须位于 Indicator 内。Progress 和 Indeterminate 不能互相包含，Message 不能处于这两个会被隐藏的分支中。Source 更换会解除旧订阅；Element 销毁只解除订阅并隐藏提示，不销毁借用的 Scope，也不结束业务操作。

底部 Tab 场景可将 LoadingAdapter 放到内容区域、TabBar 保持同级独立。已有异步 Tab 的版本化加载状态仍由 TabController 管理；通用 Loading 不替代它的最新请求、失败与内容提交协议。

当前仅完成编译和静态检查，尚未完成 Unity 实际显示、门控、异步取消与重入的运行验收。


View 的结构/契约校验已覆盖 indicator 严格子节点、可选分支范围、Filled Image、互相包含与消息被进度分支隐藏的错误配置。校验只读取序列化字段，不调用 Element.Initialize；编辑器执行结果尚待 Unity 验收。


## 项目自定义加载策略

`LoadingElement.Source` 接收 `ILoadingSource`，只读取 Snapshot 和 Changed。默认 LoadingScope 实现该接口；项目可替换为分阶段进度、服务端进度或自己的延迟/最短展示策略，而不修改 UGUI 控件。Begin、Advance、Dispose 及输入阻挡归加载服务所有者，不进入显示接口。

LoadingSnapshot 已独立为同名源码文件并公开构造器：任务数非负，阻挡数在 0 到任务数之间，非空进度为有限的 [0,1] 数值。null 进度表示不确定；可见性独立配置，允许任务完成后短暂保留最终画面。Message 接受 null 并归一为空字符串，default 快照也可以安全交给控件。

自定义来源必须在 UI 线程发布状态，Snapshot 无副作用，事件访问器只增删订阅并在发布时隔离观察者异常。BlockingCount 只描述状态，不会让显示控件创建 InputGate 令牌；真实阻挡由任务服务创建和释放，避免“显示中”和“阻挡中”被强制绑定。

兼容性：LoadingScope 仍可直接赋给 Source。业务若曾通过 element.Source.Begin/Advance 调用加载操作，应持有具体服务引用；Source 的双向生成绑定若写回 LoadingScope 类型，应改为单向或使用 ILoadingSource。此调整不改变默认加权计算/计时/释放规则，也没有完成 UGUI 程序集按需拆分。

已通过离线编译；自定义来源与 Unity 显示的运行联动尚未验收。


## 程序集接入更新

控件已移动到 `Runtime/Rendering/UGUI.Loading/`，位于独立 `MUI.UGUI.Loading` 程序集，命名空间保持 `MUI.UGUI`。使用控件的项目 asmdef 请引用此程序集；直接使用业务服务的代码另引用 `MUI.Loading`。基础 UGUI 不再依赖本模块。原脚本 GUID 已随 .meta 保留，Unity Prefab 重新导入仍待验证。

校验位于 `Editor/Loading/MUI.UGUI.Loading.Editor`，通过基础 Editor 的扩展接口注册，不由基础 Editor 反向引用。离线构建校验请使用 `Tools~/Build/MUI.UGUI.Loading.Editor.csproj`，仍传入既有 UnityManagedPath/UnityUIAssemblyPath。前文“尚未拆分”描述由本节替代；同一 UPM 包中源码仍随包提供，并非已经实现独立包安装。
