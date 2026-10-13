# 页面导航与标准对话框

[返回总览](../../README.md)

查看 [NavigationViewModel.cs](NavigationViewModel.cs) 及 [ExampleWorkbench.cs](../ExampleWorkbench.cs) 的 OpenDetailsAsync / ShowDialogAsync。

- `Navigation.prefab`：OpenDetails 的 ButtonElement 对应生成命令，Result 的 TextElement 显示返回结果。
- `Detail.prefab`：Complete 调用 `context.RequestClose(42)`，Back 调用无结果的 `RequestClose()`。ResourceKey 与 Resources 相对路径对应。
- `Dialogs.prefab`：Confirm / Alert 打开标准对话框；模型通过注入的异步操作调用宿主，避免在模型里查找场景对象。
- `Confirmation.prefab`：Title、Message、ConfirmLabel、CancelLabel 挂 TextElement；Confirm、Cancel 挂 ButtonElement，名称遵循标准契约。
- `Alert.prefab`：Title、Message、AcknowledgeLabel 挂 TextElement；Acknowledge 挂 ButtonElement。

路由在 ConfigureRoutes 中创建一次，并在同一 Host 中反复复用；每次打开通过模型工厂创建新的页面状态。不能每次点击都创建相同 Key 的新 PageDefinition。

调用者检查 OpenAsync 成功后等待 WaitForResultAsync，再等待 WaitForCleanupAsync。结果已经提交不等于退出动画/资源归还完成。确认返回 true、取消返回 false；普通关闭是 Dismissed，不能伪装成 false 或已确认。

工作台在模态对话期间禁用侧栏，避免跳课打断当前决策。Popup 页面仍由 MUI 负责页面间覆盖、输入门控与焦点。自定义规则在项目唯一 MUISettings 中配置。
