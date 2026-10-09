# 项目资源接入示例

这里的 Unity Resources 加载器与 JSON 文本目录读取器属于项目侧参考代码，不随 MUI Runtime 导入。运行 Navigation 或 Common Patterns 示例前先导入本示例。实际项目使用已有资源系统实现 MUI 的加载/持有权契约，无需采用这里的具体后端。框架不负责资源下载、共享缓存、预算、卸载或平台内存事件。

`LifetimeResourceExtensions` 也是项目示例：将项目加载结果的凭证交给 UI 生命周期，并归还取消后的迟到结果。框架的 `LifetimeScope` 只提供 Own/OwnDisposable 和取消、清理能力，不直接发起任意资源加载。加载只使用 `LoadAsync`，本地数据允许立即完成。

候选和迟到结果通过 `await CleanupRegistry.ReleaseAsync(resource, owner, lifetime)` 归还，调用在已登记加载操作退出前完成。该入口保存稳定归还责任，项目明确允许重试的叶责任恢复后，作用域可确认实际清理，首次失败结果保持原值；不要再次以无确认条件的 `RecordCleanupFailure` 登记同一异常。责任属性抛错或返回 null 仍尝试一次真实归还，失败保留未知责任，不重放回调。`ResourceLoadException` 的后端回滚任务也必须等待；失败保存为不可安全重试的独立责任。

`IResourceLoader` 只接受新获取，交付的每个凭证必须独立保证归还所需后端的寿命。借用父加载器的子 View 不能依赖父级仍接纳新工作，迟到结果也不能重新附着已经停止的子激活。凭证无法延长必要依赖时，View 获取不声明 `ICacheableViewAcquisition.SupportsCaching`；关闭实际归还后才可停止依赖。缓存再激活通过提供方配置回调注入当前加载器，不能保留旧激活的资源上下文。

`LoadedPrefabViewProvider` 的正常释放和创建回滚都先等待原生实例及最终控件责任确认，再归还加载引用。失败实例所在的 staging 根不会因提供方退出而销毁。项目显式恢复允许重试的叶责任后，依次确认 View、ViewHierarchy、Prefab 工厂实例或回滚、加载提供方实例或回滚，以及仍保留的 staging 责任；每个后端引用只尝试扣减一次，未知部分归还失败不重新执行。提供方的首次退出结果和历史错误仍保持原值。未确认责任通过 CleanupRegistry 查询，不能只根据 GameObject 已消失推断后端已归还。

语言目录、格式化与订阅实现均属于本示例，只有导入 **Resource Integration** 后才编译。JSON 目录读取使用 Unity 的 `com.unity.modules.jsonserialize`（1.0.0）模块。MUI 的页面向导同样使用该模块，因此已由主包声明依赖。已有项目资源或本地化服务可直接接入公开 UI API，无需采用此实现。

已删除独立的同步 Prefab 提供方及其克隆存活能力标记。视图提供方统一实现 `IViewProvider.AcquireAsync`，返回 `Task<IAcquiredView>`；不需要实现同步创建或能力查询。可选预加载通过 `IPreloadViewProvider.PreloadAsync` 返回独立驻留凭证。`LoadedPrefabViewProvider` 在资源已驻留时直接返回完成任务，未驻留时等待加载，两种路径使用同一入口。

只有本地工厂的项目可以使用 `DelegateViewProvider` 的同步委托构造函数；适配器直接返回完成任务，不使用后台线程或强制等待帧。委托交付前负责失败回滚，调用方接管交付后的凭证；取消后迟到返回的凭证也不能丢弃。

语言目录提供方只实现 `ILocalizationProvider.LoadAsync`。内存目录立即交付独立凭证；JSON 目录在复制文本并归还源 TextAsset 后交付。没有独立同步接口或模式切换。
