# 子 View 与模型替换

[返回总览](../../README.md)

打开 `Widgets.prefab`：根 View 的 Child Widgets 中登记 ItemCard，Target 指向内部的子 View，关闭 CreateVM，由父模型提供实例。

[WidgetsViewModel.cs](WidgetsViewModel.cs) 的 Item 绑定 `WidgetReference.VM`。ItemCard 内的 Title/Detail 只属于子 View 的 ItemViewModel；父绑定不跨越它的查找边界。

Replace 创建新的表现模型；Clear 赋 null；再次 Replace 复用原有子节点。父页面退出时框架结束子激活，不需要手动遍历子控件解绑，也不在运行时搭建子层级。
