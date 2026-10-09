# 拖放示例

导入 **DragDrop**，在带有活动 Camera 的场景中添加 `DragDropDemo` 并进入播放；相机避免 Unity 的“No cameras rendering”提示覆盖 Overlay UI。脚本创建 Canvas；没有 EventSystem 时创建一个使用 StandaloneInputModule 的 EventSystem。项目需启用旧输入后端或 Both，已有 EventSystem 须提供兼容指针输入。

组件的 **Delay Commit** 默认关闭，目标立即完成提交；开启后等待 600 ms，用于演示提交期间取消。两种情况使用同一协议。

将金色道具拖到 Accept、Reject 或 Fail。Accept 增加成功计数；Reject 在可用性查询中拒绝；Fail 报告演示异常且不增加计数。拖到外部后松开会取消。被动拖拽影子跟随指针，开始提交时归还，原始道具保持原位。

禁用示例会结束 LifetimeScope 并隐藏 View；重新启用不重建，请重新加载场景。场景切换前调用并等待 `ShutdownAsync()`：先取消并等待会话与激活收尾，再归还 View 和示例创建的场景对象，已有 EventSystem 仅借用。停止播放和销毁回调共享同一次清理；Unity 不等待异步原生回调，不能用销毁通知代替显式等待。清理失败时保留尚未确认的拥有关系和原生对象。

## 项目接入

```csharp
var sourceLifetime = new LifetimeScope();
var targetLifetime = new LifetimeScope();
sourceElement.Source = new DragBinding<ItemData>(sourceLifetime, () => selectedItem);
targetElement.Target = new DropBinding<ItemData>(
    new DropTarget<ItemData>(targetLifetime,
        (item, token) => new ValueTask<bool>(inventory.TryMove(item)),
        item => inventory.CanMove(item)));
```

项目提供 inventory 和载荷类型；示例需引入 System.Threading.Tasks。立即完成的提交直接返回 ValueTask，不需要线程调度。返回 false 必须表示未修改业务数据，成功提交应返回 true，不能用迟到取消覆盖成功。

直接使用 DragSession<T> 时调用 DropAsync(target)，使用 TryGetResult / IsCompleted 查询最终状态，DisposeAsync 取消并等待收尾。源生命周期自动托管会话，无需重复 Own；每个源及载荷类型最多保留 256 个未结束会话。视觉回调不得等待自身提交或销毁。

提交登记到源和目标两端，销毁时先等待提交退出再释放各自资源。业务操作不能等待跟踪自身的 LifetimeScope.DisposeAsync；框架在改变取消/清理状态前返回失败，防止等待自身完成。可以发出 Cancel，待操作返回后由外部生命周期拥有者执行销毁。

DragSession 创建时需要所属 UI 线程的 SynchronizationContext，可显式传入，默认使用当前上下文。UI 线程取消立即收尾；后台取消或提交结果通过该上下文派发回 UI 线程。上下文派发失败时报告错误并保留待处理状态，由所属 UI 线程调用 `Pump()` 收敛。uGUI 的 DragSourceElement 自动驱动 Pump；直接使用核心会话的项目可每帧调用 Pump，并通过 `DisposeAsync()` 等待提交与最终收尾。

## 验证范围

此示例演示原生 uGUI 事件适配，不是生成的业务页面绑定。Unity 2022.3.62f3 新工程的 Editor Play 已验证画面、停止播放，以及经原生适配入口执行的立即提交、延迟提交取消、重复退出和借用 EventSystem 保留；观察脚本没有模拟实体鼠标。真实指针、多指输入和任务分配仍需对应的运行证据。指针关联凭证不是操作系统级 Pointer capture。
