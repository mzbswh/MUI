# 设计目标实现记录

验收依据为 [DESIGN-GOALS.md](DESIGN-GOALS.md)。本文件记录实现差距与验证范围，不替代设计目标，也不把编译成功视为运行验收。

## 当前已确认的设计冲突

以下依据当前源码核对，不因历史编译或局部运行通过而视为完成：

- 目标第 1、4 节的 `LifetimeScope` 与核心统一异步入口已落实；部分独立业务模块、渲染适配和示例仍超出目标范围，完整交付尚未完成。
- 目标第 3 节的参数提交异常行为已修正为故障关闭，并取得导航运行证据；同一提交异常经结果发布及聚合清理的重复报告已修复。Provider、绑定、参数提交及资源归还的宿主/句柄/操作/阶段上下文已取得关闭追踪时的运行证据；其他失败链仍待核对。
- 目标第 2、4 节要求换绑准备失败保留旧界面、外部等待不占导航队列。换绑及参数更新现在释放队列执行候选准备，提交前重新取许可；静态嵌套、普通回收列表与虚拟列表已接入提交前候选准备。取消、重入、失败恢复和实际资源归还的完整运行矩阵仍未验收，不能将本轮编译视为契约完成。
- 固定槽位和虚拟条目失败隔离已补上运行时、Editor 和示例接入，尚未完成真实交互验收；虚拟列表原生方向导航、异步焦点恢复仲裁和按内容版本迁移的动态尺寸缓存已实现，仍需完整运行矩阵。
- 目标第 2 节的 Legacy/TMP 公共控件契约、输入提交模式和转换校验已补齐，并取得同一生成模型的 Play Mode 与 IL2CPP 运行证据；实体输入、输入法及换绑交错的完整矩阵仍待验收。
- 目标第 2 节的嵌套属性路径已加入初步实现：BindAttribute.SourcePath/NullValue、编译期强类型访问器及运行时通知链，用于嵌套模型替换、缺失路径回退和反向写入门控。该实现仍待完整运行验收，尤其是转换/订阅重入、换绑、清理异常及跨程序集声明；静态子 View 的既有观察不能作为新路径契约完成的证据。
- AcquiredView / AcquiredResource 命名已迁移；资源键状态快照和非法列表测量回退已接入，仍须验证运行时迟到结果、资源归还与布局交互。

后续实现优先消除上述契约冲突，再按目标第 2 至 7 节补齐能力，最终按第 8 节逐项验收。不能以删除文件数量或局部编译通过替代目标完成率。

## 当前基础

### 2026-10-08 父页与静态子视图换绑矩阵

- 通过官方 Sample.FindByPackage/Import API 重导入全部八个 Sample，刷新 Settings 校验及 Navigation 命名。日志 `/private/tmp/mui-current-samples-import-20261008.log`；不作为 GUI Add package from disk 或八个示例完整交互验收的证据。
- 临时桌面场景使用实际 Navigator、View、NestedViewElement、BindingContext 和主线程受控准备目标，完成七组观察：同帧本地换绑与 null 隐藏/恢复；嵌套准备失败保留旧 UI 并允许重试；忽略取消的迟到候选仅释放不提交；候选外部等待不阻塞同 Navigator 的其他页面关闭；父关闭期间不接管迟到候选；准备回调内换绑明确拒绝 Reentrant；嵌套提交已改写目标后抛错时故障关闭父页。七组 Play Mode 均通过，日志 `/private/tmp/mui-rebind-matrix-play-20261008.log`。
- 候选准备期间原父/子模型与输入保留，失败后旧子模型继续更新文字，候选模型不写旧 UI；成功后旧模型通知不再写子控件。原工厂父模型释放一次，借用的新父模型及所有子模型不被页面误释放；候选 Prepared/Committed/Disposed 与取消或提交结果匹配。关闭与原生节点清理由父所有者完成。两项有意注入故障各报告一次，均携带 host/view/route/operation 及 RebindPreparation 或 RebindCommit 阶段。
- 同一七组场景构建 macOS arm64/x64 IL2CPP Development/High stripping、960×640 Player；BuildReport Succeeded/errors=0/warnings=0，运行退出码 0，cases=7/reports=2。构建日志 `/private/tmp/mui-rebind-matrix-il2cpp-build-20261008.log`，运行日志 `/private/tmp/mui-rebind-matrix-il2cpp-player-20261008.log`。退出仍有 13 条 allocator 提示，不报告干净退出。
- 观察源码为 `/private/tmp/MuiRebindMatrixObservation.cs` 与 `/private/tmp/MuiRebindMatrixObservationMenu.cs`，未作为仓库测试发布。首轮把 Child.Text 作为 Property 的通知属性名，导致 null 回退观察失败；该 API 不解析路径，改为 Child 绑定及转换器处理 null 后通过。该结果仅证明静态子 View 与现有转换器路径，不证明设计目标要求的可声明嵌套属性路径、回退值和 null 反向写入门控；已将其明确列入当前设计冲突。
- Navigation README 修正拒绝模型时“恢复旧绑定”的过时说明，与提交钩子异常导致故障关闭的实际示例一致。尚未覆盖多层子容器、普通/虚拟列表候选换绑的全部异常组合，以及换绑后的清理故障恢复；完整目标仍在执行，未自动提交。

### 2026-10-08 虚拟列表统计与性能基线

- 新增 MUI.VirtualList.Maintenance，将 LateUpdate 和刷新迭代器的同步区间合并计时；同步嵌套与跨列表重入只计一次，标记不跨异步等待。各细分标记及 MeasurementSnapshot 的帧号、尝试计数、饱和累计值在虚拟列表文档公开说明。
- 在既有桌面本地 UPM 工程运行 100/1,000/10,000 项 × 固定/测量尺寸，两种模式均有双模板和原生 Image/RectMask2D。Editor 六组功能观察通过；多数 ProfilerRecorder LastValue 为零，舍弃其 CPU/GC 结果。
- 最新 macOS 通用 IL2CPP Development/High stripping、960×640 Player 构建 Succeeded/errors=0/warnings=0，运行退出码 0。六组共 3,600 帧有效采样，无 Maintenance 零值缺口；每帧 Ready、无条目失败、物化和模板实例总数最多 11、测量最多 4 次，Shutdown 后原生子节点归零。10,000 项测量累计 1,134 次，未物化全部前置条目。退出仍有 13 条 allocator 提示，不报告干净退出。
- 框架维护 P95 为 1.537–2.681 ms，P99 为 3.877–4.021 ms；整帧 GC 均值约 94–100 KiB，尚未独立归因。业务绑定单独采样，资源加载次数为零；完整环境、逐组分位数、计数与局限见 [性能基线](VIRTUAL-LIST-BASELINE.md)。目标设备与预算未指定，仅记录基线，不宣称性能达标。
- MUI.UGUI 离线编译 0 警告、0 错误；4 个相关文件的成员布局、大括号及空白检查通过。README 清除“Basic 尚无任何运行验证”和 Provider 仍为目标契约的过时表述，继续区分历史键盘证据与完整示例验收。未新增仓库测试或自动提交，完整设计目标仍在执行。

### 2026-10-08 当前提交检查点

- 最新资源与转换验收加入校验通知同步修改源模型的重入场景，Legacy/TMP 均通过；有效的新模型更新优先于旧无效草稿。Play Mode 日志 `/private/tmp/mui-completed-contracts-reentry-play-20261008.log`。
- InputSystemUIInputModule 使用模拟 Mouse/Keyboard 和实际默认动作，验证原生 Escape 返回、退出转场屏障、关闭时按下保持至释放及后续新点击恢复。Play Mode 日志 `/private/tmp/mui-inputsystem-module-play-20261008.log`。这是模拟设备输入模块证据，不等同于实体硬件验收。
- 上述两组场景在同一 macOS arm64/x64 IL2CPP Development/High stripping Player 中通过，退出码 0；BuildReport Succeeded/errors=0/warnings=2。日志 `/private/tmp/mui-completed-contracts-inputsystem-il2cpp-build-20261008.log` 和 `/private/tmp/mui-completed-contracts-inputsystem-il2cpp-player-20261008.log`。退出仍有 13 条 allocator 提示，完整目标仍未完成。
- Navigation 示例统一采用 NavigationPageViewModel/NestedPageViewModel/SharedStatus 命名，保留重命名脚本的 meta GUID；Navigation Editor 离线编译 0 警告、0 错误。
- VirtualList 新增六个分阶段 ProfilerMarker 和只读 MeasurementSnapshot，记录维护帧及累计原生测量尝试次数；Profiler 作用域在同步刷新片段结束，不跨异步等待。最新 MUI.UGUI 离线编译 0 警告、0 错误。此统计改动尚未完成 Unity Play Mode、IL2CPP 和性能基线验收，不能据此宣称性能达标。
- 当前提交为实现检查点；完整换绑/异常矩阵、八个示例的视觉和交互验收、性能基线及剩余文档核对仍待完成。历史条目中的“未提交”描述保留其记录时的状态。

### 2026-10-08 资源直接赋值与反向转换校验

- 四类资源属性 Sprite/Texture/Font/Material 复用原 ResourceSlot 的代际、赋值和清理路径。直接赋值即刻替换画面，取消在途键请求；相等对象也使请求失效，不保留旧凭证。旧归还异步等待由原槽及所属 LifetimeScope 跟踪，直接对象只借用。请求键及显示键均置空，非空直接对象状态为 Displayed，null 为 Empty。拒绝已销毁的 Unity 对象；TMP FontAsset 的间接材质写入仍保持原约束。
- ResourceSourceSnapshot 新增 CleanupFailure/Count，分别呈现旧归还或回滚错误与当前请求结果。无法确认原生赋值后的引用时，暂停相关 Graphic 渲染并冻结槽，保留候选及旧凭证；最终清理确认清空后归还。修正文档先于实现错误声称支持直接 Sprite 的文字，并以实际实现更新资源说明及原失效链接。
- Core 新增 BindingValidationState、BindingConversionResult<T>、IValidatingBindingConverter<TSource,TTarget>。BindingBuilder 只将反向转换调用失败转为校验状态，取消仍传播，目标读取及模型/校验 setter 故障不被吞掉。无效输入保留模型值与草稿；有效输入重新投影模型规范化值并清除校验。源模型更新、初始 TwoWay、初始 OneWayToSource 和解绑沿同一会话代际处理。
- Bind.ValidationProperty 声明一个独立可写的模型校验状态属性。生成器支持两种转换器，校验类型、反向模式及单写入者，并在生成属性元数据中保留状态声明供跨程序集继承。Settings 示例公开 PlayerNameConverter、NameValidation 和 NameError，空名称保留原模型、显示错误并阻止 Save；有效名称去除首尾空白。Analyzer Release 产物已同步。
- 桌面 Unity 2022.3.62f3 临时 Play Mode 场景验证：7 份获取凭证全部归还；A 归还等待期间直接替换立即生效，B 忽略取消迟到后被归还而不覆盖 C；相等直接对象同样使键失效，最终直接借用对象仍存活。Legacy/TMP 共享生成绑定均通过初始化、无效草稿保留、有效值规范化、旧抛错转换器恢复、OneWayToSource 初始失败不回写、模型重置与解绑检查。独立故障验证旧归还失败保留新显示和清理诊断，最终清理报告失败；原生材质赋值后抛错时冻结、停用渲染、保留两份凭证，并在最终清空后归还。两次注入故障各报告一次。日志 `/private/tmp/mui-completed-contracts-faults-play-20261008.log`；首轮临时脚本因 MUI/TMP 同名 ITextElement 未限定而编译失败，限定为 MUI.ITextElement 后已排除，非包编译故障。
- 独立 IL2CPP Development/High stripping、macOS arm64/x64、960×640 Player 完成同一组观察，结果与 Play Mode 一致，退出码 0。产物 `/private/tmp/mui-completed-contracts-il2cpp-20261008.app`；BuildReport Succeeded/errors=0/warnings=1，原生链接及 Clang 参数警告仍在构建日志 `/private/tmp/mui-completed-contracts-il2cpp-build-20261008.log`。运行日志 `/private/tmp/mui-completed-contracts-il2cpp-player-20261008.log` 记录 PASS/reports=2，退出仍有 13 条 IL2CPP Free after allocator was destroyed，不能记录为干净退出。此前无 MUI 的空白 Player 已复现同类提示，未定位 Unity 内部原因。
- Settings 及依赖离线构建通过，0 警告、0 错误；23 个相关文件的成员布局、大括号和空白在临时副本检查并以补丁回写。以上是生成绑定、公开资源 API 和原生属性/事件运行证据，不包含 Settings 新错误文字的实体输入与视觉验收，也不覆盖全部故障、换绑或诊断上下文。未新增仓库测试或提交，完整目标仍未完成。

### 2026-10-08 Legacy/TMP 公共控件契约与提交模式

- Core 新增 ITextElement、IInputFieldElement、IDropdownElement 与 TextInputCommitMode，不引用 Unity 或 TMP。两种后端分别实现同一 Content、Value、Options、编辑资格及公共编辑/选择事件。ElementIndex、生成器和 Editor 沿现有兼容类型解析，缺失及多义仍失败，不增加后端扫描或自动控件创建。
- Legacy/TMP 输入框公开 CommitMode，默认 OnChange；OnEndEdit 保留原生草稿，结束时先通知 Value 再执行 EditingEnded。模型赋值始终生效且不发布原生用户事件，切换模式不提交草稿。非法配置在运行时初始化及共用 Editor 校验中拒绝。下拉公共 SelectionChanged 只在有效用户选择及 Value 通知后发布，模型写入不发布。
- Basic 标题改为 ITextElement.Content；页面向导为实现公共接口的文本适配生成同一接口声明，第三方专属文本类型仍使用其显式契约。Documentation~/index.md 补充声明及两种提交模式用法。
- 桌面工程中同一 CommonControlsModel 通过包内生成器生成绑定与两项命令，分别用于 Legacy/TMP 页面。Play Mode 两种后端均记录 initial=Initial/nativeChanges=0、草稿期间模型仍为 Initial、结束后 Draft/events=1、逐次编辑后 Live；模型回写原生 events=0；用户下拉选择 Choice=2/events=1，模型设置索引不增加事件；门控关闭后的草稿及结束通知没有回写或新增事件。日志 `/private/tmp/mui-common-controls-observation-20261008.log`。这是原生属性/UnityEvent 路径，非物理输入或输入法验证。
- 主 Editor、TMP 和 Basic Editor 依赖离线编译成功，0 警告、0 错误；14 个相关文件的成员布局及大括号检查通过。新增契约后，Runtime、Editor、Analyzer 与 Samples 的 709 个 meta 无非法、重复或缺失。
- 在桌面工程构建三场景 IL2CPP Development Player，High 托管裁剪、macOS arm64/x64 通用产物，960×640，实际 Metal 设备 Apple M4 Pro。TMP 首跑因 Essentials 未完成落盘出现两次 TMP_Settings 空引用；改用官方导入 API 并等待设置/字体都可加载后重建。最终 BuildReport errors=0/warnings=0，Clang 命令行警告仍在日志中，不宣称整个构建日志无警告。
- 最终 Player 实际运行固定槽位、原生列表和 Legacy/TMP 公共生成绑定，结果与上述 Play Mode 一致；固定槽位父关闭资源 held=0、模板节点清除而借用节点保留；单项失败占位及重试清除正常；各场景显式等待 Navigator.ShutdownAsync 后结束。Player 退出码 0，最终运行日志无 NullReferenceException/未处理业务异常；退出仍有 11 条 IL2CPP Free after allocator was destroyed，原因未确认，不能报告干净退出或全部验收通过。产物 `/private/tmp/mui-current-contracts-il2cpp-20261008.app`；日志 `/private/tmp/mui-current-contracts-il2cpp-rebuild-20261008.log`、`/private/tmp/mui-current-contracts-il2cpp-player-final-20261008.log`。这项验证通过公开 API、原生 Move 和 UnityEvent 驱动，不替代物理输入或全部 Samples 视觉验收。
- 未新增仓库测试文件或提交，完整目标仍在执行。

- 退出提示对照：另建 `/private/tmp/mui-il2cpp-baseline-20261008` 空白 Unity 2022.3.62f3 工程，不安装 MUI，只在第 5 帧 Application.Quit。相同 IL2CPP/High/Development、960×640 配置的空白 Player 退出码 0，运行记录 MUI loaded=False，仍有 13 条 IL2CPP Free after allocator was destroyed。日志 `/private/tmp/mui-empty-il2cpp-baseline-player-20261008.log`。证明该提示不要求 MUI 代码参与；未确定 Unity/IL2CPP 内部原因，也不能据提示条数比较实际内存或确认框架资源是否归还。

### 2026-10-08 桌面 UPM、固定槽位与原生列表观察

- 在桌面新建独立工程 `/Users/mzbswh/Desktop/MUI-Acceptance-20261008`，Unity 2022.3.62f3；通过 `UnityEditor.PackageManager.Client.AddAndRemove` 安装本仓库本地包，解析来源为 Local。未导入 Samples 时，真实 Editor 编译并加载 Core、Resources、Navigation、ChildViews、uGUI、Editor，Assets 没有 Runtime 副本。日志 `/private/tmp/mui-desktop-empty-20261008.log`。此为 Package Manager API 安装，不是 Add package from disk 的 GUI 点击证据。
- 通过 Sample API 导入全部八项登记示例，安装 TMP 3.0.9 与 Input System 1.19.0 后真实 Editor 编译通过；确认 MUI.TMP、MUI.TMP.Themes、MUI.UGUI.InputSystem 及全部示例程序集已加载。日志 `/private/tmp/mui-desktop-samples-20261008.log`、`/private/tmp/mui-desktop-optional-compile-20261008.log`。一次性安装脚本仅留在验收工程 Assets/Editor；可选控件和 Input System 的实际交互仍待验收。
- 固定槽位 Play Mode 观察覆盖挂点模板及已有借用节点：初始五槽三项、清空后活动项为零且节点仍为五个；六项超量赋值被拒绝，原来源保留；父级换绑 Applied，两项显示且原节点保持；父关闭清理 Complete、子项全部解绑、资源持有为零。最终 View.Dispose 后模板节点剩零、借用节点保留五个。日志 `/private/tmp/mui-slot-observation-20261008.log`；并非真实指针交互证明。
- 原生 EventSystem Move 派发验证 Primary→Secondary、跨条目保留 Secondary；Slider 与 Scrollbar 的 Right 将值从 0.5 改为 0.6且保留条目键；三列 Grid 的 Down 从 1 到 4，Right 跨条目从 4 到 5。列表条目绑定故障保持整表 Ready，failed=1 且视口有一个占位；聚焦失败目标返回 Failed，显式 Retry 返回 Ready/failed=0，换源后无旧失败记录。显式 Shutdown 正常结束。日志 `/private/tmp/mui-native-list-observation-20261008.log`；不替代物理键盘及延迟条目方向竞争验收。
- 修正 PointerDown 回调内失效、随后恢复门控时的拖动重新捕获；输入模块在回调结束后写入的 pointerDrag 由 InitializePotentialDrag 按原按下资格撤销。复跑旧点击、绑定切换、拖动与重入观察，pointerDownReentryDrag=False/eligible=False。日志 `/private/tmp/mui-gesture-reentry-20261008.log`。
- Runtime、Editor、Analyzer 与 Samples 的 705 个 meta 无非法或重复 GUID，脚本、程序集及目录未缺 meta。未新增仓库测试文件或提交；完整目标仍在执行。

### 2026-10-08 手势失效、逻辑控件恢复与异常上下文

- IInputGestureView 提供输入捕获失效契约。View 在输入资格下降、重新激活和最终释放时同步清理原生 Pointer；BindingContext 的实际绑定/退订接入同一入口，候选准备不触碰当前手势。NativeInputGesture 清除旧点击资格及对应 press/drag，不通过 ExecuteEvents 派发业务 EndDrag；原生 ScrollRect、Scrollbar 和 Legacy/TMP 选区拖动分别沿原生收尾接口处理。虚拟列表在其独占扫描边界内登记 ScrollRect，并撤销失效的在途焦点请求；自定义拖拽订阅同一通知，只取消仍处于拖动状态的捕获。
- Unity 2022.3.62f3 Play Mode 通过原生 EventSystem 事件派发观察：关门再恢复后的旧按下 clicks=0/eligible=False，新的按下 clicks=1；绑定切换 press=False/eligible=False；拖拽中关门后 drag=False/dragOwner=False/velocity=(0,0)。日志 `/private/tmp/mui-gesture-observation-20261008.log`。这项验证为临时验收工程中的原生事件路径，不能视为真实鼠标硬件或所有输入模块验收。
- VirtualListFocusPosition 新增只含节点名的 ControlPath；无 Element 的控件和条目子 View 控件可恢复同一逻辑位置，同级重名按默认规则回退。无有效条目控件时继续按页面规则回退，全无有效控件时清空选择。失焦期间的 ConstrainFocus 不再覆盖保存的逻辑焦点；选择回调取消请求后只撤回本请求仍持有的选择。
- 首轮 Play Mode 发现失焦维护覆盖旧快照，修复后复跑：captured=1501/Secondary、moved=Ready/1502/Secondary、automaticRestore=1502/Secondary；物化期间选择 Close，旧请求 Superseded 且 retained=True。显式 Shutdown 正常结束。日志 `/private/tmp/mui-focus-observation-restored-20261008.log`。临时工程动态加入原生控件，验证逻辑 API 和自动恢复；不替代多控件模板真实方向键交互验收。
- UIErrorContext 和 UIErrors.BeginContext/AttachContext/GetDiagnosticContext 接入原有弱键异常记录，字段包含宿主 Guid、导航句柄编号、路由、操作和阶段；字符串最多 256 字符，不持有页面或业务对象。传播补充缺失字段，原始阶段及诊断标识不被外层清理覆盖。导航准备、绑定、参数/模型换绑及资源归还接入阶段上下文；时间线仍按需记录，跨帧转场不遗留环境上下文。Unity 默认出口单条日志携带完整标识、上下文与异常栈。
- 关闭时间线追踪的 Unity Play Mode 验证 Provider/Binding/ArgsCommit/ViewResourceRelease 四个独立故障，共 reports=4；重复观察不重复报告，完成后 outsideEmpty=True。修正关闭路径在未开启追踪时的操作名称后复跑，归还阶段 operation=ForceCloseAsync，显式 Shutdown 结束；最终日志 `/private/tmp/mui-error-context-final-20261008.log`。未证明全部故障链均携带完整位置。
- 条目失败占位清理增加逐项来源复核；模板创建回调换源后停止写入旧节点，避免后续删除覆盖新来源责任。最新 Navigation Editor 依赖编译 0 警告、0 错误；170 个相关文件的成员布局与大括号检查通过，未声明全仓库均符合格式。本轮未新增测试文件或提交，完整设计目标仍在执行。

### 2026-10-08 原生焦点、测量缓存与列表收尾

- 物化条目的 Selectable 接入 VirtualListFocusInput：条目内部沿原生规则导航，到边界后按稳定键定位相邻项；保留 Slider/Scrollbar 调值及 Navigation.None。宿主的 EventSystem 用于捕获与提交焦点，避免误用另一个系统的当前选择。View 保存条目键与元素名，在重新获得资格时异步恢复，激活切换清空旧焦点。
- NativeFocusObserver 观察选择代际。框架回收及默认焦点回退不会把同一请求误判为用户操作；用户的新选择使旧异步聚焦失效，恢复失败的回退同样复核资格与选择代际。新增组件不按帧轮询焦点，不保留已关闭页面的导航历史。
- VirtualListItem 新增可选 contentVersion；同一来源内同键、同版本和同模板可跨条目实例保留测量，未指定版本仅随原实例保留。换源、Reset、列数和布局约束变化清空相关缓存，NotifyUpdated 显式失效，InvalidateItemSize 提供按键的局部重测并保持阅读锚点。
- 失败记录及占位节点以当前条目实例作内部索引，稳定键查询后重新检查来源，内部清理不执行项目键比较。整表清理先转移旧占位节点，避免销毁回调误删新来源的节点。普通及虚拟列表提交收尾捕获任务快照，父关闭后不会读取已清空的候选集合或重复归还已接管节点。
- Navigation 增加五槽奖励栏菜单，复用 LocalRecyclingListDemo 和生成绑定；原节点借用接口及挂点容量继续共用固定槽位契约。Basic Prefab 的 Confirm 明确接入 UIBackInput，Provider 在激活前配置宿主。共享输入在无合格页面且无模态时保留项目原生控件选择，修复重开按钮的键盘焦点被清空。
- 离线主 Editor、Navigation Editor、Basic Editor 依赖构建成功，0 警告、0 错误。150 个相关源文件的成员布局与大括号检查通过；格式在临时副本处理后以补丁应用，未声明整个仓库格式均已通过。运行时、Editor、Analyzer 与 Sample 的 700 个 meta 无非法或重复 GUID，脚本及程序集定义未缺 meta。
- Unity 2022.3.62f3 本地 UPM 工程重新导入 Navigation 后，现有 RunPreviewBatch Play Mode 演示正常退出，记录 MUI Navigation shutdown complete；10000 项工作集仍为 8，定位、模板替换、阅读锚点、三列 Grid 与模态屏障均产生日志。日志 `/private/tmp/mui-focus-navigation-preview-20261008.log`。这轮不包含随后任务快照收尾修正，也不证明原生方向、异步焦点竞争、固定槽位或完整列表故障矩阵通过。
- 最新 Basic IL2CPP 构建成功，errors=0；实际 Player 的 Enter 完成 Done(42)，再次 Enter 重新打开页面；Escape 关闭记录 Dismissed/value=0，再次 Enter 可重新打开并完成 Done(42)。原生指针的按下/抬起已被 Unity 接收，但 Unity 读取位置在窗口外且 Raycast=none，工具点击坐标原因未确认，不能归因于框架按钮逻辑。日志 `/private/tmp/mui-basic-pointer-player-20261008.log`。退出仍记录 IL2CPP Free after allocator was destroyed，原因未定位，不能报告干净退出或完整 Basic 验收。
- Measured Navigation 的 macOS IL2CPP 构建 errors=0；真实方向键已验证 1501→1502→1501，以及连续 Down 到 1513，跨视口并经过普通/featured 模板时稳定键与画面一致；动态尺寸四轮演示均 Ready，物化工作集 4–5。日志 `/private/tmp/mui-measured-focus-player-20261008.log`。该 Player 早于本轮手势、控件路径和诊断上下文修正，不代表最新包的完整 Player 验收；退出同样出现 allocator 提示。未新增测试或提交，目标仍未完成。

### 2026-10-08 候选换绑、列表失败隔离与固定槽位

- 静态 NestedViewElement 在父绑定预览时准备原节点的换绑或隐藏候选，提交时同步切换；未提交候选取消后等待原清理任务。重试允许前次已排空失败，成功保留同模型也发布新的 PendingChange，避免旧失败污染本次父提交。同步激活和关闭回调可观察到本轮任务；立即完成的子收尾不人为等待下一帧。实现拆分为 Rebind / PreparedRebind partial，Core 的 PreparedViewRebind 单独保存同步提交与异步收尾契约。
- RecyclingListElement 在旧来源和旧画面仍有效时准备全部候选；VirtualListElement 只准备候选视口，并在提交后等待旧条目排空后释放。列数、来源、选择与候选值统一校验，迟到候选不接管新来源；旧节点退役期间暂停新复用。
- 虚拟列表的单项创建、准备和绑定失败现在保留有效占位尺寸，其他条目继续准备。公开 GetItemFailure / FailedItemCount / ItemFailed / RetryItemAsync；失败按来源代际与条目内容失效，滚动不自动重试，定位到失败目标返回 Failed。可选纯视觉失败模板不接纳输入，运行时和 Editor 均检查其归属与输入门控。
- 增加 SlotListElement，借用既有 NestedViewElement 包装节点，或在空挂点创建固定数量的模板实例；共用回收列表来源通知、子视图与候选准备。容量不会扩展，超量赋值拒绝，空槽解绑并隐藏；只销毁容器创建的节点。Prefab、增量及构建前校验沿统一 ViewContractValidator 接入。
- Navigation 依赖链离线编译成功，0 警告、0 错误；主 Editor 编译成功，0 警告、0 错误。空白 UPM 工程中的 Unity 2022.3.62f3 已编译候选换绑与条目失败实现；随后固定槽位和最终改动仍需最后一次 Unity 刷新。本轮未新增测试或提交，当前变更的运行矩阵未验收。

### 2026-10-08 Sample 导入与 Basic IL2CPP

- 独立空白工程 `/private/tmp/mui-upm-acceptance-20261008` 以本地 UPM 引用安装包，使用 Unity Package Manager 的 Sample API 导入 Basic Example，再导入其余七项登记 Sample；全部成功并完成 Unity 脚本编译。日志为 `/private/tmp/mui-basic-sample-import-20261008.log` 与 `/private/tmp/mui-all-samples-import-20261008.log`。验收工程在临时目录，尚未满足设计要求的桌面新建工程位置。
- Basic Example 的 macOS IL2CPP Development Player 构建成功，报告 errors=0；Clang 链接和参数警告仍在构建日志，不能报告整个构建无警告。产物 `/private/tmp/mui-basic-il2cpp-20261008.app`，日志 `/private/tmp/mui-basic-il2cpp-build-20261008.log`。该产物先于后续虚拟列表、固定槽位改动构建，不代表最新包的完整 IL2CPP 覆盖。
- Player 正常显示标题与 Done 按钮，键盘 Return 提交后实际显示 `42: Reopen`；运行日志 `/private/tmp/mui-basic-il2cpp-player-20261008.log` 记录 `Page result: Completed, value=42`。原生指针点击与重开尚未取得通过证据，原因未确认；不能将这次运行记录为 Basic 完整验收通过。未接受软件条款，未操作无关 Unity 工程。

### 2026-10-08 静态嵌套模型替换

- `NestedViewElement` 对可换绑的活动子句柄使用其 `RebindAsync`，候选准备失败时保留旧子界面及其绑定；子命令内发起替换、清空模型或无法换绑的句柄仍使用原有关闭与重新准备路径。连续请求继续等待前一次清理并检查版本，已提交结果才更新显示模型。
- Navigation 示例依赖链离线构建通过，0 警告、0 错误。Unity Editor 新版软件条款尚未由用户接受，Basic Sample 的 Package Manager Import 和本次改动的 Play Mode 行为未验收。父页面整体换绑的静态嵌套候选仍在父提交后准备，设计目标的隔离准备契约仍待实现。

### 2026-10-08 UI 状态线程边界与空白工程导入

- `ObservableObject` 的生成属性写入和批次通知在修改前校验创建线程；自定义 setter 可调用 `RequireOwningThread()`。绑定与两个列表适配器拒绝后台自定义通知，UIHost 提供捕获 Unity 同步上下文的 `Dispatcher.InvokeAsync` 供项目服务提交状态。
- 使用 Unity 2022.3.62f3 在 `/private/tmp/mui-upm-acceptance-20261008` 创建空白项目，以本地 UPM 引用安装当前包；最新线程改动及新增 `.meta` 刷新后重新编译，Editor 批处理正常退出，`MUI.Core.dll` 与 `MUI.UGUI.dll` 已重建，`Assets` 无示例文件。Navigation 离线编译 0 警告、0 错误。此为包安装与空项目编译证据，不是示例运行、Play Mode 或 Player 验收。

### 2026-10-08 绑定换绑入口统一

- 删除 `BindingContext<TViewModel>` 绕过页面所有者的直接 `RebindAsync`，以及仅供该入口使用的回滚和代际状态。该入口先解绑再尝试恢复旧会话，无法遵守候选准备与故障关闭契约；现在由 Navigator 或 ChildViewHandle 协调换绑。静态嵌套子视图的隔离准备仍未完成。
- `MUI.Samples.Navigation.csproj`、`MUI.Editor.csproj` 和 `MUI.Samples.Dialogs.csproj` 使用 Unity 2022.3.62f3 程序集引用离线编译通过，均为 0 警告、0 错误；未进行本轮 Unity Editor 或 Player 运行验收，未新增测试文件或提交。

### 2026-09-30 候选准备、资源状态与包边界继续收敛

- 换绑新增可选隔离候选准备接口，旧绑定在短提交中同步停止订阅；导航队列不再跨候选准备、旧命令清理和子视图等待持有。参数更新同样在准备时释放队列、提交前复核资格，且与换绑从受理时互斥；页面关闭取消参数操作时也会取消其提交队列等待。子视图准备仍无法在当前 View 写入前完成隔离，失败仍会故障关闭。
- 四种资源键控件公开请求键、显示键、状态、失败和冻结信息的快照；旧请求迟到不改写最新状态，失败保留原显示。虚拟列表测得无效动态尺寸时缓存有效估算值并报告条目错误，避免整表停用和逐帧重测；独立 EventSystem 的返回事件保留实际来源供同帧去重。上述行为尚无本轮 Unity Play Mode 证据。
- 翻译实现从 Runtime 移入 Resource Integration 示例，包清单的旧同步示例描述已校正。`MUI.Navigation.csproj`、`MUI.Samples.ResourceIntegration.csproj`、`MUI.Samples.Navigation.csproj` 和 `MUI.Editor.csproj` Release 编译均为 0 警告、0 错误；后面三项使用 Unity 2022.3.62f3 的程序集引用，覆盖生成器、Core、Resources、Navigation、ChildViews、uGUI、主 Editor 及相关示例。此结果不代替新建本地 UPM 项目、Editor 交互或 Player 验收。格式检查仍报告 39 个既有文件的成员布局问题，本轮涉及文件不在该名单中；未新增测试文件，未提交。

### 2026-09-30 换绑候选创建提前

- 将绑定工厂与候选身份校验移到输入封锁、旧绑定解绑和模型切换之前。仅校验通过后接管候选；提交前取消或失败时清理本次候选，不解绑工厂误返回的其他实例上下文。提交时转移候选给页面，后续失败由故障关闭清理。
- Navigation 示例及依赖编译 0 警告、0 错误。Unity 2022.3.62f3 注入候选工厂异常，结果为 `status=Failed, faulted=False, state=Open, oldBinding=Bound, factories=2, oldUpdates=True`，证明原页面和订阅保留，原模型还能更新界面。
- 上一轮绑定工厂异常归入提交失败的历史行为已被本轮替代；真正进入模型/绑定提交后的异常仍应故障关闭。完整 Binding 构建及子视图异步准备尚未隔离，解绑等待和导航队列仍需拆分，不能据此宣称换绑事务全部完成。

### 2026-09-30 换绑提交失败故障关闭

- 移除开始解绑旧会话之后的模型恢复和旧绑定重建。提交失败先调用宿主故障关闭，再解除临时输入门控；保留当前半绑定候选供统一关闭清理。RebindOutcome.RecoveryFailed 改为 ViewFaulted，并更新导航、子视图、诊断和示例引用。
- 示例先演示成功换绑，再单独触发提交故障并等待实际清理，不再在已故障关闭的句柄上继续换绑。Navigation 示例及依赖编译 0 警告、0 错误。
- Unity 2022.3.62f3 注入候选绑定工厂异常，实测 `status=Failed, faulted=True, state=Failed, oldBinding=Unbound, factories=2`，旧绑定未重新激活，关闭清理结束。
- 仍未完成隔离准备：当前候选工厂仍在旧绑定退役后执行，其失败尚不能按准备失败保留旧界面。导航队列也仍覆盖整个换绑；必须继续实现候选准备与短提交，不能将本次局部修复视为换绑符合全部目标。

### 2026-09-30 同步操作自等待保护

- 修复 LifetimeScope.Run 未登记操作调用链的问题：同步委托现在与 RunAsync 使用相同的 OperationFrame，自身或嵌套回调请求 DisposeAsync 会在取消和清理开始前被拒绝；退出时恢复父调用链，异常也不遗留活动标记。
- DragDrop 示例及依赖编译 0 警告、0 错误。Unity 2022.3.62f3 验证嵌套 Run 请求外层 Scope 销毁立即失败，拒绝后 Scope 未结束，业务异常正常传播，退出回调后释放成功且清理只执行一次。
- 重新核对换绑实现：仍先解绑旧会话、修改模型后尝试恢复，且导航队列持有到整个换绑完成；这些与隔离准备及短提交要求不符，仍为后续实质性修复项。整体目标未完成。

### 2026-09-30 LifetimeScope 模式与释放入口统一

- 删除 LifetimeMode、LifetimeScope 的模式构造参数、Mode、同步 Dispose 及同步释放实现。Own / OnDispose / OnDisposeAsync 共用逆序清理记录；同步操作仍在当前线程执行，并纳入 DisposeAsync 的等待范围。
- 删除子视图、命令绑定、资源元素和 Presenter 生命周期的模式检查。上下文菜单返回登记使用统一 Scope，退出时启动并观察 DisposeAsync；登记撤销仍由立即取消及已有清理顺序保证。
- Navigation 示例 Editor、DragDrop 示例及依赖编译均 0 警告、0 错误。Unity 2022.3.62f3 确认 Scope 不实现 IDisposable，实测取消后等待在途操作、资源释放顺序 2→1、重复释放共享任务、失败释放只执行一次，全部通过。
- 本次未执行上下文菜单原生交互及全部可选模块回归；仍需检查操作自等待、换绑事务、完整 UI 功能与验收矩阵。整体设计目标未完成，未新增测试文件或提交。

### 2026-09-30 DragDrop 提交与释放统一

- 删除 DropTarget 的同步工厂、模式属性及同步委托分支；DragSession 与会话管理器只实现 IAsyncDisposable，删除同步 Drop / Dispose 和模式匹配。uGUI 拖放绑定统一派发 DropAsync，源/目标操作仍登记到各自生命周期，保留重入与取消检查。
- 示例改为默认立即完成、可选延迟提交，共用统一协议；文档同步更新。DragDrop 示例及依赖编译 0 警告、0 错误。
- Unity 2022.3.62f3 确认旧 Drop 入口不存在；真实核心会话验证第一次及重复提交均 Committed，业务提交和视觉收尾各 1 次；源生命周期释放使未提交会话 Cancelled，视觉收尾 1 次。
- 本轮未覆盖延迟提交期间取消、失败矩阵或原生指针拖动。LifetimeScope 本身的同步模式仍未删除，整体目标继续执行。未新增测试文件或提交。

### 2026-09-30 自动化会话模式移除

- UIAutomation 构造只接受 View，删除 LifetimeMode 参数与 RequiresAsync 派发状态；View 的自动化检查改为就绪检查。输入资格、控件归属和重入检查仍沿用原流程，示例文档已更新。
- Automation 模块及依赖编译 0 警告、0 错误。Unity 2022.3.62f3 实际调用 InvokeCommand("Confirm")，经生成绑定执行命令、请求关闭并等待清理，后续缓存复用与延迟归还检查全部通过。未初始化 View 返回 NotReady。
- 这是按钮命令派发验证，不代替原生指针或键盘输入验收。LifetimeScope 和 DragDrop 的同步模式仍待处理，整体目标继续执行。未新增测试文件或提交。

### 2026-09-30 导航生命周期模式移除

- 删除 INavigator / Navigator、内部 ViewInstance / ViewContent 和 NavigationSnapshot 的模式属性与模式构造参数。导航直接创建默认 LifetimeScope，预加载、请求计数、清理、转场及守卫不再按同步模式分流；同步模式检查辅助文件改为 Navigator.Failures。
- 更新 UIHost Inspector 与依赖示例的快照展示。Navigation 示例 Editor 和主 Editor 编译通过；Unity 2022.3.62f3 重导入后反射确认 Navigator 与 NavigationSnapshot 均不存在 Mode，编译结束。
- 本次验证编译及加载后的公开契约，未重新执行运行矩阵。LifetimeScope 本身和 DragDrop、自动化等可选模块仍有同步模式使用，整体目标未完成。

### 2026-09-30 绑定会话解绑统一

- BindingContext 仅实现 IAsyncDisposable，删除同步 Unbind / Dispose、CanUnbindSynchronously、LifetimeMode 和 SetLifetimeMode；删除 BindingContext.Synchronous。BindingSession 使用统一 LifetimeScope，解绑先发布共享任务，再退订并等待命令收尾，不再分流到同步命令释放。
- Navigation 示例及依赖编译 0 警告、0 错误。Unity 2022.3.62f3 强制刷新并确认新 BindingContext 不实现 IDisposable；真实生成绑定验证 `initial=True, live=True, detached=True, state=Unbound, cleanup=True`，覆盖初值、模型变化、解绑后退订和重复 DisposeAsync。
- 本次未覆盖慢命令取消与绑定构建重入矩阵。LifetimeScope 本身及可选模块的同步模式仍待清理，整体目标未完成。未新增测试文件或提交。

### 2026-09-30 Core 清理与模型所有权统一

- 删除无调用方的 ViewPresenterLifecycle.Synchronous、ViewActivationCleanup.Synchronous、ViewInstanceCleanup.Synchronous 和 ViewModelOwnership.Synchronous。模型所有权只实现 IAsyncDisposable，先发布共享清理任务再调用业务释放；移除同步兼容状态和重复结果存储。业务模型仅实现 IDisposable 时仍由统一异步流程直接调用其 Dispose。
- Navigation 示例及依赖编译 0 警告、0 错误。Unity 2022.3.62f3 对真实 ViewModelOwnership 和示例异步模型验证：未完成释放的重复等待共享同一任务，释放计数为 1，借用模型释放计数为 0，所有权对象不再实现 IDisposable。
- 本轮只验证模型所有权释放契约，未完成全部 Core 生命周期迁移；LifetimeScope、绑定会话及可选模块中的同步模式仍需处理。未新增测试文件或提交。

### 2026-09-30 路由同步配置移除

- 删除 PreparationMode、Route.Preparation、SupportsSynchronousLifecycle 及相应构造参数和 ViewRouteAttribute 属性。移除内容创建阶段针对同步路由的限制，立即完成与异步完成使用相同路由。
- 生成器路由工厂不再生成准备模式参数和同步能力标记；更新随包交付的 Release 生成器 DLL。页面向导移除同步生命周期选项，路由图移除准备模式与同步能力展示，相关示例已迁移。
- Release 生成器、Navigation 示例 Editor 及主 Editor 编译均 0 警告、0 错误。Unity 2022.3.62f3 重导入 Navigation 后确认旧枚举与属性不存在，实际生成工厂参数只包含 modelFactory、presenterFactory、policy、argsEqual、dependencies、key、resource、estimatedRetainedBytes，编译已结束。
- 本次验证生成代码和 Editor 编译，未执行向导生成页面或完整运行矩阵。Core 层 LifetimeMode 和同步生命周期契约仍待处理，目标未完成。

### 2026-09-30 内部关闭与依赖释放收敛

- 删除 Navigator.Close.Synchronous、ViewInstance.Synchronous、ViewContent.Synchronous；移除同步依赖释放、同步父链收敛和已无调用方的同步依赖回滚检查。所有实例和内容释放都进入已有异步清理实现。
- 移除同步关闭完成存储及其延迟 Task 适配，Closing 与 CleanupCompletion 各自只读取统一任务；删除仅用于同步释放准入的回调深度和能力检查。
- Navigation 示例及依赖编译 0 警告、0 错误。Unity 2022.3.62f3 再次运行 CloseAsync / CompleteAsync / BackAsync 与延迟归还流程，缓存复用、等待释放、额度归还、失效禁止重新缓存均为 True，随后退出播放。
- 共享依赖关闭矩阵尚未运行验证。LifetimeMode、路由同步标记及 Core 层同步生命周期契约仍需继续收敛；本轮未新增测试文件或提交。

### 2026-09-30 关闭公开入口统一

- 删除同步 Close / ForceClose / Complete / Back / CloseLayer / CloseAll / ReleaseExplicitOwnership 及对应接口声明和追踪包装；批量关闭同步实现删除。立即返回的关闭守卫仍由统一 CloseAsync 评估，示例移除旧同步关闭准入演示。
- 命令请求关闭、业务结果提交、故障关闭及依赖强制收敛入口不再根据同步模式分流；删除同步延后关闭队列，诊断只读取统一关闭请求状态。
- Navigation 示例及依赖编译 0 警告、0 错误。Unity 2022.3.62f3 验证 CloseAsync、CompleteAsync、BackAsync 实际关闭流程，并取得缓存复用、等待释放、额度归还及失效禁止缓存均为 True 的结果；反射确认七个旧公开入口不存在，验收对象已释放。
- 本次没有验证完整批量关闭、共享依赖与取消矩阵。内部同步关闭实现、同步依赖释放与生命周期模式仍有残留，目标继续执行，不能视为全部完成。未新增测试文件或提交。

### 2026-09-30 缓存清理统一

- 删除导航同步 `ClearCache` / `InvalidateCache` / `ClearInactiveContent` 入口、同步缓存释放循环与独立清理状态；移除不再使用的同步缓存探测和 `ViewContent.ReleaseCached` 契约。主动清理与淘汰均通过 `ReleaseCachedAsync` 归还资源，等待期间继续占用在途数量与估算额度。
- Tabs 示例及依赖编译 0 警告、0 错误。Unity 2022.3.62f3 实际验证返回 `cached=True, reused=True, pending=True, released=True, invalidated=True`：关闭后缓存、重新打开复用且生成新句柄、重复清理均等待同一未完成释放、完成后数量与额度归零、失效前活动页关闭后不重新进入缓存。使用延迟释放凭证验证，未新增测试文件。
- Unity 反射确认三个同步缓存入口不存在，验收对象已销毁，编辑器已退出播放。同步关闭及内部生命周期实现仍待清理；本次证据不覆盖缓存释放异常或完整性能矩阵。

### 2026-09-30 导航获取与退出统一

- 删除 `Navigator.CreateSynchronous`、同步 `Open` / `Replace`、同步候选及依赖准备实现；`PostOpen` 仅派发统一异步事务。导航构造只接受 `IViewProvider`，缓存版本也从同一提供方取得。
- 删除 `ISynchronousViewProvider`、`SyncCreateAvailability`、`ISynchronousViewLease` 和内部同步凭证接管路径。删除同步 `Shutdown` 与 `IDisposable`，宿主退出和 `ShutdownAsync` 共用已有异步清理任务。
- Navigation 示例及依赖编译 0 警告、0 错误。Unity 2022.3.62f3 反射确认上述同步公开入口不存在；实际打开及参数失败流程记录 `preparation=PreparationFailed, retained=True, commit=CommitFailed, faulted=True, noRollback=True, state=Failed`，退出后验收对象已销毁。控制台连接未返回普通日志，结果由 Editor 日志核对。
- 尚未完成：同步关闭/缓存入口、内部同步释放实现、LifetimeMode 与路由同步能力标记；本次不宣称整体导航协议清理完成。未新增测试或提交。

### 2026-09-30 导航结果契约收敛

- 删除没有产生路径的 `RequiresAsync`、`RequiresPreload`、`SyncCreationUnsupported` 拒绝值及其转换分支；删除实例上的同步能力探测及仅供该探测使用的状态字段。
- 替换与超限打开结果只通过 `SourceCleanup` / `ReplacedCleanup` 提供旧页面清理任务，移除失效的同步结果字段与兼容任务适配。诊断仅采集已完成的清理结果，不额外等待；本地示例显式等待清理并输出结果。
- Navigation Editor 及其依赖使用 Unity 2022.3.62f3 引用编译通过，0 警告、0 错误；源码和示例文档扫描未发现已删除结果成员引用。未新增测试、未提交；本轮未做 Unity 场景验收。

### 2026-09-30 资源失败清理协议统一

- 删除 `SynchronousResourceLoadException` 及导航、预加载、资源槽、作用域扩展和资源示例中的专用处理分支。后端统一通过 `ResourceLoadException.CleanupCompletion` 报告回滚结果；立即失败使用已失败的 Task，延迟回滚提供实际清理任务。
- 可选依赖降级检查拒绝忽略尚未完成或未成功回滚的资源加载异常，继续保留清理失败记录；本次没有将回滚失败转换为普通资源缺席。
- Navigation Editor 及其资源、uGUI、导航和资源接入示例依赖使用 Unity 2022.3.62f3 引用编译通过，0 警告、0 错误。源码扫描无旧异常类型引用；未新增测试，未执行 Unity 场景验收，未提交。

### 2026-09-30 资源获取目录及生命周期命名核对

- 补齐预加载命名：`PreloadLease` / `IPreloadLease` 更名为 `AcquiredPreload` / `IAcquiredPreload`，保留脚本 `.meta` GUID；导航、Provider、作用域扩展和示例引用同步迁移。内部持有变量、方法及示例资源计数文案不再使用 Lease 命名。
- 本轮源码及文件名扫描确认 Runtime、Editor、Samples 和生成器源码没有 Lease 残留；Navigation Editor 与 Tabs 示例使用 Unity 2022.3.62f3 引用编译通过，0 警告、0 错误。本轮未执行 Unity 场景验收。

- `Runtime/Resources/Leases` 更名为 `Acquisition`，`LeaseReleaseContext` 更名为 `ResourceReleaseContext`；文件与目录的 `.meta` 随迁移保留。
- 删除没有调用方的 `SynchronousViewLease` 及其专用 `SynchronousResourceLease<T>`、`ISynchronousResourceLease<T>`，不再为这些旧实现保留兼容别名。当时仍存在的 `ISynchronousViewLease` / `ISynchronousViewProvider` 已在后续导航获取收敛中移除，其他同步分支仍未清理完成。
- 连同 `LifetimeScope` / `Scope` 命名迁移，Navigation、Tabs、Dialogs、DragDrop 示例及 DragDrop Editor 使用 Unity 2022.3.62f3 引用编译，全部 0 警告、0 错误。一次共享编译进程曾发生 Roslyn MissingMethodException，禁用共享编译后通过。
- Unity 2022.3.62f3 验收工程刷新后确认资源脚本位于新目录、两个已删除具体类型不再存在、ResourceReleaseContext 已加载且编译已结束。本次未新增测试或执行运行场景验收；控制台仍含先前故意注入的参数异常和 MCP 连接错误。

- 当前分支以 `main` 中的 Runtime、Editor、生成器和示例作为迁移基础；恢复时保留了工作区已有文件。
- UIHost 已移除独立同步入口；核心导航已移除 `ISynchronousViewProvider`、同步创建宿主及同步打开/替换/退出入口；同步关闭、缓存和内部生命周期分支仍待清理，尚未满足完全移除第二套 API 的目标。
- 未自动提交，未新增测试。

## 已修改，待运行验收

- 首次就绪不再等待进入动画；Open 依据已发布的首次就绪结果返回，后续关闭不改写成功结果。
- 就绪等待不再提前结束进入动画；进入期间继续禁止输入与业务 Tick。
- 默认 Tick 策略改为完全隐藏时暂停，InputGate 与焦点不参与 Tick 资格判断。
- BringToFront 不修改历史；Replace 对参与历史的新页保留旧记录位置。
- Back 使用实际显示顺序，返回行为与历史登记解耦；进入及退出中的模态屏障消费返回输入。
- Close、ForceClose、Complete、Back 和批量关闭通过独立提交信号返回；内部回滚及 Shutdown 仍等待真实清理。页面业务结果在关闭提交固定，清理或转场失败不覆盖原结果；Back 输入恢复仍由视觉退出驱动。转场示例显式等待清理。
- 类型化句柄提供独立且可重复观察的进入/退出等待，区分完成、打断与失败；取消等待不修改动画。视觉退出收尾避免重复执行；转场示例通过异步打开及独立阶段等待演示。
- 重复打开默认拒绝；`ExistingInstancePolicy.ReturnReady` 显式返回兼容的就绪实例，不置顶或重排历史。关闭提交释放 Route 额度，未结束的视觉退出和清理继续占用独立清理容量；依赖和 Replace 使用相同的额度口径。
- Open/Replace 登记候选后释放队列许可，异步准备和确认不占执行权；重新取得许可后校验并提交，失败候选的等待清理也在队列外执行。其他操作的队列与直接自等待规则仍需逐项核对。
- `IViewProvider.AcquireAsync` 和可选 `IPreloadViewProvider.PreloadAsync` 使用 Task；异步后端不再被要求实现同步创建或能力查询。提供本地/异步委托适配器，Prefab 提供方支持统一预加载；导航、子视图、挂载装饰器和相关示例已迁移获取入口。异步宿主的旧同步 Open/Prepare 不再启动资源获取，应使用异步入口；独立同步宿主与其示例尚待删除或迁移。
- Basic Example 改为无 Presenter 的 ViewModel/生成绑定/Route，使用异步宿主、打开和显式退出清理。业务结果发布后等待视觉退出才恢复场景重开按钮，README 区分手写文件与生成产物。
- 生成器对 void、Task、ValueTask 命令统一生成 AsyncCommand；立即完成的业务委托不再生成第二套同步命令。AsyncCommand 提供同步委托接入，继续使用同一来源、取消和并发规则。旧同步宿主示例尚未迁移，不能据其通过编译推断仍能运行。
- 清理虚拟列表分页配置残留、Editor 校验和失效示例说明；去掉移除调用后重复的代际检查，保留事件重入保护。
- 设计补充参数更新失败、共享依赖与预加载身份、合并请求取消、位置快照兼容性和 Canvas 坐标约定；这些约定仍需对照实现核验，不作为已经实现的证据。

- 虚拟列表新增 Viewport 几何快照及按帧合并的属性通知：可见范围不含预留行，提供距首尾距离及滚动状态，弹性越界只统计实际相交内容。此项目前覆盖纵向列表与 Grid，运行表现待验收。

- 横向单列列表接入现有复用、定位、阅读锚点、视口通知及布局测量流程，纵向 Grid 保持原路径。配置明确选择滚动轴，横向多列与无效轴拒绝启用。条目公开尺寸改为 Extent，测量 API 使用 Size 命名；旧 Prefab 字段通过 FormerlySerializedAs 迁移，示例源码已更新。横向实际交互尚待验收。

- 虚拟列表新增按索引的异步定位入口及 Nearest/Start/Center/End 对齐，沿统一滚动轴计算并钳制首尾位置；测量后按可实现位置判断完成。移除焦点定位的无条件 Task.Yield。增量更新通过稳定键重新解析目标，删除返回 NotFound，换源与重置终止旧请求；自定义键比较重入改写数据时拒绝不一致索引。平滑滚动与原生拖动/滚轮中断已接入，完整焦点恢复与运行验收仍未完成。

- 定位支持 duration 参数，以非缩放时间平滑滚动；使用独立请求取消源结束旧定位等待，不取消列表自身的条目清理。原生输入观察组件在拖动、滚轮及停用时中断定位；新定位与来源重置替代旧请求。显式定位期间暂停阅读锚点补偿，定位结束后恢复。MaxRevealCorrections 独立限制动画后的布局修正次数，默认 64；运行期与 Editor 均校验正值。

- 阅读锚点删除后按原顺序查找仍存活的后继、其次前驱；换源与重置回到起点并停止原惯性。增加可选 FollowEnd 与 EndFollowTolerance，更新前已靠近末尾且无拖动或显式定位时跟随，覆盖追加和动态尺寸修正。阅读补偿保留速度并重设原生拖动及上一帧位置基准，尚待 Unity 实际交互验收。

## 仍需实现或逐项核对

1. API：统一异步 Provider、导航、子视图与生命周期，移除第二套同步分支；同步委托走立即完成路径，更新生成器和全部示例。
2. 导航：运行验证关闭提交、转场与最终清理分离；核对短提交队列、实例预留、重复关闭决议、取消、守卫与不可变结果。
3. 资源：缓存仅保留解绑后的 View 和可移交凭证；核对预加载合并、依赖持有、加载器寿命、资源槽切换与失败责任记录。
4. 数据绑定：核对双向校验、嵌套模型、换绑准备隔离、主线程检查、命令代际与扩展控件。
5. 容器与列表：核对父子生命周期、固定槽位及设计列出的全部虚拟列表能力，包括动态尺寸、定位、锚点、焦点与有界物化。已移除 Core/Collections 的分页、分组、树形模型、VirtualListElement 的分页驱动及对应导航示例；保留扁平条目与模板选择。范围及边界通知已接入现有纵向布局，横向已接入共用滚动轴逻辑；完整定位、锚点恢复及其他剩余核心能力仍需实现和验收。
6. uGUI：核对跨宿主输入、完整手势取消、模态屏障、布局、安全区、外部销毁与可选适配。
7. Editor 与交付：校验规则统一、轻量向导、公共控件契约、UPM 文档与清单、样例依赖及生成器发布。
8. 验收：桌面全新 Unity 项目安装包、逐项导入示例、真实交互、异常清理、至少一个 IL2CPP Player 与性能基线。尚无本轮运行验收证据。

## 已完成的验证

- `dotnet build Tools~/Build/MUI.Navigation.csproj --no-restore --nologo -m:1 -p:NuGetAudit=false`：上述导航修改后编译成功，0 警告、0 错误；包含 Core、Resources 和 Navigation，不覆盖 Unity 渲染、Editor、生成器或 Player。
- Provider 获取与预加载迁移后，`MUI.Samples.Navigation.csproj` 使用本机 Unity 2022.3.62f3 的 Managed 引用及 `ExampleProject~/Library/ScriptAssemblies/UnityEngine.UI.dll` 离线编译成功，0 警告、0 错误；覆盖 Core、Resources、Navigation、ChildViews、uGUI、Localization、ResourceIntegration、生成器和导航示例。该引用工程只用于开发期编译，不替代桌面空白项目验收。
- 使用相同引用构建 `MUI.Samples.Tabs.csproj` 成功，0 警告、0 错误，补充覆盖 Tabs、uGUI Tabs 与版本化 Provider 示例。
- 无 Presenter 的 Basic Example 和 `MUI.BasicExample.Editor.csproj` 编译成功，0 警告、0 错误；生成产物确认 Route 使用 Unit 参数且不自动创建 Presenter，Confirm 命令生成为 AsyncCommand。该项尚未证明实际点击、结果和退出场景行为。
- 命令生成迁移后 Release 生成器编译成功，已更新 `Analyzers/MUI.Generators.dll`，并核对其 SHA256 与 Release 产物一致；现有 RoslynAnalyzer 导入标签保持有效。

- 移除分页、分组与树形模型及导航演示依赖后，使用上述 Unity 引用重新构建 `MUI.Samples.Navigation.csproj` 成功，0 警告、0 错误。首次检查发现激活清理中的分页退订残留，修正后重建通过；本次仍为离线编译。
- 同一修改后 `MUI.Editor.csproj` 离线编译成功，0 警告、0 错误，覆盖虚拟列表配置校验；不代表 Prefab 导入或构建回调已在 Unity 执行。
- 新增 Viewport 查询与通知后，使用上述 Unity 引用构建 `MUI.UGUI.csproj` 成功，0 警告、0 错误；包含停用或错误状态发布空快照的路径。几何通知、滚动停止及弹性边界仍需实际 Unity 验收。
- 横向滚动轴接入与尺寸 API 更新后，`MUI.Samples.Navigation.csproj` 使用上述 Unity 引用编译成功，0 警告、0 错误。此结果不证明布局测量、焦点或拖动行为已运行通过。
- 同一修改后的 `MUI.Editor.csproj` 离线编译成功，0 警告、0 错误；新增滚动轴及横向单列配置校验。Prefab 序列化迁移和实际编辑器诊断仍需 Unity 验收。
- 索引定位、对齐与增量目标重新解析修改后，`MUI.Samples.Navigation.csproj` 使用上述 Unity 引用编译成功，0 警告、0 错误；未新增测试。对齐后的真实画面、异步更新竞争和焦点仍需 Unity 运行验证。
- 平滑滚动与输入中断接入后，`MUI.Samples.Navigation.csproj` 使用上述 Unity 引用编译成功，0 警告、0 错误。timeScale 为零、真实拖动/滚轮、帧率变化及旧请求取消仍待 Unity 运行验收。
- 增加独立修正预算、输入门控后的拖动状态清理后，`MUI.Editor.csproj`（含更新后的 uGUI）离线编译成功，0 警告、0 错误；仍不替代实际输入及 Prefab 导入验收。
- 阅读锚点及末尾跟随接入后，`MUI.Samples.Navigation.csproj` 使用上述 Unity 引用离线编译成功，0 警告、0 错误。
- 补偿时同步 ScrollRect 的 PostLayout 历史位置、重置时停止惯性后，`MUI.Editor.csproj`（含更新后的 uGUI）离线编译成功，0 警告、0 错误；新增末尾跟随容差校验。拖动补偿与速度行为依据本地 uGUI 源码核对，尚非运行验证。

- 新建桌面 `MUI-Design-Acceptance` 工程，版本文件及实际 Editor 日志均为 Unity 2022.3.62f3。通过 `UnityEditor.PackageManager.Client.Add` 安装本仓库本地包，随后实际 Editor 批处理编译退出码为 0，生成 MUI 程序集。记录：`/private/tmp/mui-package-install.log`、`/private/tmp/mui-package-compile.log`。此项是 UPM API 安装与 Editor 编译证据，不是 Add package from disk 点击、Samples、PlayMode、输入或 Player 验收；也不覆盖后续修改。
- 位置捕获/恢复接入：来源集合使用弱键表对应的不透明身份，快照不保留集合；按键与条目内偏移恢复，默认拒绝跨来源，显式兼容恢复及删除锚点回退均有结果。恢复复用异步定位的物化、布局修正和取消流程。`MUI.UGUI.csproj` 使用 Unity 2022.3.62f3 和新验收工程 uGUI 引用离线编译成功，0 警告、0 错误；真实布局、切换来源和输入竞争仍待 Unity 验收。

- 视口尺寸变化接入独立观察组件：尺寸回调只记录原锚点及旧末尾状态，滚动刷新或 LateUpdate 再补偿；连续尺寸通知合并，来源或定位代际变化使旧记录失效。组件随列表拥有期解除并销毁。依据验收工程中 uGUI ScrollRect 的尺寸回调、LateUpdate 和边界钳制顺序核对；`MUI.UGUI.csproj` 使用 Unity 2022.3.62f3 引用编译成功，0 警告、0 错误。窗口缩放、CanvasScaler、拖动和末尾跟随仍未取得实际运行证据。

- 横向/纵向共用的布局查询与树状数组索引统一采用 Extent 命名，清除 ContentHeight、RowHeightForIndex、TotalHeight 等纵向专用概念；测量局部变量区分滚动轴尺寸与交叉轴尺寸，保留原生 Unity 宽高 API 和 FormerlySerializedAs 旧字段映射。`MUI.Editor.csproj` 及依赖使用 Unity 2022.3.62f3 引用编译成功，0 警告、0 错误。现有 Grid 仍含按行取最大尺寸的兼容布局，与设计要求的统一单元格尺寸尚不一致，不能算该目标已完成。

- 已修复上述多列 Grid 尺寸差异：几何查询、Content 范围、定位和物化单元均使用配置的统一滚动轴尺寸，不再按每行最高项排布；逐项显式尺寸与布局测量仅用于单列。列数切换清除旧测量缓存，回到单列后重建逐项尺寸索引。同步与异步刷新共用该布局规则，示例及说明已更新。`MUI.Samples.Navigation.csproj` 使用 Unity 2022.3.62f3 引用编译成功，0 警告、0 错误。自动列数、间距/内边距及实际 Grid 画面仍待实现或验收。

- 自动列数接入：初始化前 ConfigureAutomaticColumns 指定最小列宽，按 Content 可用宽度计算至少一列，均分宽度，超过池容量明确失败；自动模式的一列仍为统一尺寸 Grid。尺寸变化按帧重新计算，通过现有列数切换保留阅读锚点并发布 Columns 通知。Editor 校验滚动轴和最小列宽。`MUI.Editor.csproj` 及依赖使用 Unity 2022.3.62f3 引用编译成功，0 警告、0 错误。自动列数缩放、边界容量和恢复行为仍需运行验收；间距及内边距尚未实现。

- 间距与内边距接入：ConfigureSpacing 配置物理 X/Y 间距及四边内边距，复制 RectOffset 隔离调用方修改；布局工厂统一供内容尺寸、物化、可见范围、定位及自动列数使用。动态尺寸索引在查找时计入间距而不重建全量前缀，视口仅处于空隙时不计入前一条；快照保留负锚点偏移。增加几何溢出候选校验、交叉轴变化刷新和非负单元尺寸，Editor 校验配置。MUI.Editor.csproj 及最终 MUI.UGUI.csproj 使用 Unity 2022.3.62f3 引用编译成功，0 警告、0 错误。未新增测试；横向/纵向边缘、Grid 间距、测量与实际缩放仍待运行验收。

- 桌面独立工程 `MUI-Design-Acceptance` 已通过 `UnityEditor.PackageManager.UI.Sample` API 导入 Basic Example；日志 `/private/tmp/mui-basic-import.log` 记录 Unity 2022.3.62f3、导入目录和正常退出。移除验收安装器的自动启动入口，后续打开工程不会重复安装包或自动退出批处理。
- 随后用同一版本真实 Editor 编译导入后的示例，生成 MUI.BasicExample.dll 与 MUI.BasicExample.Editor.dll，打开 `Assets/Samples/MUI/0.1.0/Basic Example/Basic.unity` 并执行现有 UIBuildValidation.Validate。报告“目录 1，页面 1，路由 1”，IsValid 为 true，进程退出码 0；日志 `/private/tmp/mui-basic-validation.log`。这证明当前包与 Basic 示例的 Editor 编译、场景打开及目录/Prefab/绑定声明校验通过，不证明 PlayMode、真实 Done 点击、重开、视觉退出或 Player 已验收。

- Basic 示例首次在独立 macOS IL2CPP Player（Unity 2022.3.62f3，Development，High 托管裁剪，arm64/x64 通用产物）运行，真实键盘提交 Done 返回 42 时发现清理异常：BindingContext.EndSessionAsync 继承命令的 Lifetime 操作标记，误判为等待自身销毁。已在导航关闭清理启动处隔离操作调用链；原操作仍登记并被清理等待，Lifetime 对业务直接自等待的保护保留。
- 修复后 MUI.Navigation.csproj 编译成功，0 警告、0 错误；IL2CPP 重新构建成功，BuildReport 为 0 错误、0 警告（`/private/tmp/mui-basic-il2cpp-fixed.log`）。通过 CUA 实际键盘 Down/Return 完成两次 Done，并在中间用 Return 重开；画面观察到 Basic 页面和 `42: Reopen`，运行日志含两次 `Page result: Completed, value=42`，退出后未发现 Exception 行。Player 路径为桌面验收工程的 `Builds/Basic-IL2CPP.app`，日志位于 `~/Library/Logs/DefaultCompany/MUI-Design-Acceptance/Player.log`。
- 本项覆盖 Basic 的 IL2CPP 编译、裁剪后启动、键盘提交、结果与重开回归。全屏坐标点击被 CUA 的 noWindowsAvailable 阻断，鼠标路径未通过；尚不覆盖 100 次循环、资源计数、虚拟列表、其他示例或全部 IL2CPP 泛型组合。首次失败构建/运行不能计为通过，以上通过结论仅对应修复后重建版本。

- 独立验收工程已通过 UPM API 安装 MCP for Unity 9.6.6，固定 Git 提交 `73eb27aeccfa8e0676eaf3304136e9b85953d913`。仅验收工程依赖此工具，MUI 框架包不增加 MCP 依赖。编辑器使用绝对路径启动并由 MCP 再次确认 Unity 2022.3.62f3、非 batch 模式；会话锁定 `MUI-Design-Acceptance@91dc4e67`，与同时运行的 fun-slg 隔离。
- MCP 已加载 Basic 场景并进入 Play Mode，通过按钮事件完成关闭，读取到 `42: Reopen`，调用重开后确认 View 存在、Confirm 按钮存在且输入门控开启。此项是编辑器运行与事件链路验证，不等同于鼠标命中或视觉验收。当前编辑器内置 Shader（包括 UI/Default）报告无法包含 HLSLSupport.cginc，虽然对应文件实际存在，仍待定位；不得据此宣称编辑器渲染验收通过。启动日志为 `/private/tmp/mui-editor-mcp.log`。

- 导入 Resource Integration 与 Navigation 后，真实 Editor 编译成功。`Assets/NavigationAcceptance.unity` 使用现有测量演示，2000 条记录定位到键 1500；初始、视口变窄、字号改变、单项内容更新四步均记录 Ready，工作集 4–5 个条目。后台验收需开启 Application.runInBackground，避免失去窗口焦点时帧推进暂停。完整导航演示曾在模态屏障射线就绪检查处失败，尚未定位其与 Shader 报错的关系；不能将此轮视为完整导航验收通过。
- 实际复现并修复虚拟列表焦点随复用对象转移：修复前从焦点键 1500 滚到键 0，EventSystem 仍选中同一复用控件，文字已变为 Message 0。修复后回收会清除条目和祖先 View 的历史选择及原生选择；换源和 Reset 以来源代际强制结束旧绑定。选择回调中的异步刷新先发布本轮 PendingChange，再等待布局帧，避免选择重入及返回旧完成信号。
- Unity 2022.3.62f3 Play Mode 回归：滚动回收、相同键/模型换源、相同来源 Reset 后均回退至页面 Confirm；Select 回调内换源时 PendingChange.IsCompleted 为 false，随后 Ready / RanToCompletion，无 Console error。离线 MUI.UGUI 编译为 0 警告、0 错误，后续来源代际与等待调整由真实 Editor 编译并运行验证。未新增测试文件。此修复只证明解除旧原生焦点与回退，稳定键逻辑焦点恢复及方向导航仍待实现。

- Shader 恢复：关闭验收 Editor，将 Library/ShaderCache 与 ShaderCache.db 备份到 `/private/tmp/mui-shader-cache-backup-f3p3b36c`，通过指定的 2022.3.62f3 Unity.app 重新打开。新日志 `/private/tmp/mui-editor-shader-recovery.log` 无 Shader error / HLSLSupport include 错误；Editor 查询 Hidden/Internal-GUIRoundedRect、UI/Default、Hidden/BlitCopy、Hidden/Internal-GUITextureBlit 均 supported=True、messages=0，进入 Play Mode 后 UI/Default 仍为 0 消息。该结果证明缓存重建并重启后的编译恢复，未单独区分缓存与启动方式因素。窗口截图读取因自动审批服务额度耗尽未执行，尚缺整窗视觉确认。
- Shader 恢复后模态屏障检查仍失败，因此不能把模态错误归因于 Shader。已观察到新建屏障激活并 ForceUpdateCanvases 后 depth=-1，而后续帧 depth=0、cull=False，尺寸有效；需要修复创建帧的射线就绪处理，不能直接删去检查而放过输入穿透。

- 已修复上述模态创建帧问题：IModalView.PrepareModalBarrierAsync 在导航候选准备阶段等待原生深度；预备屏障透明、raycastTarget=false，不影响旧页输入，准备受取消及布局时限约束。提交时恢复颜色与射线资格并仍检查深度。隐藏时保留透明就绪节点，恢复无需重新创建；关闭时才移交仍按住的关闭手势。旧纯同步打开入口对模态返回需要异步能力，统一 OpenAsync 路径支持本地立即完成的业务准备及必要的布局等待。
- Unity 2022.3.62f3 实际回归：透明准备阶段 outside=none；准备完成后同一调用中提交，outside=MUI Modal Barrier；隐藏后 outside=none，恢复后重新命中屏障。最终完整 Navigation 默认演示跑到 shutdown complete，模态时下层 input=False，外部射线命中屏障，结果 Completed/value=42；10000 条列表工作集仍为 8，显式尺寸更新锚点保持且 offsetDelta=100，三列 Grid 工作集 12。最终轮次从日志标记 `MUI final modal regression start` 到结束未出现 Exception 或 Shader error。上述回归使用已有示例与 MCP API，未新增测试文件，尚不覆盖真实关闭手势、多宿主、Camera Canvas、完整逻辑焦点或全部设计目标。

- 虚拟列表焦点 API 已接入：CaptureFocus 保存来源代际、稳定键与元素名；FocusItemAsync、RestoreFocusAsync 和 MoveFocusAsync 共用物化定位，Grid 左右不跨行。Unity 2022.3.62f3 实际日志记录：键 1500 聚焦 Ready，下移键 1501 Ready，头部插入后恢复键 1500 Ready，Grid 行末向右 NotFound、向下键 4 Ready，Reset 后恢复返回 IncompatibleSource。日志为 `/private/tmp/mui-editor-shader-recovery.log`。尚不包括原生方向事件、页面焦点自动恢复、外部新选择仲裁和失效控件回退的完整运行验收。
- 开始移除旧同步导航示例依赖：SynchronousNavigationDemo 改名 LocalNavigationDemo（保留脚本 GUID），通过 IViewProvider、UIHost.Initialize 与异步导航入口执行；Resources 接入改用 LoadedPrefabViewProvider，参数候选改用统一异步协议，菜单统一观察异常，销毁等待宿主清理。本示例路由不再声明同步生命周期；共享 ViewModel 的旧名称暂随其他示例保留。核心同步宿主、Provider 契约及其他同步示例仍未删除，不能把此次调用方迁移视为架构收敛完成。
- LocalNavigationDemo 迁移后，MUI.Samples.Navigation.csproj 使用 Unity 2022.3.62f3 和验收工程 uGUI 引用离线构建通过，0 警告、0 错误，覆盖其生成绑定和资源适配依赖。未新增测试文件、未提交；本次示例尚未重新导入验收工程或执行菜单回归。

- Tab 调用方迁移：SynchronousTabsDemo 改名 LocalTabsDemo 并保留 GUID；本地 Prefab 通过 CreateProvider、ChildViewScope.PrepareAsync 和统一 Tab 定义准备。选择、定义更新、缓存清理和父级释放均走异步协议，立即完成守卫返回 ValueTask；移除重复帧扫描，沿用控制器维护。AsyncContentElement.CreateSynchronousProvider 已删除，源码中无剩余调用。Tab 控制器与 ContentViewProvider 内部的同步分支仍待删除。
- 上述迁移后 MUI.Samples.Tabs.csproj 使用 Unity 2022.3.62f3 与验收工程 uGUI 引用构建成功，0 警告、0 错误，覆盖 Tabs 核心、uGUI 适配和生成器。仅为编译证据，尚未重新导入示例并运行切换、失败保留和销毁回归；未新增测试文件、未提交。

- Tab 核心已收敛：删除 TabContentController.Synchronous、Definitions.Synchronous、Cache.Synchronous 与 TabContentDefinition.Synchronous（及 meta），移除同步选择/重试/定义更新/缓存清理/释放协议和模式属性。uGUI TabBar 与重试入口只调用 SelectAsync/RetryAsync。Tab 内容要求支持异步清理的父 Scope，仍待清理的旧纯同步 Scope 在构造阶段明确拒绝。立即完成的准备和守卫保留，不增加调度延迟。
- 该收敛后 MUI.Samples.Tabs.csproj 使用 Unity 2022.3.62f3 引用编译通过，0 警告、0 错误。真实 Editor 刷新后 Console 无 error，反射确认 Select/Dispose 不存在而 SelectAsync 存在。通过 MCP 执行统一协议的失败与清理流程，日志 `/private/tmp/mui-editor-shader-recovery.log` 记录 first=Failed、retry=Failed、attempts=2、definitions=Empty、scopeDisposed=True。此为 Editor 中的核心 API 回归，不是页面切换视觉、缓存 TTL 或真实按钮交互验收；未新增测试文件、未提交。

- 动态子视图收敛：删除 DynamicViewElement.Synchronous 及 meta，去掉 ConfigureSynchronous、SynchronousResult、同步刷新和释放；Provider 字段改为 IViewProvider，PendingChange/Preparation 只保留统一任务状态。旧 IChildViewElement 同步准备协议暂返回不支持，父激活开始时拒绝纯同步 Scope。ContentViewProvider 移除 ISynchronousViewProvider、同步能力查询、同步工厂和重复挂载失败清理；资源获取与归还只走统一契约。原同步动态示例改名 LocalDynamicDemo，保留 GUID，并更新 README。
- MUI.Samples.Navigation.csproj 使用 Unity 2022.3.62f3 引用构建通过，0 警告、0 错误；通过 Unity Sample API 重新导入验收工程 Navigation 示例，Console 无 error。真实 Play Mode 使用本地 Prefab 验证无模型动态内容，日志 `/private/tmp/mui-editor-shader-recovery.log` 记录 create=Ready、mounted=True、clear=Empty、remaining=False，随后退出 Play Mode。本轮证明创建挂载、清空及父清理，不覆盖模型换绑、慢加载取消、失败回滚或视觉交互；未新增测试文件、未提交。

- 修复动态内容准备信号发布时机：在提供方、绑定和版本查询执行前发布本轮 PendingChange 与 Preparation，回调内读取不再取得上轮结果；每个请求完成自身信号，旧请求提交回调也复核刷新代际。结果和准备信号分别直接完成，本地准备不会因桥接任务额外等待一帧。
- MUI.UGUI.csproj 编译通过，0 警告、0 错误；Unity 2022.3.62f3 Play Mode 的提供方回调捕获 PendingChange，日志 `/private/tmp/mui-editor-shader-recovery.log` 记录 callbackCompleted=False、sameRequest=True、immediatelyCompleted=True、result=Ready，Console 无 error。该回归验证回调内可见正确请求及本地立即完成，不证明全部回调重入换源组合。

- 命令协议收敛：删除 SynchronousCommand、ISynchronousUICommand 及 meta；BindingBuilder.Command 强类型接收 IUICommand，统一登记在途执行并由作用域等待清理。旧纯同步绑定作用域明确拒绝命令绑定。Alert/Confirmation 命令改用 AsyncCommand 的立即完成委托。生成器仅接受 IUICommand 属性，Release 构建 0 警告、0 错误，Analyzers/MUI.Generators.dll 已更新且 SHA256 与 Release 产物一致。
- Unity 2022.3.62f3 刷新后 Console 无 error；直接运行立即完成委托与预先取消调用，结果为 Succeeded / Cancelled、Calls=1、Executing=False，两次 ValueTask 均立即完成；反射确认旧 SynchronousCommand 类型不存在。此项是命令协议运行回归，不是 Dialog 按钮、完整并发策略或页面清理的全面验收。
- 命令收敛后 MUI.Samples.Dialogs.csproj 使用 Unity 2022.3.62f3 与验收工程 uGUI 引用完成构建，0 警告、0 错误，覆盖 Core、Resources、Navigation、ChildViews、Dialogs、uGUI 和 Dialog 适配及示例；未新增测试文件、未提交。

- 静态嵌套控件收敛：删除 NestedViewElement.Synchronous 及 meta，统一模型切换、准备、旧请求等待及失败恢复路径，拒绝纯同步父 Scope。LocalNestedDemo、LocalRecyclingListDemo、LocalVirtualListDemo 替代原同步示例并保留脚本 GUID；初始化等待父级准备，销毁等待 Lifetime 排空。回收列表示例资源通过 IResourceLoader 立即完成，Editor 示例菜单同步更新。IChildViewElement 旧同步准备探测仍待随父 View 协议删除。
- MUI.Samples.Navigation.csproj 使用 Unity 2022.3.62f3 引用编译通过，0 警告、0 错误；最新 Navigation 示例已重新导入真实 Editor，Console 无 error。Play Mode 自动构建奖励回收列表，初始 3 项，删除首项并追加后仍为 3 项；关闭日志 `/private/tmp/mui-editor-shader-recovery.log` 显示凭证创建 12、归还 12、仍持有 0，随后停止 Play Mode。此回归覆盖嵌套条目、生成绑定、图标/字体持有与父级清理，不覆盖全部换绑失败、真实点击、虚拟列表性能或所有同步协议清理。
- 上述运行结束前 Console 新增一条 MCP-FOR-UNITY 的 Client handler disposed-object 错误；不能将整个会话描述为零错误。列表资源计数来自独立业务日志，MCP 后续停止 Play Mode 指令成功。

- 普通回收列表收敛：移除同步刷新/准备分支和 CellPreparation 双模式包装，直接等待嵌套条目准备任务；父 Scope 要求支持异步清理，PendingChange 使用单一完成边界。LateUpdate 只补发尚未处理的 dirty 状态，不按生命周期模式分叉。
- 实际从奖励条目的 Remove 绑定事件触发删除，首次复现 ChildView 清理继承命令操作标记、误判等待自身的问题。已在 ChildViewHandle.StartClose 启动异步清理处使用框架独立清理上下文；在途命令仍登记并被最终释放等待，业务直接等待自身的检查未放宽。
- 修复后 MUI.UGUI.csproj 编译 0 警告、0 错误。Unity 2022.3.62f3 同一路径回归：Remove 后 Count=2、Pending=RanToCompletion、Error=none，关闭后凭证创建 10、归还 10、剩余 0；Console 无 error。日志从 `/private/tmp/mui-editor-shader-recovery.log` 的 `MUI recycling command fixed regression start` 标记开始。此为实际绑定事件调用，不是鼠标命中验证；首次失败不计入通过结果。未新增测试文件、未提交。

- 虚拟列表收敛：删除 VirtualListElement.Synchronous 及 meta，移除独立同步刷新、ScrollToKey 和 Retry；准备、定位、重试使用原有统一任务路径。本地条目仍可立即完成，测量/选择回调允许必要的布局等待。移除双模式 CellPreparation 包装，直接捕获每批条目任务，减少重复数组转换。父 View 和所有子元素删除 TryCompleteSynchronousPreparation，仅使用 Preparation；旧纯同步父 Scope 在控件激活时拒绝。
- 首轮收敛后 MUI.UGUI.csproj 构建通过，0 警告、0 错误；随后的任务批次简化由真实 Unity 2022.3.62f3 Editor 编译和 Play Mode 验证。2000 项动态尺寸列表初始 Ready/4 个实例；定位键 1900 Ready/3 个实例，切换三列 Grid 定位键 500 Ready，再切回单列定位键 1500 Ready/4 个实例，最终状态 Ready。日志 `/private/tmp/mui-editor-shader-recovery.log` 包含 `MUI unified virtual regression`，本轮 Console 无 error。该项不替代全部尺寸变更、真实拖动、焦点与 Player 验收。未新增测试文件、未提交。

- 子视图槽位收敛：删除 ChildViewSlot.Synchronous 及 meta，以及独立 Rebind、Replace、Clear、Dispose、同步能力查询和同步退役回调。槽位仅维护 ReplaceAsync / RebindAsync / ClearAsync / DisposeAsync 的准备、取消、隔离与最终清理流程；构造时拒绝旧纯同步父 Scope。现有动态内容与 Tab 调用方已使用统一入口。
- MUI.Samples.Tabs.csproj 使用 Unity 2022.3.62f3 引用构建通过，0 警告、0 错误，覆盖 ChildViews、Tabs、uGUI 和示例。真实 Editor 执行槽位回归，日志 `/private/tmp/mui-editor-shader-recovery.log` 记录 failure=Failed、cancelled=Cancelled、clear=Empty、disposed=True，Console 无 error。首个 MCP 验证脚本因 CodeDom 不支持 async ValueTask lambda 而未编译，改用 Task 委托包装后实际执行成功；该脚本错误不是仓库编译错误。尚不覆盖全部慢加载隔离与真实内容切换组合，未新增测试文件、未提交。

- 对话框收敛：SynchronousDialogsDemo 改为 LocalDialogsDemo，保留脚本 GUID，使用默认 Lifetime、Initialize、OpenAsync、CloseAsync、BackAsync 和 ShutdownAsync。确认流程等待业务结果与真实清理，再展示提示框；移除帧轮询意图和旧同步订阅流程，两种标准路由删除同步生命周期声明。修复 DialogService 把结果提交时 Pending 清理快照误报为失败的问题，保留排空后归还串行许可的约束。
- 上述对话框修改后，MUI.Samples.Dialogs.csproj 使用 Unity 2022.3.62f3 与验收工程 uGUI 引用构建通过，0 警告、0 错误。现有验收 Editor 确认为 2022.3.62f3，AssetDatabase.Refresh 后 Console 无 error；Dialogs 示例尚未导入，未验证真实点击、确认服务排队或延迟清理运行路径。未新增测试文件、未提交。

- BorrowedViewProvider 移除 ISynchronousViewProvider 与能力查询，获取、释放只使用 IViewProvider / ViewLease；删除无调用方的 ChildViewScope.Prepare 同步转接入口。Unity 2022.3.62f3 Play Mode 日志 `/private/tmp/mui-editor-shader-recovery.log` 记录 immediate=True、exclusive=True、repeatRelease=True、cancelled=True、reacquired=True，验证本地立即完成、重复借用拒绝、重复释放、预取消及归还后再次获取；Console error 为空，已退出 Play。此验证不覆盖完整父子导航或视觉交互，核心同步宿主及 Prefab 提供方分支仍待删除。

- 独立子视图示例收敛：LocalChildDemo / LocalThingPresenter 替代原同步示例并保留脚本 GUID；默认 Lifetime，所有准备、更新、换绑、停用、恢复、释放采用异步协议，菜单操作防重入并观察异常。参数候选保留提交失败及回滚演示，使用 IPreparedArgsUpdate.DisposeAsync。唯一外部调用方迁移后删除 ChildViewScope.PrepareSynchronous。Navigation 示例通过 Unity Sample API 重新导入返回 true，Console error 为空；真实独立子视图运行路径尚未验收。

- 子视图模板收敛：删除 SupportsSynchronousPreparation / SupportsSynchronousLifecycle 及对应构造参数，更新 Navigation 的子视图和 Tick 示例。删除无调用方 AdoptSynchronous，以及 CreateModel 的同步标志和旧能力检查；Scope 验证提供方类型收紧为 IViewProvider。Unity 2022.3.62f3 重新导入 Navigation 成功，反射确认两个模板标志及 PrepareSynchronous 均不存在，Console error 为空。本轮只验证 API 删除与编译加载，不代表完整子视图生命周期运行验收。

- 子视图操作收敛：删除 ChildViewHandle.ArgsUpdate.Synchronous、Rebind.Synchronous、Transitions.Synchronous 及 meta，移除 Scope 的 Deactivate / PrepareReactivation 同步入口、RunSynchronous 辅助事务及句柄抽象同步过渡方法。IsUpdatingArgs 只观察统一候选操作；ValidateTransition 继续为异步停用和恢复共用。旧同步销毁与父级生命周期分支仍未完全删除。
- 上述子视图操作删除后，MUI.Samples.Navigation.csproj 使用 Unity 2022.3.62f3 引用构建通过，0 警告、0 错误。现有验收 Editor 反射确认四个同步方法不存在，对应 Async 方法均存在；Console error 为空。此为编译与 API 形状验证，不代替换绑、失败恢复、停用重激活的运行验收；未新增测试文件、未提交。
- ChildViewScope 销毁收敛：仅实现 IAsyncDisposable，删除同步销毁文件、能力查询和同步完成状态；构造阶段拒绝旧同步 Lifetime，统一由 disposal 任务记录完成与失败。参数更新、换绑、过渡和 Slot 调用方只保留线程检查。修正 Slot 遗留调用后，MUI.Samples.Navigation.csproj 使用 Unity 2022.3.62f3 引用构建成功，0 警告、0 错误。现有 Editor 临时代码验证空 Scope 随父释放、重复释放和旧 Lifetime 拒绝，日志 `/private/tmp/mui-editor-shader-recovery.log` 记录 ownerDisposed=True、repeatDispose=True、legacyRejected=True，Console error 为空；未覆盖有子项的完整关闭链。子句柄和旧宿主同步分支仍待删除；未新增测试文件、未提交。

- 子句柄关闭收敛：删除 ChildViewHandle.Synchronous、ChildViewScope.Requests、IChildRequestHost 及 meta，移除同步凭证和完成状态；RequestClose / DisposeAsync 共用关闭任务，保留独立清理上下文和命令排空。导航、View、子句柄移除同步请求 Pump；业务 Tick 不再为旧关闭队列注册。Unity 2022.3.62f3 Play Mode 通过 LocalRecyclingListDemo 的 Remove 按钮事件执行自身删除：count=2、PendingChange=RanToCompletion、error=none；CloseExample 后 created=10、released=10、live=0，Console error 为空，已退出 Play。属于命令链及资源清理验收，不是鼠标命中或完整导航验收。
- 删除 ChildViewScope.Mode，子句柄使用默认 Lifetime；Tab、Slot、Dynamic、Nested、RecyclingList 和 VirtualList 删除重复模式检查，仅 Scope 构造保留旧父 Lifetime 拒绝。自动化的旧同步请求仍返回 RequiresAsync。MUI.Samples.Tabs.csproj 使用 Unity 2022.3.62f3 引用构建成功，0 警告、0 错误；Editor Refresh 后 Console error 为空。本轮为编译验证，未新增运行覆盖、测试文件或提交。

- 资源适配收敛：删除无调用方的 SynchronousLoadedPrefabViewProvider（406 行）和仅供其使用的 ISynchronousInstantiableResourceLoader 及 meta。UnityResourcesLoader 暂直接声明仍被调用的 ISynchronousResourceLoader 与 IResourceLoader；没有把通用加载器同步迁移视为完成。MUI.Samples.ResourceIntegration.csproj 使用 Unity 2022.3.62f3 引用构建通过，0 警告、0 错误；Resource Integration 示例在现有验收 Editor 重新导入成功。本轮未新增资源加载运行覆盖、测试文件或提交。

- 本地化资源示例收敛：删除 ISynchronousLocalizationProvider、TextAssetLocalizationProvider.Synchronous 及 meta，移除内存目录 Load、JSON 模式检查和 LifetimeResourceExtensions 同步 Load。保留统一异步目录交付和源资源归还。Navigation 及资源依赖构建通过，0 警告、0 错误；Resource Integration 在 Unity 2022.3.62f3 重新导入成功。临时代码验证内存目录立即完成、独立凭证、释放一份不影响另一份及预取消均为 true；未验证 JSON 解析和加载失败运行路径，未新增测试文件、未提交。

- UnityResourcesLoader 删除独立同步 Load 和接口，凭证使用 ResourceLease。ResourceImageDemo 删除同步模式开关、同步配置和同步清理；默认 Lifetime，等待父级准备后提交，示例加载器仅实现 IResourceLoader 并使用统一凭证。Navigation 构建成功，0 警告、0 错误；两份示例重新导入后，Unity 2022.3.62f3 Play Mode 创建资源示例，切换键后立即 Close，等待忽略取消的加载返回：pending=0、created=6、released=6、live=0、cleanup=RanToCompletion，Console error 为空，已退出 Play。此为迟到凭证归还验收，不覆盖视觉、失败重试或真实 Resources 资产加载。未新增测试文件、未提交。

- 控件资源配置收敛：删除 View.ConfigureSynchronousResources 与四类控件的同步配置及槽创建方法；ViewResourceContext 仅保留 IResourceLoader，父级继承和首次准备跟踪沿统一路径执行。同步模式诊断字段及 Inspector 展示一并删除。ResourceSlot、ElementResourceOwner 内部同步执行仍待删除，本次不能视为资源模块收敛完成。

- ResourceSlot 与 ElementResourceOwner 执行收敛：删除 ResourceSlot.Synchronous 及 meta，同步字段、同步替换/清空/释放和模式查询；四类控件只登记统一异步持有权。Navigation 构建通过，0 警告、0 错误。Unity 2022.3.62f3 Play Mode 资源示例故意失败三项 Cool 请求后仍显示 Warm，重试成功显示 Cool；再次请求 Warm 并立即关闭，pending=0、created=9、released=9、live=0、cleanup=RanToCompletion。Console 三条 DemoLoadException 对应注入故障，不能报告为零错误。已退出 Play，未新增测试文件或提交；旧同步资源异常的兼容识别仍待随核心资源契约删除。
- 核心资源加载契约收敛：删除零实现、零调用方的 ISynchronousResourceLoader 及 meta；LocalRecyclingListDemo 字体和 Sprite 凭证改用 ResourceLease。Navigation 及其依赖使用 Unity 2022.3.62f3 引用构建通过，0 警告、0 错误；Unity Sample API 重新导入 Navigation 返回 true。Console 仍保留上一轮三条主动注入的 DemoLoadException，本轮无新增运行覆盖。同步资源异常仍被 Prefab 工厂和旧导航回滚使用，尚未删除；未新增测试文件、未提交。
- 共享依赖示例迁移为 LocalDependenciesDemo，三份 partial 与 meta 保留 GUID，路由图上下文菜单同步更新。Initialize / OpenAsync / ReleaseExplicitOwnershipAsync / CloseAsync / ForceCloseAsync / RebindAsync / UpdateArgsAsync / ShutdownAsync 替代同步入口；参数候选改为 IArgsUpdatePresenter / IPreparedArgsUpdate，菜单通过统一异常观察与防重入包装执行，父关闭等待 Pending 清理后报告拥有者状态。Navigation 构建通过，0 警告、0 错误；Unity Sample API 重导入成功，Console 仍为此前三条主动加载失败记录。共享/可选依赖完整运行路径未验收；未新增测试文件、未提交。

- UIHost 删除独立同步初始化、退出、非活动内容清理入口和对应状态；返回输入统一走 BackAsync 并保持视觉结束前防重入，OnDestroy 共用异步退出。Navigation 及其依赖使用 Unity 2022.3.62f3 引用构建通过，0 警告、0 错误；现有 Editor 反射确认旧 InitializeSynchronous / Shutdown 不存在、ShutdownAsync 存在。Console 仍为此前三条主动加载失败记录，本轮未新增完整导航运行覆盖；核心 Navigator 和 Prefab 同步分支仍待收敛。未新增测试、未提交。

- PrefabViewProvider 移除独立同步创建、预加载及能力查询，目录查询共用 GetPrefab；PrefabViewFactory 返回统一 ViewLease。归还现在等待 Destroy 的原生实例实际消失，清理失败仍沿凭证报告。Navigation 及依赖使用 Unity 2022.3.62f3 引用构建通过，0 警告、0 错误。Unity 2022.3.62f3 Play Mode 临时代码验证本地获取立即完成、释放等待帧末销毁、重复释放及独立预加载归还；日志 `/private/tmp/mui-editor-shader-recovery.log` 记录 immediate=True、waitsForDestroy=True、destroyed=True。Console error 为空，已退出 Play；未新增测试文件、未提交。工厂失败回滚异常与核心 Navigator 的旧同步协议仍待收敛，此项不代表完整导航或视觉验收。

- 修复 NestedViewElement 在同步关闭/绑定回调之后才发布 PendingChange 的问题：执行前发布本轮完成源，回调只完成本轮任务；过期请求不再读取或覆盖新请求，保留立即失败参与父级回滚及取消状态。Navigation 及依赖使用 Unity 2022.3.62f3 引用构建通过，0 警告、0 错误。现有 Editor Play Mode 临时代码在绑定回调读取 PendingChange 并注入失败，日志 `/private/tmp/mui-editor-shader-recovery.log` 记录 callbackPending=True、sameTask=True、faulted=True；Console error 为空，已退出 Play。此验证覆盖信号发布及失败传播，不代替完整重入关闭/恢复交互验收。未新增测试文件、未提交。

- Prefab 创建失败统一报告 ResourceLoadException：即使逻辑回滚成功，只要原生实例仍等待帧末销毁，也必须携带实际清理任务；清理异常与销毁失败保留在同一任务。LoadedPrefabViewProvider 的驻留创建失败将依赖归还纳入对外清理任务。Navigation 及依赖构建通过，0 警告、0 错误，Resource Integration 重新导入成功。Unity 2022.3.62f3 Play Mode 注入 Prefab 配置失败，日志 `/private/tmp/mui-editor-shader-recovery.log` 记录 pending=True、destroyed=True、cleanup=RanToCompletion，Console error 为空，已退出 Play。未验证原生 Destroy 抛错及完整远程提供方失败链；未新增测试文件、未提交。

- 补齐 UIHost.ShutdownAsync 的取消等待参数：先接纳退出，再由共享 AsyncWait 结束调用方等待；令牌不传入实际清理，重复调用共用原任务。文档明确预取消仍启动退出。Navigation 及依赖使用 Unity 2022.3.62f3 引用构建通过，0 警告、0 错误。Editor Play Mode 临时注入可控释放作用域，分别验证预取消和启动后取消；日志 `/private/tmp/mui-editor-shader-recovery.log` 两组均记录 cancelled=True、retained=True、stopped=True、completed=True。Console error 为空，已退出 Play。此验证覆盖宿主等待与清理分离，不代表完整业务导航退出验收；核心同步导航创建入口及事务尚未删除。未新增测试文件、未提交。

- 导航参数更新与换绑收敛：删除对应同步导航、实例事务、Presenter 接口及共用同步事务实现；BindingContext 删除独立同步 Rebind 和状态。Unity 2022.3.62f3 Play Mode 中显式注册示例绑定后验证统一 RebindAsync：isolated=True、updated=True、detached=True、legacy=False。首次临时代码遗漏绑定注册产生一条异常，纠正初始化后通过；此为绑定换绑验证，不代替导航参数更新与失败恢复全链验收。
- 根据设计第 85 行纠正遗漏的命名迁移：ViewLease / IViewLease 改为 AcquiredView / IAcquiredView，ResourceLease / IResourceLease 改为 AcquiredResource / IAcquiredResource；文件及 meta 同步改名保留 GUID，更新运行时、示例和文档调用方，不留旧名兼容别名。Editor 已加载新类型，确认旧 ViewLease 不存在。独立同步分支及其 Synchronous*Lease 尚未删除，预加载命名尚待整体核对。
- 上述事务删除和命名更新后完整重跑 MUI.Samples.Navigation.csproj，Unity 2022.3.62f3 引用构建成功，0 警告、0 错误。重命名发生前已启动的旧构建曾因混用旧资源程序集失败，更新后完整重跑已排除该错误；未新增测试文件、未提交。

- 删除独立同步预加载账本、Navigator.Preload / ClearPreloads、同步 Provider 与预加载凭证契约，清除同步缓存维护和退出中的相应调用。Navigation 及依赖使用 Unity 2022.3.62f3 引用构建成功，0 警告、0 错误；本轮预加载运行验收尚未执行，已退出 Play。未新增测试文件、未提交。

- 按目标第 3 节修复参数提交异常：删除 IPreparedArgsUpdate.Rollback 及业务候选的恢复快照，提交异常不再反向写回参数；在候选异步清理前触发故障关闭，结果使用 ViewFaulted，更新导航/子视图清理记录、诊断和示例。Unity 2022.3.62f3 Play Mode 通过实际 Navigator + BorrowedViewProvider + LocalThingPresenter 验证：preparation=PreparationFailed、retained=True、commit=CommitFailed、faulted=True、noRollback=True、state=Failed。该路径等待实际清理后取得终态；仍需覆盖子视图、慢候选及输入原生事件。Console 包含一条注入的准备异常、两条同一次提交异常，重复报告已登记为目标第 7 节缺口。Navigation 及依赖编译通过，0 警告、0 错误，已退出 Play；未新增测试文件、未提交。

- 按第 7 节接入稳定异常标识与报告去重：UIErrors 使用异常弱键保存 Guid 与原子报告状态，查询不触发输出；聚合按实际原因分别报告，单项聚合复用原因标识。操作结果、子视图变更、生命周期事件、导航追踪公开 DiagnosticId，文本追踪同时输出标识。Unity 2022.3.62f3 Play Mode 重跑参数失败导航路径：totalReports=2（准备与提交各一次）、wrapperId=True、traceMatches=5；Console 正好两条注入异常。16 个并发报告者及另一新异常共报告 2 次，抛错观察者不破坏分发；嵌套混合聚合报告 2 个不同原因，没有因已报原因丢失新异常。已退出 Play，编辑器加载新结果属性；最终 Navigation 及依赖编译通过，0 警告、0 错误。尚未完成全部包装异常、宿主/阶段诊断上下文与 Player 验收。未新增测试文件、未提交。
