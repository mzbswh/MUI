# MUI 入门

## 安装范围

目标版本为 Unity 2022.3.62f3。通过 Package Manager 的 Add package from disk 选择包根目录的 package.json；运行时代码保留在 Packages 引用中，示例由 Package Manager 的 Samples 独立导入。

Core 不引用 Unity；Resources、Navigation 与 ChildViews 分别定义资源凭证、页面导航和子视图生命周期。MUI.UGUI 提供原生 uGUI 的 Element 适配。TMP 与 Input System 位于独立程序集，项目按需安装对应包；基础页面不要求这些可选依赖。

## 从 Basic 开始

导入 Basic Example 并打开 Basic.unity。BasicPageViewModel 声明标题绑定与 Confirm 命令，BasicView.prefab 包含名为 Title 和 Confirm 的 Element。BasicExampleModule 声明生成注册入口，BasicDemo 负责注册、配置 PrefabViewProvider、初始化 UIHost 并调用 OpenAsync。

普通页面不要求 Presenter；需要跨服务协调时再引入。不要手写或编辑生成的绑定订阅、命令属性和 Route 接线。若看不到生成类型，先检查 Console 中的声明诊断，以及包内 Analyzers 的 RoslynAnalyzer 导入标签。

## 异步完成点

- OpenAsync 等待首次就绪及提交，不等待进入转场结束。
- 页面结果在关闭提交时确定，不能用默认业务值代替普通关闭原因。
- 需要等待退出画面时调用句柄的 WaitForExitAsync；资源实际清理使用独立清理等待入口。
- 场景主动退出前显式等待宿主 ShutdownAsync；BasicDemo 提供相应示例包装。可传入 CancellationToken 取消当前等待，退出及清理仍继续，后续调用等待同一次清理；传入已取消令牌也会先启动退出。OnDestroy 的兜底清理不能阻止场景销毁。

同步完成的 Provider 或业务委托仍使用同一异步契约，不使用 Task.Run 包装，也不调用 Wait/Result 阻塞主线程。新增页面以 Basic 为接入示例。

## 资源与输入

项目负责资源后端。Provider 交付的凭证交由框架持有并归还；直接传入 Unity 资源默认借用，不能按拥有资源销毁。取消只使旧请求不能附着到当前 UI，迟到资源仍需归还。

InputGate 控制新输入准入；临时关门不自动取消已接纳的业务命令。换绑和页面关闭会使旧 UI 身份失效，项目服务可继续持有需要独立完成的业务工作。

原生 Pointer 在失去输入资格、绑定退订和重新激活时同步失效。旧按下不能在恢复后产生点击，旧拖动不能转给复用条目或下层对象；滚动惯性与原生选区拖动走内部收尾。自定义捕获可订阅 `IInputGestureView.InputGesturesInvalidated`，只结束捕获，不派发点击、提交或 Drop；换绑准备阶段不会调用此清理入口。

## 公共文本控件契约

仅绑定公共内容时，使用 Core 的 `ITextElement`、`IInputFieldElement` 和 `IDropdownElement` 声明目标。例如 `[Bind("Title", nameof(ITextElement.Content))]` 与 `[Bind("Name", nameof(IInputFieldElement.Value), bindingMode: BindingMode.TwoWay)]`。Legacy 和 TMP 适配实现同一接口；替换 Prefab 中的原生控件和对应适配后，模型及生成绑定无需修改。标识缺失或同一标识命中多个兼容适配时仍明确报错，不自动添加后端组件。字体、排版及后端输入配置继续使用具体适配类型。

输入框 `CommitMode` 默认 `TextInputCommitMode.OnChange`，原生编辑变化立即通知反向绑定；`OnEndEdit` 只在有效的原生编辑结束时发布 Value，再派发 EditingEnded，失去焦点也属于编辑结束。模型到控件的更新始终生效，切换模式不提交已有草稿。门控关闭、换绑或页面退役期间的结束事件不执行业务写入。TMP 的 Submitted 仍是其后端专属通知，不代表输入法组合文本已经提交。

下拉框公共接口提供文本 Options、原生 Value、Interactable 和 SelectionChanged；模型赋值不发布用户选择事件。空选项和 TMP 占位项保留各原生后端的值语义。

## 嵌套属性路径

`Bind.SourcePath` 声明相对于被标注成员的属性路径。例如模型的 `Child` 属性指向另一个 ViewModel，可在该成员上声明：

```csharp
[ObservableProperty]
[Bind("Name", nameof(IInputFieldElement.Value), bindingMode: BindingMode.TwoWay,
    SourcePath = "Profile.Name", NullValue = "")]
private ChildViewModel child;
```

这会绑定 `Child.Profile.Name`。中间模型必须派生自 ViewModel，路径只包含属性名，支持普通公共属性、生成属性及继承属性；不支持索引器、集合索引或方法调用。普通属性的 setter 应发布对应属性名或全部属性通知。生成器在编译期校验访问权限及转换类型，生成访问器和逐层通知订阅，运行时不解析字符串或使用反射。

替换 Child 或 Profile 后，绑定同步移除旧链订阅并投影新值。任意中间模型为 null 时，目标使用 `NullValue`；未声明时使用目标类型默认值，转换器不会收到该缺失路径。`NullValue` 必须是可隐式赋给目标类型的属性常量，仅用于含前向更新的路径绑定。实际叶属性的值为 null 仍交给转换器，和中间模型缺失分别处理。

TwoWay 默认用模型初始化控件，路径缺失期间忽略反向写入。路径恢复不会回放旧输入；转换或读取期间替换路径，会使正在处理的旧输入失效，即使随后恢复同一个模型也如此。OneTime 仅投影首次读取的值，不订阅后续模型替换；OneWayToSource 初始路径缺失时跳过初始源写入，路径恢复后只接纳新的输入。叶属性读写使用本次捕获的拥有者，避免在提交输入时再次遍历已变化的模型链。

需要手写自定义绑定时，可向 `BindingBuilder.Property` 传入 `BindingSourcePath<TViewModel, TValue>`：其读写委托访问捕获的叶拥有者，`BindingSourceMember` 依次提供属性名和拥有者选择委托。选择委托必须在中间模型缺失时返回 null，使用所属 UI 线程，沿用同一绑定会话的退订与换绑规则。

## 主线程状态更新

`ObservableObject` 和 `ObservableList` 归创建线程所有。生成属性通过 `SetProperty` 在字段修改前检查线程；自定义 setter 应先调用 `RequireOwningThread()`。自定义模型或集合从后台发出的通知会被绑定与列表适配拒绝，不会更新 Unity 控件。

初始化宿主后可在主线程取得 `host.Dispatcher` 并交给项目服务。后台工作完成时通过 `await dispatcher.InvokeAsync(() => viewModel.Count = count, cancellationToken)` 提交状态；若调用已在所属线程，委托会立即执行。派发回调执行前检查到取消时会跳过委托。

## 验证状态

当前处于迁移阶段。先使用 Basic，再参考各示例 README 的导入依赖与未验收说明。完整完成标准见 [设计目标](../docs/DESIGN-GOALS.md)，逐项进度见 [实现记录](../docs/IMPLEMENTATION-STATUS.md)。编译通过不等于真实点击、资源归还或 IL2CPP 运行通过。

命令统一实现 `IUICommand`；立即完成的业务可传给 `new AsyncCommand(context => { /* 业务操作 */ })`，不需要独立同步命令。绑定入口要求可等待清理的激活作用域，以保证关闭或换绑时撤销命令来源并等待在途工作。

诊断接入：通过 `UIErrors.Sink` 接管错误输出；操作结果与导航事件/追踪的 `DiagnosticId` 可关联同一次异常，也可使用 `UIErrors.GetDiagnosticId(error)` 查询。查询不会触发报告。同一异常实例只报告一次，单项聚合复用原因标识，多项聚合逐项报告实际失败；重试产生的新异常有独立标识。错误出口和观察者抛错不打断其他观察者或框架清理。

在错误出口调用 `UIErrors.GetDiagnosticContext(error)` 可读取宿主、句柄编号、路由、操作与原始阶段，关闭时间线追踪也保留这些字段。上下文仅含值与有界字符串，按异常弱键保存，不保留页面、模型或资源。项目适配器可使用 `UIErrors.BeginContext(...)` 提供外部阶段，或 `AttachContext(...)` 在传播前补充位置；传播只填补缺失字段，不重写原始故障位置。Unity 默认出口将标识、上下文和异常栈合并为一条错误日志。

## 输入转换与校验

`IValidatingBindingConverter<TSource, TTarget>` 的 `TryConvertBack` 返回 `BindingConversionResult<TSource>`。无效输入返回 `Failure(message)`，保留原模型值和控件草稿；成功返回 `Success(value)`，模型 setter 完成后重新投影实际值，支持裁剪及规范化。已有 `IBindingConverter` 的反向转换异常也转为校验失败，取消异常仍传播。目标读取、模型 setter 和校验状态 setter 的异常属于实际绑定故障。

在反向 Bind 声明中设置 `ValidationProperty = nameof(AmountValidation)`，指定一个可写的 `BindingValidationState` 模型属性；它也可由 ObservableProperty 生成。默认状态有效，`IsValid` 和 `Message` 可用于派生可绑定文字或命令资格。校验状态目标必须与数据属性不同，且只有一个反向写入者；生成器在编译期检查。Settings 示例的 PlayerNameConverter 和 NameError 展示完整接入。

手写绑定通过 `BindingBuilder.Property` 的 `tryConvertBack` 与 `writeValidation` 使用同一机制。双向绑定先由模型初始化控件，提交后开启反向写入并重置校验状态；OneWayToSource 初始无效值只发布校验失败。后续模型变化重新投影并清除旧输入错误，解绑后旧输入不能发布状态。校验发布可能同步触发业务回调，回调不得等待自身绑定清理。

## 资源键与直接对象

ImageElement.Sprite、RawImageElement.Texture、TextElement.Font 和 GraphicElement.Material 可在资源键槽存在时直接赋值。赋值立即使旧请求失效，成功替换显示后归还旧凭证；异步归还仍由同一激活生命周期跟踪。直接 null 清空，直接对象只借用，不自动销毁。调用方必须覆盖整个显示期间的持有权；即使赋予槽已显示的同一对象，也须有独立持有权。

直接对象赋值将请求键和显示键都置空，非空对象的 Snapshot.State 为 Displayed，清空为 Empty；查询实际对象使用控件属性。被取代请求即使忽略取消，其迟到凭证也归还。Snapshot.CleanupFailure 和 CleanupFailureCount 保留旧归还、回滚及最终清空失败的诊断，不改写新资源的显示结果。释放失败仍使最终生命周期清理失败，重复等待不会自动重试。

## 清理责任与显式重试

`CleanupResponsibility` 记录归还责任的稳定 Id、拥有者标签、状态、错误、诊断上下文与尝试次数。状态区分未启动的 Retained、在途 Pending、失败或归还状态不明的 Failed，以及已确认的 Completed。`CleanupRegistry.CaptureSnapshot(maxEntries)` 按上限读取在途或失败记录，`UnconfirmedCount` 返回总数；完成记录立即移出全局账本，不长期保存成功历史。账本独立于 UIHost 组件，场景或作用域结束后仍持有未确认责任及回调。

`Navigator.CaptureSnapshot()` 提供稳定 `HostId`、`UnconfirmedCleanupCount` 和有界 `CleanupResponsibilities`；使用双参数重载可分别限制实例与责任采集数量，截断由 `CleanupResponsibilitiesTruncated` 标明。`HasCleanupFailure` 表示页面或缓存清理曾失败，旧属性 `HasUnconfirmedCleanup` 沿用此历史语义；显式重试成功后当前责任数量减少，历史失败及已交付的关闭结果保持原值。UIHost Inspector 分别显示这两类信息，不从快照启动重试。

组件销毁后可用保存的 HostId 调用 `CleanupRegistry.GetUnconfirmedCount(hostId)` 或 `CaptureSnapshot(hostId, maxEntries)` 查询当前责任；`Guid.Empty` 只查询没有宿主身份的责任，不是全部宿主的通配符。账本选择和每项状态读取不冻结异步完成，采集期间完成的项可能显示 Completed，下一次采集消失。责任登记保存值身份作备用；合法移交后首次归还采用实际所有者的上下文，公布到账本后不随重试改变归属。

`AcquiredResource<T>`、`AcquiredView` 和 `AcquiredPreload` 公开同一 `CleanupResponsibility`。后端能保证完整释放回调可幂等重复执行时，构造凭证显式设置 `supportsIdempotentRetry: true`，并用 `owner` 标明资源或池身份。回调必须记录部分完成的步骤：已经扣除的引用、已归还的池对象或已经请求的销毁不能再次执行。默认不允许重试，失败不能推断为未释放。

项目选择失败责任后调用 `await CleanupRegistry.RetryAsync(id)`，或直接使用凭证的 `CleanupResponsibility.RetryAsync()`。并发重试共享当前尝试，成功更新同一记录并解除其持有；已确认完成的责任不再次执行释放。线程约束沿用凭证的 `releaseThreadId`，线程拒绝不接管新的尝试。重试任务由账本持有，调用方负责观察其结果；没有自动重试调度或丢弃失败记录的入口。

重复 `DisposeAsync` 始终观察首次清理结果。显式重试成功不改写已经交付的关闭、Shutdown 或 LifetimeScope 失败任务，也不复活页面、作用域或输入；判断当前未完成责任使用账本快照。控件 Snapshot 的 CleanupFailure/Count 继续表示历史上已记录的错误。

`LifetimeScope.IsCleanupConfirmed` 查询当前实际责任是否均已确认，区别于首次释放任务是否失败。子作用域和已登记凭证的安全重试可使确认状态变化；没有责任记录的回滚错误或取消回调异常仍按未知处理。视图归还前检查激活和实例作用域，未确认时在账本中保留 `ViewInstance.DependencyRelease`。项目先显式处理允许重试的叶责任，再重试控件/模型持有者和视图的剩余步骤；外层检查不自动重试未知后端。已移出导航实例列表的失败页面仍占一个清理名额，同页多个责任不重复计数；没有页面身份的责任逐项保守计入，确认归还后才恢复新请求准入。

`LifetimeScope.OnDisposeAsync` 也可显式声明回调的幂等重试、拥有者标签和释放线程。未声明能力的普通清理回调、外部自定义凭证和同步清理按不可安全重试处理，失败仍保留对象与回调。自定义凭证若自己管理责任，应实现 `ICleanupResponsibilitySource` 并让释放入口更新该记录，框架不会再为它建立第二份归还责任。

自定义 `IBindingRebindTarget` 返回的准备目标也遵守同一清理契约：框架逆序释放已取得的候选，一项目标失败仍继续清理其他目标；失败目标及其回调由责任账本保留，并阻止所属页面 View 提前归还。默认不重复执行未知清理；目标能保证完整回调幂等时，可实现 `ICleanupResponsibilitySource` 公开自己的责任。显式恢复先处理目标的叶责任，再确认 `BindingRebindPreparation` 和外层视图责任。外层确认不重新执行目标回调，也不改写首次失败结果。自定义 `IBindingRebindPreparation` 返回的准备对象同样由页面激活持有清理责任。

参数更新和 Presenter 换绑返回的候选也登记到所属激活；未确认清理时保留父子 View。责任属性抛错或返回空值时，框架仍执行一次真实清理并保留登记异常；实际清理失败则保留不可安全重试的回退责任。直接调用子视图准备、参数更新和换绑 API，以及子视图生命周期回调，均沿用父页面的宿主、页面和路由身份。

绑定会话及 View 资源激活保存宿主、页面句柄和路由的值快照。导航回调结束后的模型通知、命令执行和资源换键仍沿用所属页面身份；前向、反向、路径通知、命令刷新、加载、原生赋值、通知、回滚及归还使用各自阶段。绑定故障保留异常传播并向错误出口报告一次；输入转换拒绝和加载取消仍属于可预期分支。错误上下文不持有 View、宿主组件或模型。

无法确认原生 setter 失败后的引用时，槽冻结并暂停相关 Graphic 渲染，保留可能仍被引用的凭证；最终清理先清空原生引用再归还。框架不会自动恢复该控件的渲染。新的资源拥有者须等原槽成功清理后配置；TMP 换 FontAsset 可能间接替换材质，因此仍拒绝在材质槽持有期间执行该操作。

## 固定容量内容

页面缓存只持有解除绑定并重置的 View 凭证，每次打开仍创建新的模型、Presenter、实例作用域、激活、句柄和结果。正常关闭清理模型与 Presenter 的实例资源后才尝试缓存，外部传入模型仍只借用；故障关闭、强制关闭与宿主退出直接归还。缓存命中不重复获取 View，但会重新执行当前激活所需的资源配置；重置或重新配置失败的 View 淘汰并归还，不继续复用。

渲染器通过 `ICacheableView.ResetForCache()` 清除旧模型引用、焦点、输入与资源上下文；凭证通过 `ICacheableViewAcquisition` 显式保证必要后端依赖能延长至实际归还，并通过 `PrepareForReuse()` 注入当前配置。`AcquiredView` 默认 `supportsCaching: false`，项目后端满足此保证才显式启用。UGUI View 和 PrefabViewProvider 已支持；Prefab 的配置回调在缓存命中后再次执行，须允许重复配置。旧 `IReusableViewPresenter` 标记已弃用，不能使 Presenter 或模型跨关闭保留。缓存未接管任何旧实例的 LifetimeScope 或业务资源。

自定义 UGUI Element 可覆盖 `OnResetForCache()`，解除自己保存的模型、参数和活动引用。此钩子在旧业务实例已清理、View 隐藏且不可交互时同步调用，不能启动新工作；抛错时整个 View 淘汰并执行最终释放。

少量已有槽位或显式挂点可使用 [固定槽位列表](slot-lists.md)，共用子视图生命周期与候选换绑。
