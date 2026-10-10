# 标准对话框

`LocalDialogsDemo` 使用 `ConfirmationDialog`、`AlertDialog` 和统一导航接口，演示本地 Prefab 的确认及提示流程。

## 场景配置

1. 在 Package Manager 导入 **Dialogs** 示例。此示例的程序集单独引用标准 Dialogs 模块，不给基础 Navigation 示例增加模块依赖。
2. 使用菜单 `Tools/MUI/创建确认弹窗 (Create Confirmation Dialog Prefab)` 和 `Tools/MUI/创建提示弹窗 (Create Alert Dialog Prefab)` 创建两个 Prefab。文本使用 uGUI；项目应给文本配置支持所用中文的字体。
3. 创建 Canvas、GraphicRaycaster 和 EventSystem，配置项目使用的输入模块。Canvas 下创建带 RectTransform 的界面容器，在其上添加 `UIHost`，将 View Root 指向该容器。
4. 通过 `Assets/Create/MUI/配置 (Settings)` 创建配置并绑定 UIHost.Settings。在 UIHost 的“本地 Prefab 目录”折叠区登记下面两项，Version 均为 `1`：

   | Key | Prefab |
   |---|---|
   | `demo.confirmation` | 确认框 Prefab |
   | `demo.alert` | 提示框 Prefab |

5. 在另一个对象添加 `LocalDialogsDemo`，将 Host 指向此 UIHost。该宿主由示例独占，不再添加其他调用 Initialize 的启动组件。
6. 进入播放后自动显示确认框。也可通过组件上下文菜单显示确认框、提示框、关闭当前对话框或执行返回。

## 调用与所有权

示例使用 `UIHost.Initialize`、`OpenAsync`、`WaitForResultAsync` 和 `WaitForCleanupAsync`。本地资源允许立即完成，用户操作及窗口退出仍可异步完成。

对话框通过 `host.ResolvePolicy("Popup", overrides: ...)` 解析项目预设，并显式设置最多一个实例及拒绝复用；已解析策略传给 `ConfirmationDialog.CreateRoute(..., policy: policy)` 和 `AlertDialog.CreateRoute(..., policy: policy)`。未绑定配置时使用内置 Popup。运行状态与追踪统一在 `Tools/MUI/控制台 (Dashboard)` 查看，UIHost Inspector 提供快捷按钮。

业务结果提交时，清理状态可能为 `Pending`；确认后先等待真实清理，再展示提示框，避免与退出中的窗口争用路由容量。取消按钮产生 `Completed(false)`；返回、普通关闭或宿主退出产生 `Dismissed`，都不执行业务确认操作。

同一示例只允许一条展示流程，重复请求不覆盖活动句柄。组件销毁时取消本地等待，并通过 `ShutdownAsync` 关闭独占宿主。如果接入共享宿主，应由项目启动层管理宿主，业务组件只能关闭自己打开的界面。

需要串行排队的关闭确认可使用 `DialogService`。服务在结果提交后继续等待真实清理，再归还队列许可；清理失败仍报告错误。

## 输入及验证范围

向导生成的确认框默认选中取消按钮，方向键可在两个按钮间移动，原生 Submit 执行选中按钮；两个按钮的 Cancel 事件都转到取消操作。提示框默认选中唯一确认按钮；返回应接到 `UIHost.RequestBack`，不将返回伪装成“已阅读”。如果同一取消键同时绑定局部 Cancel 与全局返回，应沿用宿主已有的输入消费机制。

离线编译仅验证源码及程序集引用。实际 Prefab 创建、中文字体、鼠标/键盘/手柄输入、模态阻挡、焦点恢复、反复打开后的资源释放与 Task 分配仍需在 Unity 中验收；本示例不作为这些行为已经运行通过的证据。
