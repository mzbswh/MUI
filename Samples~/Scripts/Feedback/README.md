# 通知、Loading 和主题

[返回总览](../../README.md)

`Feedback.prefab` 已配置通知文本、进度、四个操作按钮和 PreviewCard ImageElement。[FeedbackViewModel.cs](FeedbackViewModel.cs) 使用 NotificationQueue、LoadingScope、InputGate、ThemeService；生成绑定负责同步文字和命令状态。

显示通知 发布通知；工作台用非缩放时间 Advance 推进队列。同步偏好设置 创建 LoadingOperation，立即阻挡重复业务输入，0.15 秒后显示 Loading。异步操作上报进度，结束时 Dispose 操作，页面退出由模型 LifetimeScope 兜底。

清空通知通过 ClearNotifications 命令清空通知队列。切换主题更换 ThemeCatalog；工作台绑定工厂通过 ThemeBindings 把 surface Token 连接 PreviewCard 的颜色，并登记到模型作用域。

按钮、状态控件和布局都在 Prefab 中。服务不创建界面；模型实现 IAsyncDisposable，实例所有者负责清理其订阅及服务。业务任务的延迟是演示，可替换成真实同步操作。
