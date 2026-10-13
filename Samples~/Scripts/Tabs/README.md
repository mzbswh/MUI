# 异步页签

[返回总览](../../README.md)

`Tabs.prefab` 的 ContentRegion 挂 AsyncContentElement，并序列化 ContentHost、LoadingOverlay、ErrorOverlay 和 Retry，Failure Message 设置中文错误提示。TabBar 是其兄弟节点，在 Canvas 层级中位于 ContentRegion **之后**，加载遮挡不会盖住页签按钮。

TabBarElement 的子按钮名称为 inventory / quests / locked，对应 TabContentDefinition 的 ID。选择指示节点为 Selected。`TabContent.prefab` 独立定义内容契约，Title/Detail 绑定 TabContentViewModel。

[TabsViewModel.cs](TabsViewModel.cs) 的 CreateBinding 是此课的渲染接入层：用父 View.Widgets 构造 TabContentController，绑定内容区与 TabBar。控制器由父 WidgetScope 托管；模型拥有加载提供方，退出后归还。TabsPresenter 在父页面打开后选中 inventory。

TabContentPresenter 用可取消的短延迟模拟业务准备。任务（quests）第一次故意失败，点击“重试”后成功；未解锁（locked）不可选。缓存容量 2，最近内容可复用。真实项目替换准备逻辑和内容模型，不改动展示层结构。
