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

无法确认原生 setter 失败后的引用时，槽冻结并暂停相关 Graphic 渲染，保留可能仍被引用的凭证；最终清理先清空原生引用再归还。框架不会自动恢复该控件的渲染。新的资源拥有者须等原槽成功清理后配置；TMP 换 FontAsset 可能间接替换材质，因此仍拒绝在材质槽持有期间执行该操作。

## 固定容量内容

少量已有槽位或显式挂点可使用 [固定槽位列表](slot-lists.md)，共用子视图生命周期与候选换绑。
