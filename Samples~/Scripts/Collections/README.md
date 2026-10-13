# 集合、固定槽位与虚拟列表

[返回总览](../../README.md)

查看 [CollectionsViewModel.cs](CollectionsViewModel.cs)。三个页面都是已制作的 Prefab，模型只更改数据，不创建或排列 GameObject。

## 少量条目：Collections.prefab

Rewards 挂 RecyclingListElement 和 ScrollRect。ScrollRect 的 Viewport/Content 已赋值；Content 使用 VerticalLayoutGroup + ContentSizeFitter。RecyclingListElement 的 Content 指向该节点，Item Template 指向 **Rewards 边界内部、Content 外部的非激活 RewardTemplate**。

模板用 NestedViewElement 引用内部 RewardItem View，条目模型是 RewardViewModel；Title/Detail 绑定文本，Remove 绑定条目命令。集合绑定只提供借用模型；节点池和子激活由框架管理。模板不能放在另一个 View 或列表边界外。

## 固定位置：FixedSlots.prefab

Rewards 改用 SlotListElement，Mounts 中登记五个预先摆放的槽位；Template 同样位于该边界内部且非激活。项目决定容量规则；本课最多五项，满额提示而不推送超量集合。这个模式不让列表布局覆盖美术挂点。

## 大数据：VirtualList.prefab

Inventory 挂 VirtualListElement，Scroll Rect 和 Item Template 已赋值；ItemTemplate 必须非激活并在边界内、Content 外。Content **没有 LayoutGroup/ContentSizeFitter**，尺寸和可见项布局由虚拟列表管理。Estimated Item Extent = 98，Overscan = 2。

VirtualListViewModel 创建带稳定整数键的 2,000 项，ItemViewModel 绑定条目文字。Grid 命令改变 Columns = 1 / 3。真实项目保持键稳定，不以索引漂移替代实体标识。滚动时观察 Hierarchy，可见项复用而不是生成 2,000 个节点。
