# 拖放示例

导入 **DragDrop**，在空场景添加 `DragDropDemo` 并进入播放。脚本创建 Canvas；没有 EventSystem 时创建一个使用 StandaloneInputModule 的 EventSystem。项目需启用旧输入后端或 Both，已有 EventSystem 须提供兼容指针输入。

组件的 **Delay Commit** 默认关闭，目标立即完成提交；开启后等待 600 ms，用于演示提交期间取消。两种情况使用同一协议。

将金色道具拖到 Accept、Reject 或 Fail。Accept 增加成功计数；Reject 在可用性查询中拒绝；Fail 报告演示异常且不增加计数。拖到外部后松开会取消。被动拖拽影子跟随指针，开始提交时归还，原始道具保持原位。

禁用示例会结束 LifetimeScope 并隐藏 View；重新启用不重建，请重新加载场景。销毁时取消工作、归还 View 和示例创建的场景对象，已有 EventSystem 仅借用。

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

源在 UI 线程取消时立即收尾；后台取消在纯同步模式只记录意图，须由所属 UI 线程调用 `Pump()`，不使用 SynchronizationContext.Post 或后台任务。uGUI 的 DragSourceElement 自动驱动 Pump；直接使用核心会话的项目自行每帧驱动，或在 UI 线程调用 Cancel/Dispose 完成收尾。纯同步核心不要求存在 SynchronizationContext。

## 验证范围

此示例演示原生 uGUI 事件适配，不是生成的业务页面绑定。离线编译通过；实际指针、多指输入、Canvas、纯同步任务分配及取消/资源释放尚未在 Unity 中运行验收。指针关联凭证不是操作系统级 Pointer capture。
