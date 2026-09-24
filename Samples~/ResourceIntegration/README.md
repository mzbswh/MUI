# 项目资源接入示例

这里的 Unity Resources 加载器与 JSON 文本目录读取器属于项目侧参考代码，不随 MUI Runtime 导入。运行 Navigation 示例前先导入本示例。实际项目使用已有资源系统实现 MUI 的加载/持有权契约，无需采用这里的具体后端。框架不负责资源下载、共享缓存、预算、卸载或平台内存事件。

`LifetimeResourceExtensions` 也是项目示例：将项目加载结果的凭证交给 UI 生命周期，并归还取消后的迟到结果。框架的 `Lifetime` 只提供 Own/OwnDisposable 和取消、清理能力，不直接发起任意资源加载。同步 Load 与异步 LoadAsync 分开实现。

JSON 语言目录示例使用 Unity 的 `com.unity.modules.jsonserialize`（1.0.0）模块。MUI 的页面向导同样使用该模块，因此已由主包声明依赖，无需为示例额外启用。已有项目资源/本地化后端不需要采用此 JSON 实现。
