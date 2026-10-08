# Tabs 示例

导入 **Tabs**，选择 **Tools → MUI → Samples → Open Tabs Scene**，进入 Play 模式。点击 Inventory 或 Quests；Quests 首次准备故意失败，点击 Retry 后成功。Locked 不可用。加载提示只覆盖内容区域，底部 TabBar 保持可用。

示例通过代码创建 UI 和预制体模板，使用旧版 EventSystem 输入模块，项目需启用旧输入后端或 Both。批处理入口为 `MUI.Samples.Tabs.Editor.TabsSampleMenu.RunPreviewBatch`。以下为手动验收步骤；当前新增配置只经过离线编译，尚未在 Unity 中执行验收。

## 默认流程与可选表现

请在进入 Play 前配置组件。启用 Automatic Walkthrough 可演示重复等待取消、不可用项拒绝、延迟加载提示、失败重试、快速切换、离开守卫及父级清理。验证下列可选策略时关闭 Automatic Walkthrough，避免自动流程干扰操作。

- Blank While Loading：加载时隐藏内容和加载字样。
- Keep Previous While Loading：加载时保留旧画面，冻结旧内容业务与输入；优先于 Blank While Loading。
- Failure Display = Restore Previous：目标准备失败后尝试恢复上一项。可与上述加载表现组合。
- Cache Capacity：0 为离开即释放；正数启用 CacheRecent，限定停用缓存数量。
- Cache Time To Live Seconds：0 不按时间过期；正值需先启用缓存，从每次实际入缓存起计时，过期命中会释放并重新创建。

手动缓存检查：设置 Cache Capacity = 1；打开 Inventory，再打开 Quests（首次失败后重试），然后返回 Inventory。使用组件上下文菜单 **Log Cache And Preparation State** 查看缓存与隔离数量；使用 **Clear Cached Tabs** 等待停用缓存释放。清缓存不关闭当前内容，后续切换仍可再次缓存。

缓存过期检查：设置 Cache Capacity = 1、Cache Time To Live Seconds = 2。成功切到 Quests 后等待超过 2 秒，再返回 Inventory，应重新经历准备延迟。Unity 同步上下文下每秒自动检查过期，可用数量菜单观察未再次访问的缓存也被回收。扫描或清理可能延后，命中仍独立检查期限；父级关闭取消扫描并等待已启动的清理。

## 超时与迟到清理

以下配置用于观察不即时响应取消的准备任务：

| Inspector 配置 | 示例值 |
|---|---|
| Preparation Delay Milliseconds | 3000 |
| Preparation Timeout Seconds | 0.2 |
| Ignore Preparation Cancellation | 开启 |
| Max Quarantined Preparations | 2 |

初次打开会在准备期限后进入错误状态。立即 Retry 可启动下一次准备；隔离数量达到 2 时，后续新准备被拒绝，直到旧任务及其清理结束。使用 **Log Cache And Preparation State** 观察数量；等待各次准备原定的 3 秒延迟结束后，数量应下降。迟到候选不会显示，也不会进入缓存；如果其关闭已启动，仍等待实际清理结束才归还隔离名额。

隔离容量恢复后可以再次 Retry，但这组配置每次都会再次超时；要验证成功加载，请退出 Play，把 Preparation Timeout Seconds 设为 0（不启用超时）或设为大于准备延迟，再重新运行。准备配置在启动时固定，运行中修改 Inspector 不改变既有 Tab 定义。

超时只限制准备等待，不强制终止外部任务；父级最终销毁仍等待隔离任务及资源清理。此示例没有实际模拟永不完成的任务，也不代表已经验证所有取消与销毁竞态。

## 动态目录与导航

自动流程还会更新目录：相同定义保留内容；重复键失败且不替换目录；移除选中项时回退；空目录清空内容；恢复目录重新显示预配置按钮。显式的未激活按钮模板支持新键，示例生成 Mail 按钮，移除后恢复焦点至 Inventory。生成多个按钮时应配置布局组。

Inventory 的异步离开守卫演示拒绝、重复等待取消、新请求淘汰旧请求及父级取消。键盘导航只经过可用 Tab 按钮，子内容加载完成不会主动抢焦点。


## 资源代际失效演示

Play 前将 Cache Capacity 设为 2，Cache Time To Live Seconds 保持 0。切换两个有效 Tab 建立缓存，再从 TabsDemo 组件菜单执行 **Invalidate Tab Resource Version**。菜单模拟提供方整体资源内容换代并请求维护：当前显示页面保留，旧停用缓存回收；再次选择之前的页签时 created 计数应增加，不能复用旧物理实例。

也可把 Preparation Delay Milliseconds 设为 1000，在新页签仍加载时执行同一菜单：旧候选应取消/失败并回收，不得提交；重试后使用新代际。启用缓存但不配置 TTL 时仍每秒扫描，菜单的 RefreshCache 只是提前请求维护。版本包装器使用固定同步 Prefab，仅模拟目录代际，不模拟远程热更新。以上是人工验收步骤，尚未在 Unity 运行。


## 统一回收登记演示




## 本地 Prefab Tab 示例

LocalTabsDemo 使用统一异步协议接入常驻 Prefab，不额外延迟立即完成的准备。配置场景父 View、兄弟区域的 TabBarElement 与 AsyncContentElement，以及 TabPage Prefab；按钮键为 inventory/quests，页面绑定使用 TabPageViewModel。组件由原 SynchronousTabsDemo 改名并保留脚本 GUID。

启动默认选择背包。Allow Leave 关闭时拒绝离开背包；Fail Quests 开启时任务页准备失败并保留原内容。选择、定义替换和缓存清理均使用异步入口，守卫通过立即完成的 ValueTask 返回结果。缓存容量为 2、TTL 为 5 秒，由控制器维护；组件不再额外逐帧扫描。销毁等待父 LifetimeScope 排空并释放子视图、控制器、缓存和提供方。菜单入口观察异常并忽略父生命周期结束引起的取消。上述行为仍需 Unity 运行验收。

闲置 UI 内容由项目显式清理：导航调用 ClearInactiveContentAsync，Tab 调用 ClearCacheAsync。框架不订阅平台低内存事件或协调项目对象池。Navigation 示例需先导入 Resource Integration 接入示例。
