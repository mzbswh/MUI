# 完全同步的标准对话框

`SynchronousDialogsDemo` 使用已有的 `ConfirmationDialog`、`AlertDialog` 和导航器实现完整流程。组件及其业务回调不编写 `async/await`，不读取任务属性，不安装 `DialogService`。等待用户点击只保留一次性结果订阅。

## 场景配置

1. 在 Package Manager 导入 **Dialogs** 示例。此示例的程序集单独引用标准 Dialogs 模块，不给基础 Navigation 示例增加模块依赖。
2. 使用菜单 `Tools/MUI/Create Confirmation Dialog Prefab` 和 `Tools/MUI/Create Alert Dialog Prefab` 创建两个 Prefab。文本使用 uGUI；项目应给文本配置支持所用中文的字体。
3. 创建 Canvas、GraphicRaycaster 和 EventSystem，配置项目使用的输入模块。Canvas 下创建带 RectTransform 的界面容器，在其上添加 `UIHost`，将 View Root 指向该容器。
4. 在 UIHost 的 Prefabs 目录登记下面两项，Version 均为 `1`：

   | Key | Prefab |
   |---|---|
   | `demo.confirmation` | 确认框 Prefab |
   | `demo.alert` | 提示框 Prefab |

5. 在另一个对象添加 `SynchronousDialogsDemo`，将 Host 指向此 UIHost。该宿主由示例独占，不再添加其他调用 Initialize 的启动组件。
6. 进入播放后自动显示确认框。也可通过组件上下文菜单同步显示确认框、提示框、关闭当前对话框或执行返回。

## 调用与所有权

核心调用方式如下，完整错误处理见示例源码：

```csharp
var observations = new Lifetime(LifetimeMode.Synchronous);
host.InitializeSynchronous();
var route = ConfirmationDialog.CreateRoute(new ViewResource("demo.confirmation"));
var opened = host.Navigator.Open(route,
    new CloseConfirmation("提交", "是否提交？", "提交", "取消"));

if (opened.IsSuccess)
{
    opened.Handle.ObserveResult(observations, result =>
    {
        if (result.Error == null && result.Cleanup == CleanupStatus.Complete &&
            result.IsCompleted && result.Value)
        {
            // 执行项目的同步业务操作。
        }
    });
}

// 组件销毁时先撤销业务结果订阅，再关闭由本组件拥有的宿主。
// observations.Dispose();
// host.Shutdown();
```

`Open` 直接完成创建和显示，然后返回；用户稍后点击时，`SynchronousCommand` 请求完成结果，UIHost 的同步帧泵在命令栈退出后关闭界面。清理后的结果通过 `ObserveResult` 发布。示例确认成功后仅记录后续意图，再由 `Update` 同步打开提示框，避免在导航通知栈内嵌套打开。

取消按钮产生 `Completed(false)`；返回、普通关闭或宿主退出产生 `Dismissed`，都不会执行确认业务。清理失败也不会执行后续业务。同一示例只保留一个活动对话框；重复请求不覆盖活动句柄。这里没有排队服务，也不把需要用户回答的操作伪装成立即返回 `bool` 的同步确认函数。

订阅 Lifetime 只拥有订阅，不拥有对话框。停止订阅不等于关闭界面，主动关闭应调用导航器；示例销毁会先停止通知，再同步关闭独占宿主。若集成到项目共享宿主中，应由项目启动层管理宿主，业务组件只关闭自己打开的界面，不能照搬本示例的 Shutdown 所有权。

## 输入及验证范围

向导生成的确认框默认选中取消按钮，方向键可在两个按钮间移动，原生 Submit 执行选中按钮；两个按钮的 Cancel 事件都转到取消操作。提示框默认选中唯一确认按钮；返回应接到 `UIHost.RequestBack`，不将返回伪装成“已阅读”。如果同一取消键同时绑定局部 Cancel 与全局返回，应沿用宿主已有的输入消费机制。

离线编译仅验证源码及程序集引用。实际 Prefab 创建、中文字体、鼠标/键盘/手柄输入、模态阻挡、焦点恢复、反复打开后的资源释放与 Task 分配仍需在 Unity 中验收；本示例不作为这些行为已经运行通过的证据。
