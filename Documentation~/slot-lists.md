# 固定槽位列表

`SlotListElement` 用于数量固定的奖励栏、角色位或装备位。`Items` 接受 `IReadOnlyObservableList<ViewModel>`，监听来源变更，按位置使用子视图。容量由槽位数量确定；超量或 null 条目会拒绝本次赋值，原来源仍有效。长列表使用 `VirtualListElement`。

已有节点采用 `NestedViewElement` 包装子 View，所有包装节点都在 SlotListElement 的独占容器边界内。初始化前配置：

```csharp
slots.ConfigureSlots(new[] { firstSlot, secondSlot, thirdSlot });
```

也可提供明确的空挂点和同一边界内的非激活 NestedViewElement 模板：

```csharp
slots.ConfigureMounts(new[] { leftMount, rightMount }, itemTemplate);
```

每个挂点只创建一次模板实例。容器持有这些实例，已有预制节点则只借用；释放容器不会销毁借用节点。挂点和槽位不能重复或互相包含，不能穿过其他 View 或 Element 容器边界。布局由 Prefab 或原生布局组件决定，框架不移动槽位或增加容量。

通过生成绑定将模型集合写入 `Items`。空槽隐藏并解除子绑定；重新填入数据使用新的绑定代际。父页面换绑时提前准备所有槽位的候选，准备失败保留旧来源和旧画面，提交失败沿页面故障清理。`PendingChange` 包含本轮绑定和旧子项清理；条目命令修改来源后应直接返回，不能等待包含自身清理的列表操作。

Editor 的结构校验、Prefab 增量检查和构建前校验包含固定槽位归属规则。当前源码及离线编译已接入，固定槽位的完整 Unity 运行验收仍见实现记录。

导入 Navigation Sample 后，菜单 `Tools/MUI/示例 (Samples)/打开固定槽位列表场景 (Open Fixed Slot List Scene)` 创建五个挂点的奖励栏示例。初始填入三项，Remove 解除该项绑定，Add reward 使用剩余槽位；满额时显示容量提示。普通回收列表菜单复用同一示例组件和生成绑定。
