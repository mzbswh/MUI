# 拖放示例

导入 **DragDrop**，在空场景添加 `DragDropDemo` 并进入播放。脚本创建 Canvas；没有 EventSystem 时创建一个使用 StandaloneInputModule 的 EventSystem。项目需启用旧输入后端或 Both，已有 EventSystem 须提供兼容指针输入。

组件的 **Synchronous** 默认开启：源与目标使用同步 Lifetime，接收目标立即提交，整个拖放与退出流程不创建或等待完成任务。关闭该选项后，接收目标先等待 600 ms，再提交，用于演示异步取消。模式在 Start 时固定，不在播放期间切换。

将金色道具拖到 Accept、Reject 或 Fail。Accept 增加成功计数；Reject 在可用性查询中拒绝；Fail 报告演示异常且不增加计数。拖到外部后松开会取消。被动拖拽影子跟随指针，开始提交时归还，原始道具保持原位。

禁用示例会结束 Lifetime 并隐藏 View；重新启用不重建，请重新加载场景。销毁时取消工作、归还 View 和示例创建的场景对象，已有 EventSystem 仅借用。

## 纯同步项目接入

```csharp
// 两端应使用对应界面的同步激活周期，不需要项目实现任何异步方法。
var sourceLifetime = new Lifetime(LifetimeMode.Synchronous);
var targetLifetime = new Lifetime(LifetimeMode.Synchronous);
sourceElement.Source = new DragBinding<ItemData>(sourceLifetime, () => selectedItem);
targetElement.Target = new DropBinding<ItemData>(
    DropTarget<ItemData>.CreateSynchronous(targetLifetime,
        (item, token) =>
        {
            token.ThrowIfCancellationRequested();
            return inventory.TryMove(item); // false 必须表示没有提交业务修改。
        }, item => inventory.CanMove(item)));
```

上述项目变量由业务提供。来源与目标模式须一致，不匹配时不高亮接收，也不调用提交；异步目标构造器不接受同步 Lifetime。`DragBinding` 自动沿用来源模式，uGUI 仅在确实存在未完成的异步提交时观察任务，同步拖放不读取 Completion。

直接使用核心 `DragSession<T>` 时，调用 `Drop(target)` 取得结果，`TryGetResult`/`IsCompleted` 查询收尾状态，`Dispose` 同步取消并收尾；构造时已由 source 的会话管理器托管，无需再次 Own。每个来源 Lifetime、每种载荷类型只登记一个管理器，最多 256 个未结束会话；结束后从管理器移除，页面不会因历史拖放次数持续持有旧载荷。容量不足在接管指针凭证前拒绝，凭证仍由调用者归还。`DropAsync` 在同步模式下提前拒绝；`Completion` 是显式兼容观察入口，读取它才会创建任务，纯同步业务不要读取。视觉收尾回调在完成状态发布前执行，不得重入自身提交或释放。

同步提交登记到两端 Lifetime，回调退出前不允许同步销毁两端资源。Cancel 是合作式信号；业务已经返回 true 的成功不会被迟到取消改写，也不会由框架撤销业务数据。抛出异常得到 Failed；业务需要自行保证失败时的数据一致性。

异步提交也登记到源和目标两端，销毁时先等待提交退出再释放各自资源。业务操作不能等待跟踪自身的 Lifetime.DisposeAsync；框架在改变取消/清理状态前返回失败，防止等待自身完成。可以发出 Cancel，待操作返回后由外部生命周期拥有者执行销毁。

源在 UI 线程取消时立即收尾；后台取消在纯同步模式只记录意图，须由所属 UI 线程调用 `Pump()`，不使用 SynchronizationContext.Post 或后台任务。uGUI 的 DragSourceElement 自动驱动 Pump；直接使用核心会话的项目自行每帧驱动，或在 UI 线程调用 Cancel/Dispose 完成收尾。纯同步核心不要求存在 SynchronizationContext。

## 验证范围

此示例演示原生 uGUI 事件适配，不是生成的业务页面绑定。离线编译通过；实际指针、多指输入、Canvas、纯同步任务分配及取消/资源释放尚未在 Unity 中运行验收。指针关联凭证不是操作系统级 Pointer capture。
