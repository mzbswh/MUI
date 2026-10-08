# Changelog

## [Unreleased]

当前重构版本尚未完成运行验收；以下记录源码变化，不代表正式发布。

- 换绑提交前因来源、布局或资格失效而取消时，保留旧绑定并返回取消结果，不再记录预期取消异常；已经解绑或候选清理失败时继续保留故障诊断。

- 统一 ChildViewScope 的普通释放与直接责任释放完成状态；直接责任入口同样拒绝子生命周期自等待，显式恢复不改写首次结果。普通本地释放维持同步完成，不额外增加等待帧。

- ChildViewScope 与绑定会话区分首次清理失败和当前真实归还确认，保留稳定依赖；子容器显式重试仅检查依赖，修复子项历史提交失败导致顶层 View 无法归还的问题。

- 页面缓存拆为独立 View 凭证，关闭清理旧模型/Presenter/实例资源，命中创建新业务实例。加入 ICacheableView 与 ICacheableViewAcquisition 显式重置及后端依赖保证；UGUI 清除旧输入/焦点/资源上下文，Prefab 缓存命中重新配置，失败实例淘汰。IReusableViewPresenter 保留兼容声明并标为弃用。

- LifetimeScope 区分首次清理结果和当前责任确认，视图归还等待清理依赖；模型释放失败继续持有模型，资源与模型持有者可在叶责任确认后显式完成剩余步骤。失败页面移出导航账本后仍占清理容量。

- 绑定会话和资源激活保存页面诊断身份，独立模型通知、异步命令、资源加载/赋值/通知/回滚/归还保留具体阶段；资源加载取消只更新状态，不写错误日志。
- CleanupRegistry 增加宿主筛选，导航快照和 UIHost Inspector 分别呈现历史清理失败及当前未确认责任。显式重试成功更新当前账本，不改写首次关闭结果；责任归属支持合法移交后的实际释放所有者。

- 页面首次就绪、进入/退出转场和最终清理使用独立完成状态。
- 关闭提交固定业务结果；后续转场与清理失败不覆盖该结果。
- IModalView 增加 PrepareModalBarrierAsync；模态打开在提交前等待原生射线深度，透明准备阶段不拦截输入。隐藏屏障保留就绪节点以支持恢复。模态路由使用 OpenAsync，旧纯同步打开入口不再尝试创建未就绪屏障。
- Provider 获取统一为 AcquireAsync；本地委托适配可以立即完成。旧同步宿主和生命周期分支仍待移除。
- 资源接入示例删除无调用方的 SynchronousLoadedPrefabViewProvider 和 ISynchronousInstantiableResourceLoader，保留统一 LoadedPrefabViewProvider 的驻留、预加载与版本管理。
- 本地化资源示例删除独立同步目录接口、同步 JSON 加载和内存 Load 入口；统一 LoadAsync。LifetimeResourceExtensions 删除无人调用的同步 Load。
- BorrowedViewProvider 删除独立同步创建和能力查询，使用统一 ViewLease；移除无调用方的 ChildViewScope.Prepare 兼容入口。
- 独立子视图示例改为 LocalChildDemo / LocalThingPresenter，准备、参数更新、换绑、停用、恢复和销毁使用统一协议；删除 ChildViewScope.PrepareSynchronous 入口。
- ChildViewTemplate 删除同步准备与同步生命周期能力标志；清除无调用方的子视图同步凭证接管和模型创建分支。
- 子视图删除同步 UpdateArgs、Rebind、Deactivate、PrepareReactivation 及内部事务入口，只保留对应异步操作和共享状态校验。
- ChildViewScope 仅实现 IAsyncDisposable，删除同步释放、能力查询、模式属性与重复清理状态；构造时拒绝旧同步 Lifetime，调用方只需验证线程归属。Tab、Slot、动态内容、嵌套和列表移除重复模式检查。
- ChildViewHandle 删除同步释放，RequestClose 统一进入独立异步清理；删除同步子关闭队列、IChildRequestHost 及导航和 View 的子请求帧派发，保留业务 Tick 与命令排空。
- TabContentController 和 TabContentDefinition 移除独立同步 API 与选择、定义更新、缓存、销毁分支；本地 Prefab 使用同一异步协议并可立即完成。TabBar 与重试按钮统一调用异步入口；旧纯同步父 Scope 不再接入 Tab 控制器。
- DynamicViewElement 移除独立同步配置、结果和刷新分支，统一通过 Configure、PendingChange 与异步子视图协议执行。ContentViewProvider 移除同步创建协议，只保留 AcquireAsync 和统一的失败归还路径。
- NestedViewElement 删除独立同步准备、替换和恢复分支，统一等待旧子项清理再绑定借用 View；相关嵌套、回收列表和虚拟列表示例迁移到默认激活协议。
- RecyclingListElement 统一刷新和条目准备路径；修复条目命令删除自身时，子视图清理继承命令上下文而误判自等待的问题。
- VirtualListElement 移除独立同步刷新、定位和重试 API，统一使用 ScrollToKeyAsync / RetryAsync；父 View 与子控件只保留任务准备协议。
- ChildViewSlot 移除独立同步替换、换绑、清空和释放协议，统一维护异步事务与退役清理。
- 标准对话框路由不再声明同步生命周期，LocalDialogsDemo 使用统一导航并等待真实清理后展示后续提示。DialogService 接受业务结果的 Pending 清理快照，等待清理完成后归还串行许可，不再把正常延后清理误报为确认失败。
- Basic Example 使用无需 Presenter 的 ViewModel、生成绑定及异步导航。
- void、Task、ValueTask 命令统一生成 AsyncCommand；移除 SynchronousCommand / ISynchronousUICommand。手写命令绑定仅接受 IUICommand，Dialog 命令使用立即完成委托。
- 移除框架内的分页来源、分组及树形数据模型；项目组织扁平列表数据。
- 虚拟列表增加横向单列、视口通知、索引与对齐定位、平滑滚动、输入中断和末尾跟随。原 Height 尺寸 API 改为 Extent；测量入口改为 ConfigureSizeMeasurement 和 InvalidateSizeMeasurements。旧 Prefab 尺寸配置通过字段迁移保留。
- 增补 UPM 入门、列表接入、实现与验收说明；保留原 MIT 授权内容，许可证文件规范为 LICENSE.md。

离线编译、Editor 运行与 Player 验收的具体覆盖见 docs/IMPLEMENTATION-STATUS.md。

- UnityResourcesLoader 仅保留 IResourceLoader / LoadAsync 和统一 ResourceLease；ResourceImageDemo 删除模式开关，统一准备、绑定和清理，延迟为零时仍允许立即完成。
- View 与 Image/RawImage/Text/Graphic 删除同步资源配置入口，资源上下文只借用一份 IResourceLoader；准备诊断和 Inspector 删除旧加载器模式字段。
- ResourceSlot 与 ElementResourceOwner 删除同步执行、释放和能力查询；控件统一登记异步持有权，保留失败旧值、迟到归还及原生引用解除顺序。
- 删除已无调用方的 ISynchronousResourceLoader；奖励列表示例改用统一 ResourceLease。同步 Prefab 回滚异常尚未移除。
- LocalDependenciesDemo 替代共享依赖同步示例，保留脚本 GUID、路由图菜单及可选依赖演示，导航和参数候选统一使用异步协议。
