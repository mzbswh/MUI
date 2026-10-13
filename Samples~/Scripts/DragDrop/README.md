# 类型化拖放

[返回总览](../../README.md)

`DragDrop.prefab` 中 Crate 挂 DragSourceElement；Accept/Reject/Fail 挂 DropTargetElement。Image 保持射线开启，内部文字关闭射线。Drag Icon 指向道具 Image，Ghost Root 指向同一 Canvas 下的独立容器，不能是源的子节点。

[DragDropViewModel.cs](DragDropViewModel.cs) 创建 `DragBinding<Crate>` 和同类型的 `DropBinding<Crate>`，通过生成属性绑定传给控件。LifetimeScope 管理源、目标和异步提交。原生指针事件由框架组件处理，业务模型不计算鼠标位置、不创建跟随影子。

拖到 Accept 成功计数 +1；Reject 在可用性查询时拒绝；Fail 模拟提交返回 false，业务数据不改变；区域外松开取消。Delay 使用 ToggleElement 的双向绑定，开启后提交延迟 600 ms。Finished 结果显示在状态栏。

可用性查询不修改数据；真正提交发生在异步回调里。返回 false 必须表示没有完成业务修改。页面退出时取消并等待源/目标作用域收尾，不能只销毁显示节点而遗留后台提交。
