# Changelog

## [Unreleased]

2026-10-09 完成当前设计的运行验收；各条源码变化按实现阶段保留，最终状态与证据见 docs/IMPLEMENTATION-STATUS.md。本节仍为未发布记录。

- Settings、Tabs 与 Navigation 的示例菜单创建场景时保留模板相机，避免 Game 视图的无相机提示覆盖 Overlay UI；共用入口的奖励列表与资源示例同时受益。

- 同步包内生成器 DLL 与当前 Release 源码产物；八项示例的 24 份生成源码逐字一致。刷新当前 Runtime 的固定/测量双模板桌面 IL2CPP 性能基线，并记录原生组合容器的停用、恢复、父关闭、迟到归还及显式清理恢复证据。

- 普通打开的 Route 实例满额统一返回 Busy，不再把内部 InstanceLimit 接纳信号直接交给调用方；准备中的单实例同样不启动第二份获取。显式 CloseOldest 仍经过旧页守卫，没有可替换的就绪旧页时返回 Busy。

- 普通关闭/返回、Done 与 Replace 共用激活上的关闭意图记录，同意图的普通关闭合并观察，不同意图返回 Busy。守卫许可同时校验业务版本和框架提交版本；确认期间允许参数/绑定提交，旧许可返回 Superseded，不关闭已更新的页面。嵌套通知退订失败保留旧拥有者并阻止解绑误报成功，失败订阅和最终清理回调沿统一责任账本保留，不自动重试未知项目代码。

- 活动原生 View 被外部销毁时，先撤销身份并通知拥有者，再异步观察最终清理，避免通知中的故障关闭等待同一清理责任；慢归还只等待已启动的激活清理，不提前释放或隐式重试。缓存重置清除激活引用。SafeAreaFitter 检查有效 Camera 配置；真实窗口 IL2CPP 合并验收覆盖场景、Unity Resources 后端、CanvasScaler、横竖分辨率与安全区/键盘坐标。

- 页面向导补齐 Notice 非交互提示预设，生成纯文本 Prefab 与现有路由策略，不生成关闭命令、不要求 Presenter；已有资产关联按提示边界校验且不修改原 Prefab。导入/保存后的校验按依赖路径合并，编译、导入和 Play Mode 忙碌时排队，域重载后补跑；手动和构建检查始终重新采集全部目录，共用规则，不读取旧增量结果。

- 虚拟列表父激活结束时，清空条目后仍观察到的旧准备取消不再作为另一项清理失败聚合报告；目标 setter、真实节点归还及未取消的准备故障仍保留诊断。相关焦点与普通/虚拟列表清理场景采用同一验收批次。

- 资源槽、迟到加载及预加载归还复用公开的 CleanupRegistry.ReleaseAsync(resource, owner, lifetime)，只捕获一次项目责任并登记真实确认条件；安全叶恢复可确认所属作用域，首次清理错误保持原值。责任属性异常不覆盖已经显示的新资源，未知后端回滚保留独立不可重试责任。控件资源拥有者只在本次释放前已确认子责任时完成剩余解除持有，避免吞掉首次错误。

- UIHost 拥有的提供方接入独立稳定清理责任，退出接纳后即可查询迟到准备的等待阶段；原生宿主销毁不丢弃失败提供方和归还回调。提供方退出按当前未归还责任判断，历史导航错误不永久阻止已确认资源的剩余释放。未知同步/异步释放失败只尝试一次，源凭证显式确认后可完成外层依赖；责任属性异常仍执行真实释放，直接等待自身 Shutdown 明确拒绝。导航快照的预加载占位反映已确认叶归还。

- 预加载凭证及批次直接持有同一稳定归还责任，明确安全重试确认后恢复容量；未知部分归还和取消回调异常继续保留责任与额度。共享请求分别登记消费者，只有最后一个消费者取消才停止底层准备；后台取消不因解除登记同步阻塞 UI，Ready 后的取消不撤销批次驻留。清除等待接纳时已在途批次及迟到凭证，不消耗后来接纳的新请求。

- 缓存估算预算跟随稳定归还责任：安全重试确认后恢复额度，历史错误保持原值；未知部分归还不因原生节点消失而释放预算。缓存凭证的责任属性抛错或返回空值时仍通过不可安全重试的回退责任执行一次真实归还，失败保留对象和回调；预算查询不触发项目回调。

- 参数更新的提交许可归还失败接入稳定清理责任，并登记到所属激活；候选归还成功不再覆盖已有失败状态。许可登记异常仍继续候选、输入阻挡收尾及结果发布，未知许可回调不重放。整理全仓库既有成员布局、控制流大括号及源码格式。

- View 的最终控件清理接入 LifetimeScope 和稳定责任，初始化失败与正常释放共用路径；原生节点销毁等待各 View 的最终责任。Prefab 正常归还、创建回滚及 staging 退出分阶段保留责任；加载提供方恢复不重复后端引用扣减。列表最终清理保留各节点责任，叶责任确认后只完成剩余依赖检查。View 首次清理异常采用单一 catch 分支，避免本轮 IL2CPP 生成代码中的过滤 catch 重抛落入后续 catch 而被吞掉。

- Element 增加 CreateProperty、Track 和 TrackCleanup 扩展入口，属性、同步资源及原生监听共用最终 LifetimeScope；初始化失败逆序收尾并保留未知失败责任。Element 公开稳定清理责任，重复释放保持首次结果；自定义属性检查线程、生命周期和比较器重入，结束后解除值、比较器及回调引用。补充自定义 Slider 与生成双向绑定接入说明。

- 列表候选子视图清理失败时保留隐藏节点，确认所有借用凭证归还后才执行节点收尾；开始最终清理的节点拒绝新子内容。普通/虚拟列表准备对象提供稳定清理责任，显式恢复只确认叶责任及节点，不重复未知退订回调。固定槽位保持借用节点与模板实例的所有权区别。

- 参数更新和 Presenter 换绑候选接入激活清理责任，未确认归还时保留父子 View；责任属性异常仍执行一次真实清理。子视图准备、参数更新、换绑及生命周期回调沿用父页面诊断身份，避免直接调用时丢失或串用其他宿主身份。

- 换绑准备目标及外部绑定准备对象接入统一清理责任；回滚释放失败保留对象和回调，阻止父 View 提前归还。显式恢复只确认已登记的叶责任，不重复未知清理，也不改写首次失败结果。BindingPreview 拆为独立文件，分离绑定构建与候选清理职责。

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
