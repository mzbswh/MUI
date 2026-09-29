# 实现与验证状态

资源职责整改（2026-09-21）：已删除共享加载器、资源预算包装器和全局 MemoryTrimCoordinator；不再提供 MUI.ResourcePolicies。具体 Unity Resources/JSON 后端移至 Samples~/ResourceIntegration，仅供项目接入参考。UIHost 不监听 Application.lowMemory，项目显式调用 ClearInactiveContent/ClearInactiveContentAsync 清理该宿主的预加载与停用页面；Tab 使用 ClearCache/ClearCacheAsync。资源槽仅协调单个显示位置，后端加载和卸载归项目。本文后续历史实现记录不代表被删除能力仍属于当前框架。

目标保持为完整实现 `UI-FRAMEWORK-DESIGN.md` 的核心与标准模块。以下为当前工作树状态，不以阶段性交付替代完整目标。条件扩展仍遵循设计文档的启用条件。

更新：2026-09-29。

2026-09-29 uGUI 线程与释放所有权：View/Element 的存活查询、初始化、通知和释放统一先校验 Unity 回调记录的主线程，诊断快照、Prefab 工厂及提供方也在原生访问前检查。`UnityMainThread.Require()` 对项目适配器开放。同步和异步资源凭证新增可选释放线程约束，错误线程请求在所有权转移前拒绝，正确线程仍可重试；内置和示例 uGUI 页面、借用子视图及示例异步预加载凭证启用该约束。使用 Unity 2022.3.62f3 对应 uGUI、TMP、Input System 程序集顺序完成 41/41 个离线 Release 构建，零警告、零错误；611 个 C# 文件格式与大括号检查及 `git diff --check` 通过。本次未重做逐文件编译覆盖核对或启动 Unity，异步续体、回调重入和实际原生释放仍待运行验收。

2026-09-29 模态退出故障清理：`View.SetModalBarrier(false)` 的关闭手势移交若抛错，立即断开半移交屏障的页面引用，并分别尝试停用与销毁；清理也失败时保留全部错误，避免同帧重试复用异常屏障。`MUI.BasicExample.Editor` 及依赖使用 Unity 2022.3.62f3 程序集离线 Release 编译零警告、零错误，611 个 C# 文件格式与大括号检查及 `git diff --check` 通过。本轮未启动 Unity，原生回调抛错和真实指针输入仍待运行验收。

2026-09-29 UGUI 主线程职责：将线程身份记录与校验从即时布局工具移到 `UnityMainThread`；Unity 运行时与 Editor 初始化回调各自接线，布局类只维护重建状态。UIHost 初始化、手动帧驱动及返回输入在读取 Unity 对象或帧状态前检查线程。`MUI.Editor` 与 `MUI.BasicExample.Editor` 使用 Unity 2022.3.62f3 对应程序集离线 Release 编译，均零警告、零错误；611 个 C# 文件的格式与大括号检查、`git diff --check` 通过。未重跑全量 41 项或启动 Unity，编辑器回调顺序、真实输入和运行时故障组合仍待验收。

2026-09-29 上一源码快照完整离线编译覆盖：基础示例 Editor 菜单新增独立 asmdef 与构建项目。使用匹配 Unity 2022.3.62f3 的 uGUI、TMP、Input System 程序集，顺序完成 Tools~/Build 的 41/41 个 Release 构建；将 MSBuild 实际 Compile 项与 Runtime、Editor、Samples、Basic Example、生成器目录中的 609 个 C# 源文件逐一对照，未发现漏编、重复或额外项目文件。610 个 C# 文件格式与大括号检查通过；未启动 Unity，本次离线结果不证明新的 Editor asmdef 在 Unity 中导入、基础示例物理点击或完整设计验收。

2026-09-29 绑定工厂所有权校验：页面初次准备及同步/异步换绑均先确认工厂返回的上下文非空、关联当前模型与 View 且处于 Unbound，再交给本次激活清理；直接继承 BindingContext 的项目实现须覆写 BoundView，否则准备阶段拒绝接管。错误返回的其他页面上下文不会被误解绑。初次准备的 View/子视图激活与可覆写绑定配置入口统一进入宿主回调保护。绑定清单构造时拒绝空白字段、无效枚举、非 Element 类型及非 ViewModel 清单类型。Core、BasicExample、ChildViews、Navigation/Settings 示例离线编译零警告、零错误，610 个 C# 文件格式与大括号检查通过；未新增测试或启动 Unity，故障工厂回调的运行行为仍待验收。

2026-09-29 基础示例独立工程导入：在正常用户授权环境中明确指定 Unity 2022.3.62f3，对 `/private/tmp/MUI-BasicExample-20260929-01` 执行一次限时批量导入，退出码 0；日志 `/private/tmp/mui-basic-import-once-20260929.log` 显示 AssetDatabase 完成初次刷新并正常退出，`MUI.BasicExample.dll`、`MUI.Navigation.dll` 和 `MUI.UGUI.dll` 均已生成，未见 C# 编译错误。复用页面和共享依赖的参数比较器现处于导航回调保护内，比较后复核实例资格；基础示例及依赖离线编译零警告、零错误，610 个 C# 文件格式检查通过。导入不证明 Play 画面、真实指针点击、结果回收或完整设计第 19/24 章验收。

2026-09-29 基础示例图形启动复核：对同一独立工程明确指定 Unity 2022.3.62f3 执行一次 `unity projects open`，命令退出码为 0；等待后 `unity editors running` 仍未列出该工程，Unity Hub 项目表也没有该工程，因此没有取得 Play 会话。此返回码只证明启动请求已交给 Hub，不证明编辑器实际打开；本轮停止图形启动尝试，基础示例的 Done 点击和结果回收仍未运行验收。

2026-09-29 基础示例工程结构：包内 `ExampleProject~` 改为模板，通过 `Tools~/create-basic-example.py` 在包目录外生成独立 Unity 2022.3.62f3 工程，避免同一场景在本地包与项目 Assets 下出现路径别名。工具只复制 Assets、Packages、ProjectSettings，使用 JSON 解析器同步改写 manifest 与锁文件中的本地包引用；已在 `/private/tmp/MUI-BasicExample-20260929-01` 生成，资源与工程设置逐目录比较一致，引用解析回当前 MUI 根目录；已有输出及包内输出均被拒绝。该阶段尚未启动 Unity；后续导入结果见上，画面与真实点击仍未验收。

2026-09-29 宿主容量与退出所有权：UIHost 的同步/异步初始化均暴露 queueCapacity，异步初始化补充 preloadCapacity，参数先于默认目录创建校验。导航快照增加持久的 HasUnconfirmedCleanup；页面或缓存释放失败后，即使实例已移出账本且历史诊断已清空，宿主也不会据“零实例”提前释放提供方。MUI.BasicExample 及依赖用 Unity 2022.3.62f3 程序集离线编译零警告、零错误，610 个 C# 文件格式与大括号检查通过；未启动 Unity，故障路径仍待运行验收。

2026-09-29 示例提供方契约复核：LoadedPrefabViewProvider 在交付 Lease 前清理失败时，改用 ResourceLoadException 携带已失败的回滚结果，供导航保留预加载容量或等待创建回滚；已驻留 Prefab 的同步创建不再被异步加载额度拒绝。资源接入示例及依赖使用 Unity 2022.3.62f3 程序集离线编译零警告、零错误，610 个 C# 文件格式与大括号检查通过；未启动 Unity，故障注入与真实资源库行为仍未运行验收。

2026-09-29 预加载所有权复核：异步批次逐份 Lease 成功释放后才归还容量；未取得 Lease 但提供方报告后台回滚时，导航等待回滚完成，回滚失败或同步残留均保留容量并报告。同步/异步预加载的历史清理错误按终态容量保留并统计省略数；宿主退出即使一段预加载清理失败仍尝试其余批次。`MUI.BasicExample` 及依赖离线编译零警告、零错误，610 个 C# 文件格式检查与 `git diff --check` 通过；未启动 Unity，故障组合与真实场景退出仍待运行验收。

2026-09-29 模态首帧射线准备：对照当前 Unity 2022.3.62f3 随附 uGUI 源码，GraphicRaycaster 会跳过深度为 -1 或被裁剪的 Graphic。新建或重新启用模态屏障后，View 在尚无深度且非 Canvas 重建期间调用 ForceUpdateCanvases；刷新后仍无有效深度、被裁剪或失去射线目标则报告表现失败。直接销毁 View 时也会移交已登记的关闭手势；可选输入状态提供方返回可释放的登记凭证，避免场景级适配长期持有项目对象。MUI.BasicExample、MUI.UGUI.InputSystem 及依赖离线编译零警告、零错误，610 个 C# 文件格式、大括号和 diff 检查通过；未启动 Unity，首帧真实射线、设备手势和 Canvas 回调重入仍需运行验收。

2026-09-29 宿主失败清理复核：基础示例在宿主初始化成功后，若路由创建或首次打开抛错，立即关闭已接管提供方的宿主；若关闭也失败，聚合保留两项错误。异步 UIHost 只有在导航退出请求成功启动后才清除返回订阅并继续释放提供方；启动前被拒绝时保留提供方，导航仍运行时允许在 UI 线程重试。使用 Unity 2022.3.62f3 程序集离线编译 MUI.BasicExample 及依赖，零警告、零错误；610 个 C# 文件格式和大括号检查、`git diff --check` 通过。本轮没有启动 Unity；这些检查不证明真实输入和运行时故障组合。

2026-09-29 基础示例工程交互编辑器复验：从 `ExampleProject~` 作为工作目录直接启动指定的 Unity 2022.3.62f3 后，内置 Shader 编译错误和整窗洋红消失。通过系统文件对话框选择场景时，Unity 将同一物理文件映射为 `Packages/com.mzbswh.mui/ExampleProject~/Assets/Basic/Basic.unity`，实际载入空场景；示例工程新增 `MUI > Basic Example > Open Scene` 菜单，以 `Assets/Basic/Basic.unity` 打开后，Hierarchy 出现 Main Camera、UI Host、Basic Demo 与 EventSystem，Play 中显示绑定后的标题和 Done 按钮，Console 为 0 错误、0 警告。桌面工具的坐标点击未触发 Done，故页面结果、清理与真实指针输入仍未验收。此轮没有新增测试、没有提交。

2026-09-29 基础示例可见性修复：静态复核 `ExampleProject~/Assets/Basic/Basic.unity` 时发现根 Canvas/UI Host 的 RectTransform.localScale 为 `(0,0,0)`，会把所有子界面缩成零；已恢复为 `(1,1,1)`，场景和 Prefab 中不再有零缩放节点。`MUI.BasicExample` 连同生成器及依赖按 Unity 2022.3.62f3 匹配程序集完成 Release 离线编译，零警告、零错误，`git diff --check` 通过。打开工程时 Unity 弹出新版软件条款窗口，尚未由用户本人处理，因此本次仍未进入 Play、确认画面或物理点击；此前的导入与 GUID 核对不能当作视觉验收。

2026-09-29 材质清理异常增量：内置 uGUI `Image`、`RawImage`、`Text` 的原生材质 setter 在写入内部引用后触发脏标记回调。材质清空时若该回调抛错，适配器仅对这些精确内置类型再次赋 null 并清空 CanvasRenderer；确认成功后报告原异常，允许资源槽归还凭证。自定义 Graphic、派生原生组件及 TMP 保留原有保守持有策略。`MUI.UGUI` 对 Unity 2022.3.62f3 的 Release 离线编译为零警告、零错误；607 个 C# 文件的成员布局、空白和控制流大括号检查通过。当前没有连接中的 Unity 编辑器，此分支尚未执行 Play Mode 故障注入；不能把静态验证记为运行验收。

2026-09-29 基础示例工程：新增仓库内 `ExampleProject~`，包含 Unity 2022.3.62f3 工程设置、相对路径本地 MUI 包、真实 uGUI Prefab、场景及最小的生成绑定/纯同步类型化导航源码。Unity 首次导入发现包路径按 `Packages` 目录解析，已修正为 `file:../../`；再次导入退出码 0，实际生成 `MUI.BasicExample.dll`，包锁文件确认只依赖本仓库 MUI 与内置 uGUI/模块。离线 Release 编译零警告零错误，格式、成员布局及控制流大括号检查已纳入新源码，共 607 个 C# 文件通过。场景脚本和 Prefab GUID 静态核对匹配。图形编辑器曾打开该工程，但本轮未取得可信的 Play Mode 画面和物理点击结果，因此示例工程的运行交互仍待复验；该工程不代替项目资源后端或目标设备验收。

2026-09-29 原生赋值与清理故障复验及修复：在独立 Unity 2022.3.62f3 Play Mode 中，用既有真实 uGUI Image、RawImage、Text 和示例加载器，在原生脏标记回调内故意抛错。先赋候选后抛错的 Sprite 路径仍返回 ResourceAssignmentException、冻结资源槽，新旧凭证在成功清空前均未归还；清理后创建/归还为 2/2，后续帧两个 Sprite 均销毁。另发现清空 Sprite 已将原生引用设为 null、但脏标记回调抛错时，旧实现把凭证永久留在已结束 Lifetime 中，重复 Dispose 无法重试。现仅对可直接读取引用的 Sprite、Texture 和 uGUI Font，在异常后确认属性已清空并清空 CanvasRenderer，再报告原回调错误并完成凭证归还；不能确认的候选赋值继续冻结保留。修复后这三种控件的同步清理均为创建/归还 1/1、Pending=0、错误上报一次；Sprite 异步清理结果相同，重复释放未重复归还。Material getter 会回退默认材质，尚未加入同类确认路径；清空异常仍保守保留凭证和诊断。uGUI 与依赖 Release 编译零警告、零错误，604 个 C# 文件格式、成员布局及大括号检查通过；以上是不落盘的编辑器故障注入，不代表所有项目后端或目标设备通过。

2026-09-29 真实 Prefab 导航故障组合复验：同步及异步 Navigator 均以独立验收工程的 PureSyncView Prefab 运行。两个父页面共享依赖时只创建一份，直接 Close 返回 InUse，按依赖层关闭保留它；Replace 候选 OnOpen 失败后旧父页仍 Open、可见、可输入，拥有者仍为 2。关闭第一个父页后依赖仍 Open/1 个拥有者，末父页关闭后依赖 Destroyed；CloseAll、显式持有与释放、ForceClose 级联结束后实例和请求计数均归零。另用同一真实 Prefab 复验守卫拒绝结果候选后不发布结果，再次允许时只发布新值；两个页面借用同一 VM 时，按钮命令只完成来源页面。慢速 OnCloseAsync 的模态页进入 Closing、清理任务仍等待时，屏障已停用且下层 View 输入恢复；放行后 OnCloseAsync/OnClose/OnDestroy 各一次。该检查使用现有示例和内存探针，不新增脚本、测试或场景资产；未证明物理指针无穿透、共享 VM 准备回滚的任意业务副作用或其他平台。

2026-09-28 虚拟列表与子视图回调性能复核：同步刷新复用已提交的键索引，移除视口切片、临时目标字典和单元快照，同步分支不再创建异步准备批次；异步调度闭包仅在实际启动时创建。独立 Unity 2022.3.62f3 的 200×260 视口、40 行高和 overscan=1 下，100/1000/10000 项在顶部均物化 8 个单元，偏移 401 时均为 9 个；预热后 10000 次同视口滚动、直接滚动回调和已绑定标签更新均无 GC.Alloc 样本，显式分配阳性对照仍能记录。跨行滚动仍约有 455 次分配/次，主要在子激活、绑定、清理和 ExecutionContext 写入，不作为整个列表零分配结论。ProfilerRecorder 的 GC.Alloc 样本值单位是 TimeNanoseconds，本轮只用样本次数判断分配，未取得修改后的字节数证据。子视图句柄改用每个闭合泛型类型共享的 AsyncLocal 和带归属的回调链；1000 次跨行循环的 ExecutionContext 槽数量从 1 增至 4 后保持稳定，避免同一同步循环中槽数量随句柄创建增长。这不证明此前存在跨工具调用的全局内存泄漏。同步嵌套、抛错、捕获后回调退出及并行异步回调的执行归属检查保持正确；三列网格的 27 个标签、条目定位、模板/高度切换、首项移除，以及自定义键比较中关闭列表或替换来源均复验通过。动态高度全矩阵、剩余跨行分配、分阶段性能和长期设备内存仍待验收。

2026-09-28 共享回调修改后的 Unity 回归：现有 Navigation 自动演示达到 `MUI Navigation shutdown complete`，活动 View 数量为 0；Tabs 演示达到 `MUI Tabs cleanup complete`，失败重试、快切淘汰、动态页签移除与父关闭均得到既有预期结果。两轮日志为 `/private/tmp/mui-editor-repair-20260928.log`；运行的是既有演示，没有新增脚本、测试或场景资产。Navigation、Tabs 示例及其依赖 Release 编译零警告、零错误，604 个 C# 文件格式、成员布局与控制流大括号及 `git diff --check` 通过；本轮未重跑全部 39 个离线工程。验收后恢复未 Play、未变脏的 Tabs 4 场景和原后台运行设置。未提交；上述回归不代替完整故障组合、物理输入及目标设备验收。

2026-09-28 工程完整性复核与原生层级停用修复：对照包的六份 Sample 声明逐目录检查，标准导入目录中的 233 个文件无缺失且与当前包一致；TMP Settings/defaultFontAsset 可读取，默认字体为 LiberationSans SDF，BlitCopy、GUIRoundedRect、Skybox、UI/Default 与 Sprites/Default 均可用且无 Shader 消息，当前编辑器截图无洋红。历史启动日志仍保留内置 Shader 无法打开 HLSLSupport.cginc 的错误，安装中的 include 文件实际存在；不能据此认定资源文件丢失或已证明单一启动根因。退出 PureSync 时另复现 View.OnDisable 的输入通知触发导航器原生排序，Unity 拒绝在父对象停用过程中改变子项 sibling。输入通知现只重算门控与焦点；页面结构变化仍更新排序与模态遮罩，并通过独立脏标记保留重入期间的排序请求。真实 Prefab 单页连续 10 次父停用/恢复均关闭并恢复输入，三页排序保持 1/2/3，关闭第 1 层后保留原页；模态打开、父及页面停用/恢复、BringToFront、遮罩紧邻模态页下方与关闭后原页恢复均通过。导航程序集 Release 编译零警告、零错误，未新增测试或提交，验收后恢复 Tabs 4 场景。此证据不代替完整设计验收。

2026-09-28 主题、本地化与偏好真实 uGUI 验收：在既有真实 Prefab 的 Play 会话中，仅创建临时原生 Text/VerticalLayoutGroup，通过现有公开 API 接入同步 Lifetime、ThemeService、LocalizationService 和 UIUserPreferences，不新增脚本或测试。主题从 dark/18/spacing=12 切至 light/24/spacing=20，FontScale=1.5 后原生字号为 36；缩放往返得到 24/36，未累计放大。ReducedMotion 使 ColorTint 时长变为 0，关闭偏好与最终解绑均恢复作者配置 0.1。缺少活动订阅 Token 的目录被 KeyNotFoundException 拒绝，旧主题和颜色保持不变。英语的 `3 items`、`1,234.50 kg` 切至德语长文本和 `1.234,50 kg`，原生字体从 LegacyRuntime 换为项目已导入的 LiberationSans；1080×1920 与 1280×720 渲染截图中，文本完整换行，两行高度匹配 preferredHeight 且没有重叠。空缺译文回退至英语时，当前语言 ar-EG 与实际译文 en-US 分别保留，数量 1 使用回退目录的 `1 item` 形式及 LTR 方向，本地译文返回 RTL 元数据；这不证明阿拉伯字形塑形。更新 Message 立即显示 `1 item`。重复 Dispose 后 Lifetime 已结束/已释放、PendingOperationCount=0；随后切主题、语言和偏好不再写入原生目标，临时布局与标签在后续帧均销毁。截图为 `/private/tmp/mui-unity-validation/ValidationCaptures/mui-theme-localization-1280x720-20260928.png` 和 `mui-theme-localization-safe-1080x1920-20260928.png`。该证据覆盖当前 uGUI 接线，不代表全部语言、TMP 字体、目标设备或任意布局通过。

2026-09-28 安全区与键盘矩形验收：在上述 1080×1920 渲染帧中，将 SafeAreaFitter 放在屏幕空间根 Canvas 的直接子项；项目显式提供安全区 `(54,96,972,1728)` 及底部键盘区域 `(0,0,1080,480)`。Refresh 得到可用区域 `(54,480,972,1344)`，锚点为 `(0.05,0.25)` 至 `(0.95,0.95)`，与键盘区域没有重叠，内部长文本布局仍完整。矩形使用公开 override 入口，不证明移动端原生键盘区域、焦点滚动或旋转后平台接线；没有向 UI 框架增加键盘管理职责。

2026-09-28 IL2CPP 指针诊断与键盘复验：临时工程中的 PureSyncInputView 为已有真实 Prefab 的独立副本，仅使用 Unity 自带 EventTrigger 和持久化 TextElement.Content setter 观察 PointerEnter/Down/Up/Click，不新增诊断脚本。构建 `build-d8fb541ec7` 成功，8.185 秒、751.01 MB、0 errors、1 warning（Unity Services cloud project ID）。Player `/private/tmp/mui-unity-validation/ValidationBuilds/MUI-AOT-Input.app` 正常显示；CUA 点击、置前和拖动未取得指针阶段或完成结果，键盘 Return 则完成 Completed(42)、Cleanup=Complete，日志 `/private/tmp/mui-aot-input-player.log`。同一副本在编辑器中直接调用持久化 PointerEnter 监听会显示事件名称；CUA 指针位于按钮图像时，渲染帧末 Unity Input.mousePosition 为 `(1751,1569)`，超出 1080×1920 Game View 的横向范围，该位置射线为空，而按钮中心 `(1032.75,1886.25)` 命中 Confirm 和根 View。该对照提示工具输入与游戏输入坐标映射仍需定位，不能将鼠标验收记为通过，也不能据此修改按钮逻辑。Player 已退出；编辑器恢复 Mono2x/Disabled、原 API/后台设置、空构建场景列表、Free Aspect 和未变脏的 Tabs 4 场景；临时目录已随域重载清除，主目录仍为 1 个目录、2 个页面、2 条有效路由，Console 无错误或警告。未新增测试或提交。

2026-09-28 真实 Prefab 制作与构建校验闭环：在独立 Unity 2022.3.62f3 中实际调用现有 PageWizard，生成 `Assets/Acceptance` 下的五份类型化页面源码与 AcceptanceView Prefab；生成属性、命令、BindingContext/Factory 和 Route 均通过 Unity 编译，Prefab 根为 RectTransform/View，Title 与 Close 契约无问题。使用该真实 Prefab 的同步页面打开 Succeeded/Ready，Presenter 参数正确显示；原生 PointerClick 派发后结果 Dismissed、Cleanup=Complete，后续帧原生 View 为 0。生成 Presenter 只实现 OnOpen，参数更新返回 Rejected，不将其记为支持更新。另用现有 SynchronousNavigationDemo 接入独立 PureSyncView Prefab，保存 `Assets/Acceptance/PureSync.unity`，并在项目 Editor 初始化入口登记两份真实 Prefab。编译与 Play 域重载后仍为 1 个目录、2 个页面、2 条路由，校验无问题；目录接线属于验收项目，不加入 MUI Runtime。故意将同步导航的 Confirm 契约映射到只有 Close 的真实 Prefab，实际构建 `build-0d5ee313a3` 被 UIBuildPreprocessor 以缺少 ConfirmCommand 目标拒绝。移除故障登记后 `build-52cc1ec5ed` 的 macOS IL2CPP 构建成功，45.09 秒、751.14 MB、0 errors、1 warning；剩余警告为 Unity Services cloud project ID，未覆盖目录警告已消失。产物 `/private/tmp/mui-unity-validation/ValidationBuilds/MUI-AOT-Catalog.app` 的真实 Prefab 首场景正常显示，CUA 原生键盘 Enter 后日志 `/private/tmp/mui-aot-catalog-player.log` 确认 Completed(42)、Cleanup=Complete。另一次鼠标操作未取得完成结果，不能记为通过；此目录只覆盖登记的两份 Prefab，不代表所有程序化示例或任意项目资源提供方已验收。Player 已退出，编辑器恢复 Mono2x/Disabled、原 API/后台运行/空构建场景列表及未变脏的 Tabs 4 场景。只新增向导产物、Prefab/场景及项目目录接线，未新增测试或提交。

2026-09-28 真实 Prefab 循环与原生销毁复验：现有同步导航示例的真实 Prefab 连续执行 500 次缓存打开/关闭，全部 Succeeded/Ready 且清理 Complete，创建次数保持 1，结束时活动实例、在途/排队请求、预加载占位、待清理与淘汰计数均为 0，缓存和原生 View 各为 1；最早一轮 Handle 仍可直接读取结果。热缓存 Open 返回耗时 P50/P95/P99 为 0.0604/0.0979/0.1314 ms，不包含帧末布局，不作为设备或阶段性能基线。清缓存后再执行 100 次非缓存打开/关闭，失败数 0；工具回调结束前仍有 101 个原生 View（含原缓存项），后续帧全部销毁为 0，区分逻辑清理完成与 Unity 原生销毁。显式 Shutdown 后活动实例、缓存、请求、预加载与清理计数全部为 0。该证据覆盖当前同步常驻 Prefab 路径，不证明长时运行、异步不合作任务、后端实际卸载或进程内存收敛，未新增测试或提交。

2026-09-28 渲染时机、射线与截图复验：在编辑器工具回调内直接查询时，Screen 宽度为 813 而按钮中心 X 为 995，GraphicRaycaster 因尺寸上下文不同返回 0；通过现有示例的内存协程等待 WaitForEndOfFrame 后，Screen 为 1041×1530，同一按钮中心命中 2 项，首项和点击处理目标均为 Confirm，CanvasGroup 与按钮输入资格有效。不能用工具回调的尺寸上下文断言 Prefab 存在射线遮挡，也不能用该查询代替 Player 物理鼠标验收。随后在渲染结束后执行既有 UIAutomation.CaptureSnapshot/EncodePng，获得 1041×1530、40435 字节的正常画面 `/private/tmp/mui-unity-validation/ValidationCaptures/mui-prefab-rendered-20260928.png`；重复 Dispose 安全，释放后访问 Texture 抛 ObjectDisposedException，后续帧纹理原生对象为空。未新增脚本或测试；截图只证明该调用时机和当前图形环境，不覆盖像素预算异常、其他渲染时机或平台。退出 Play 后 Console 无错误，原场景未变脏。

2026-09-28 通知参数复用与性能复验：ObservableObject 普通及批次通知、uGUI Element 通知统一复用不可变 PropertyChangedEventArgs；缓存最多 256 个普通名称，最长 128 字符，null/empty 分别保持原语义，超容量或超长名称继续正常分配且不永久保留。Unity Mono 的 GC.GetAllocatedBytesForCurrentThread 对显式分配阳性对照也返回 0，因此不采用其结果。改用当前线程 ProfilerRecorder 的 GC.Alloc 标记，预热后 SettingsViewModel.Status 交替修改 10000 次，修复前 10000 个样本、修复后 0 个，显式分配阳性对照仍为 10000 个；通知次数保持 10200。批次顺序、最终值、null/empty、400 个动态名称的容量限制及 129 字符名称均复验通过。这只证明已缓存普通名称的通知路径；安全分发 GetInvocationList、批次作用域/快照和错误列表仍会分配，不表示整个绑定或列表零分配。修改后 39/39 Release 构建零警告、零错误，日志目录 `/private/tmp/mui-notification-build-HXlbqr`；604 个 C# 文件格式、成员布局与大括号检查通过。随后核对全包 746 份元数据的 GUID 格式、唯一性及资产对应关系，Runtime/Editor/Samples 的脚本与 asmdef 元数据无缺失，38 个 asmdef 内部引用无缺失或依赖环。未新增测试或提交。

2026-09-28 当前工作树 macOS IL2CPP 构建与运行：独立 Unity 2022.3.62f3 将 Settings、Navigation、Tabs 4 三个现有场景构建为 `/private/tmp/mui-unity-validation/ValidationBuilds/MUI-AOT-Settings.app`，任务 `build-8ce4d08a67` 成功，203.11 秒、750.59 MB、0 errors、2 warnings，二进制包含 x86_64 与 arm64。独立 Player 首场景 Settings 正常显示字体、滑条、输入框和按钮；日志 `/private/tmp/mui-aot-settings-player.log` 包含 `MUI Settings ready: Volume: 50%`，退出时绑定清理为 Unbound、saveExecuting=False。物理输入工具操作尚未得到可确认的交互结果，不能记为鼠标键盘通过；未运行后续两个场景。两条构建报告警告分别为未绑定 Unity Services cloud project ID，以及 MUI 构建目录/页面/路由均为 0，真实 Prefab 构建校验覆盖仍待补齐。该证据只覆盖当前构建和首场景启动、画面及退出清理，不代表所有泛型、裁剪级别、资源后端、长期内存或其他平台通过。验收后已恢复 Mono2x、Disabled 裁剪；API、后台运行和空构建场景列表保持原值，原 Tabs 4 场景未变脏。未新增测试或提交；完整设计验收仍未闭合。

2026-09-28 验收工程完整性与洋红编辑器修复：本轮图形编辑器日志报告内置 BlitCopy、GUIRoundedRect、EditorUIE 和 Skybox 的 `HLSLSupport.cginc` 无法打开；安装中的文件实际存在，渲染配置为 Built-in/Metal，运行进程的工作目录已经是验收工程。因此此前仅归因工作目录的解释不足。关闭本任务启动的独立编辑器，备份 ShaderCache 与 ShaderCache.db 后，通过 macOS 正常启动入口重新打开 `/private/tmp/mui-unity-validation`；新 Shader 编译器日志包含 `compileSnippet/ok=1`，相关 Shader 消息为空，启动日志无 Shader include 错误。原先四组示例为手工复制，Dialogs/DragDrop 未导入；备份已有副本后，通过 Unity Sample API 将六组示例全部导入标准 `Assets/Samples/MUI/0.1.0` 目录，233 个文件无缺失、源码及 README 与当前仓库一致。uGUI 改为正常 `1.0.0` 包依赖，消除对 fun-slg Library 缓存的引用；补齐已安装 TMP 的 Essential Resources，默认 `LiberationSans SDF` 和 TMP Settings 可读取。当前 Tabs 场景无 Missing Script，Console 无错误，Game 截图 `/private/tmp/mui-unity-validation/ValidationCaptures/mui-tabs-ready-repaired-20260928.png` 显示完整页签与 Inventory 内容，无洋红。该对照同时改变启动入口和缓存，不能将单一因素认定为最终根因；未修改 Unity 安装或其他项目。

2026-09-28 Tab 取消恢复真实 uGUI 复验：在上述独立 Unity 2022.3.62f3 的 Play Mode 中，仅临时调整既有 TabsDemo 内存设置，使用 KeepPrevious/RestorePrevious、1000ms 准备延迟，并临时允许后台运行。Inventory 切至 Quests 准备时，旧 View 同一实例保持可见、alpha=1、标题不变，输入关闭而 TabBar 仍可交互；取消后原请求为 Cancelled，快照恢复 Ready/Selected=Displayed=inventory，同一 View 的输入、CanvasGroup.interactable 和 blocksRaycasts 恢复，活动页面仅 1 个。随后通过原生 EventSystem 的 PointerClick 派发选择 Quests，最终 Ready 且标题为 Quests content；这不代表物理鼠标输入。排空父 Lifetime 后控制器 Inactive、DisplayedTab=null、可见页面 0，示例宿主根 View 的状态仍由示例管理；销毁运行时 Demo 完成 Canvas/Provider 清理，退出 Play 后重载原场景，AutomaticWalkthrough 恢复且场景未变脏。未新增脚本、测试或场景资产，未提交；完整设计验收仍未闭合。

2026-09-28 Tab 取消恢复修复：复现 A 已显示、B 准备中取消后旧实现返回 Cancelled，但快照为 Empty/Selected=B 且 A 的凭证已归还，违反取消恢复设计。现将未提交选择的失败与取消收敛到同一旧页恢复流程；取消恢复使用独立父激活令牌，原目标取消不阻断恢复，后续选择和父关闭仍撤销恢复，延迟提示也由有效恢复令牌驱动。无旧页时 SelectedTab/DisplayedTab 同时清空；恢复失败进入 Error 并保留诊断；恢复成功保留原请求 Cancelled，不发 SelectionFailed。Unity 2022.3.62f3 附带 Mono 的不落盘探针验证原凭证恢复（created=1/released=0/input=True，最终父清理 released=1），以及无旧页、失败恢复通知、恢复失败、新选择 C、父关闭、重复等待取消、恢复延迟提示和失败恢复中取消。恢复失败的父清理仍如实汇总 AggregateException。Tabs 及 uGUI/示例/Editor 依赖 Release 编译零警告、零错误，603 个 C# 文件的格式、成员布局与大括号检查及补丁空白检查通过。独立 Unity 2022.3.62f3 重新编译当前包后，既有 Tabs Play Mode 演示退出码 0，达到清理及预览完成标记，日志 `/private/tmp/mui-tab-cancellation-preview.log`。交互式 MUI 编辑器启动日志停在许可连接，未取得实时连接；未操作其他项目。专项恢复证据是托管协议验证，不证明冻结画面、原生输入或布局。未新增测试或提交，完整设计验收仍未闭合。

2026-09-28 当前工作树全量复验与终态淘汰：38 个离线工程及生成器共 39/39 Release 编译零警告、零错误，使用 Unity 2022.3.62f3 和独立验收项目的匹配程序集，日志目录 `/private/tmp/mui-current-build-W1dTW8`。603 个 C# 文件格式、成员布局与控制流大括号检查通过；719 份元数据和 38 个 asmdef 的 GUID、引用及无环检查通过，补丁空白检查通过。独立 Unity 2022.3.62f3 重新编译当前包并运行既有 Navigation、Settings、Tabs 演示，均退出码 0；Navigation 导出 1063 条记录，进入/实际退出/实例清理/凭证归还分别为 35/1/36/33 对，无重复开始或缺失结束；Settings 到达 Unbound，Tabs 到达清理完成。日志分别为 `/private/tmp/mui-current-lifecycle-preview.log`、`/private/tmp/mui-current-Settings-preview.log`、`/private/tmp/mui-current-Tabs-preview.log`。Unity 附带 Mono 的内存探针另验证终态 400ms TTL 前可查询 Destroyed/AlreadyClosed，450ms 后为 UnknownOrExpired；容量 2 的账本关闭第三个实例后淘汰最旧记录，旧 Handle 的直接结果及前后建立的等待均保留。异步归还超过 150ms TTL 时实例仍为 Closing，物理清理完成后才开始终态计时，随后过期查询明确返回 UnknownOrExpired，业务结果 77 和恰好一次归还均保持。托管探针不落盘；上述批处理使用无图形模式，不证明真实输入或渲染，也不覆盖 AOT 与长期性能。Runtime 职责复核未发现文件、网络、存档、低内存监听或具体资源加载后端的新实现。未新增测试或提交，完整设计验收仍未闭合。

2026-09-28 错误出口重入与 View Lease 故障复验：UIErrors 在同线程分发期间忽略回调中的嵌套报告，并以 finally 恢复报告资格；出口和观察者抛错仍隔离，其他线程的独立报告不受影响。使用 Unity 2022.3.62f3 附带 Mono 的内存探针，递归且抛错的出口与首个观察者连续处理两次外层报告，后续观察者均收到原始错误；四个线程的独立报告均送达。同步导航确认 close/destroy/instance/lease 顺序、重复关闭只归还一次、归还异常返回 Failed、进入缓存不归还而 ClearCache 归还、归还回调重启追踪不污染新会话。异步导航确认关闭钩子与凭证归还分别等待、归还异常只释放一次且阶段保留异常类型、归还等待期间重启追踪隔离旧记录；CloseAsync 等待取消返回 WaitCancelled 后实际清理继续，WaitForCleanupAsync 等待最终完成，归还阶段保持原关闭请求编号。Core 与 Navigation Release 编译及格式检查通过。这些是不落盘的托管状态机与所有权验证，不代替 Prefab、Unity 原生销毁、输入或渲染验收；此前独立 Unity 追踪演示证据见下项。未新增测试或提交。

2026-09-28 View Lease 归还阶段与真实追踪报告：共享实例清理保持先生命周期、再退订、最后归还视图凭证的顺序；Navigation 在同步/异步关闭、失败候选回收和缓存淘汰中分别计时 ViewResourceRelease，未启用追踪时不创建诊断作用域或回调，闭包工厂只在启用检查通过后调用。没有凭证或关闭进入缓存时不伪造归还，缓存清理使用路由键和无效句柄。现有 Navigation 示例增加可选 RecordLifecycleTrace 和独立批处理入口。38/38 个离线工程 Release 编译零警告、零错误；随后仅调整闭包创建位置，Navigation 再编译通过。603 个 C# 文件格式与大括号检查、补丁空白检查通过，719 份 Unity 元数据和 38 个程序集的 GUID/引用/无环检查通过。独立 Unity 2022.3.62f3 的追踪 Play Mode 演示退出码 0，导出 1063 条记录：进入阶段 35 对、实际退出动画 1 对、实例清理 36 对、View Lease 归还 33 对（含 ClearCacheAsync 6 对），均有正常结束，没有重复开始或缺失结束。日志为 `/private/tmp/mui-lifecycle-trace-preview.log`。带异步关闭钩子的页面实例清理约 102 ms，凭证归还约 0.24 ms；这些是编辑器观察值，不是性能基线。纯同步、释放异常、跨会话重启及 Inspector 操作仍待专项验收；该阶段不证明后端卸载、原生销毁或目标设备内存收敛。未新增测试或提交。

2026-09-28 追踪上下文与转场收尾复核：进入转场、实际播放的退出转场和实例清理现有独立的操作阶段记录，关闭请求编号通过异步视觉退出传播到最终清理；缓存维护与预加载显式请求进入各自作用域。异步回调若保留已结束请求的 ExecutionContext，不再把后续关闭归到旧编号；Back 目标解析共享同一完成状态，无全局在途编号集合。项目异步代码在非 UI 线程异常结束时，只结束追踪状态，不伪造 UI 线程记录。38/38 个离线工程 Release 编译通过；602 个 C# 文件的格式与大括号检查、补丁空白检查通过。独立 `/private/tmp/mui-unity-validation` 的 Unity 2022.3.62f3 Navigation Play Mode 演示出现 `MUI Navigation shutdown complete` 与预览完成标记，批处理退出码 0。该演示没有启动追踪并采集阶段报告，也未注入转场异常；单独的资源凭证释放耗时、目标平台 AOT 与长期性能仍待验证。沙盒内 Unity 授权 IPC 超时，非沙盒批处理通过；本次后续增量未新增测试或再次提交。

2026-09-28 诊断报告上限复核：Back/BackAsync 补目标路由键时改用统一截断入口；射线命中路径的单层名称包含省略号后不超过 128 字符，超出 64 层时用保留的最高一层作为省略位；资源准备标签的 256 字符裁剪不拆开代理项；构建校验单条问题包含省略号后不超过 2048 字符。Navigation、UGUI、Editor 对应 Release 离线编译零警告、零错误；601 个 C# 文件格式与大括号检查通过。专用 Unity 2022.3.62f3 Navigation Play Mode 演示退出码 0，输出导航清理完成标记；尚未在 Unity 中构造长名称与超深层级场景。关闭操作编号向动画与最终释放传播仍未实现。未新增测试或提交。

2026-09-28 候选准备诊断分段：在共享生命周期执行器中为 Presenter.Create、绑定工厂与 Bind、Presenter.Open、可选 OnOpenAsync 增加嵌套阶段记录；仅导航候选传入追踪回调，ChildView 共用执行器不承担导航请求身份。阶段异常或取消记为未完成，追踪关闭时不创建阶段作用域；Presenter 工厂仍由外层模型创建阶段计时。此改动后 38/38 个离线工程及生成器 Release 编译零警告、零错误，601 个 C# 文件格式与大括号检查、元数据检查及补丁空白检查通过。专用 Unity 2022.3.62f3 项目的 Navigation、Settings、Tabs Play Mode 演示均退出码 0，分别到达导航退出、绑定 Unbound 与 Tab 清理完成标记。阶段时间线本身尚未专项运行采集；转场与资源释放仍未接入请求链路。首次沙盒运行因 LicensingClient 无法连接退出码 199，在非沙盒环境中重试通过。未新增测试或提交。

2026-09-28 诊断字段长度复核：生命周期与请求追踪的路由键、依赖键和异常类型名现共用包含省略标记的 256 字符上限；截断时避免拆开 UTF-16 代理项。修复前长键的截断结果会达到 257 字符。`MUI.Navigation` Release 编译零警告、零错误，600 个 C# 文件格式和大括号检查通过；专用 Unity 2022.3.62f3 项目重新导入并编译 Navigation 程序集成功。此项为边界契约修正，未在运行中注入长键；完整请求追踪中的绑定、Presenter、动画和资源释放分段仍未接通。MUI 验收编辑器当前未连接，Unity MCP 只显示 `fun-slg`，未向该业务项目发送探针。未新增测试或提交。

2026-09-28 跨模块职责与当前代码复核：Runtime 的 Core/Navigation/Resources/ChildViews/Modules 无 Unity 引擎或 Editor 引用；Runtime 未发现文件、网络、存档、具体资源后端或低内存监听调用。修正无 UIHost 时 View/Element 错误只有 `Reported` 观察者时绕过 Unity 回退、没有观察者时绕过统一分发的问题；局部 Unity 默认出口现在与项目 `Sink`、宿主默认出口和观察者共用 `UIErrors.Report`。修正后 38/38 个离线工程 Release 编译通过，生成器编译通过，600 个 C# 文件格式和大括号检查、元数据检查及补丁空白检查通过。专用 Unity 2022.3.62f3 项目重新导入成功并编译 `MUI.UGUI.Automation`；既有 Navigation、Settings、Tabs Play Mode 批处理均退出码 0，分别到达导航退出、绑定 Unbound 和 Tab 清理完成标记。受限环境中的首次 Unity 批处理因 LicensingClient IPC 超时退出码 199；非沙盒重试成功。无宿主日志组合与终态 TTL 边界未做专项运行验证；这些演示不覆盖目标平台、IL2CPP、长期性能或第 19/24 章全部故障组合。未新增测试或提交。

2026-09-28 终态账本 TTL：Navigator 的已关闭记录现同时按容量和单调时钟淘汰，默认上限 256 条、10 分钟；异步和纯同步入口及 UIHost 均可配置。旧 Handle 在 TTL 或容量淘汰后返回 UnknownOrExpired，已有 Handle 的独立结果完成源不受影响；查询时惰性淘汰，宿主帧每秒维护，不启动后台任务。Navigation 与 UGUI 离线 Release 编译零警告、零错误，600 个 C# 文件格式与大括号检查通过；尚未在 Unity 中验收时间跨越、边界查询和结果等待，未新增测试或提交。

2026-09-28 显式 UI 缓存与闲置内容清理请求追踪：`RefreshCache`、`ClearCache`、`InvalidateCache`、`ClearInactiveContent` 的同步及适用的异步入口沿用 Navigator 现有 OperationId 时间线；追踪关闭时异步入口直接返回原操作，定时扫描和组合清理不重复记账。Navigation、UGUI 离线 Release 编译零警告、零错误；599 个 C# 文件格式与大括号检查、Unity `.meta` 检查及补丁空白检查通过。尚未在 Unity 运行中检查追踪先后顺序和异步重入；本轮 Unity CLI 未连接到验收实例，桌面 Unity 停在软件条款页面，未代用户接受条款。未新增测试或提交。

2026-09-28 回收列表异步迟到资源专项复验：仅在独立 `mui-unity-validation@8bc64b8a` 的 Play Mode 内存中复用现有奖励列表布局和示例加载器，子 View 开启 `WaitForResourceSources`，后端加载忽略取消。先确认旧列表刷新尚未完成且加载器有 1 个在途请求，再在同一编辑器回调内结束旧激活；迟到凭证创建 1、归还 1，旧 `PendingChange` 为 `Canceled`。旧激活物理清理完成后复用同一 View 和列表，新激活只显示 `New activation`，列表 `Error=none`、物化节点 1、加载器在途 0。随后排空新激活并清理临时对象，退出 Play 后原 `ResourceBinding 1` 场景未变脏。该探针覆盖取消后后端迟到返回与再次激活，不证明旧刷新完成回调在新激活开始后才执行；目标设备和长期性能仍待验收。未新增测试或提交。

2026-09-28 回收列表刷新归属补强：`Reconcile` 在条目模型赋值后复核父激活与来源版本，再交出本轮捕获的子视图准备信号；旧协调过程不再读取新激活的准备任务或错误状态。同步路径在父激活已结束时不把预期取消写入列表 `Error`。`MUI.UGUI` Release 编译零警告、零错误，597 个 C# 文件的成员布局、空白和控制流大括号检查通过。独立 Unity 2022.3.62f3 验收实例完成脚本编译与域重载；现有同步回收列表示例新增后物化 4 个节点、删除首项后保留 4 个池节点且无错误。仅在 Play Mode 内存中注入条目 `ViewModel` 通知回调取消父激活，得到 `callbacks=1; ended=True; cells=3; error=none; call=none`。已退出 Play，`ResourceBinding 1` 场景未变脏；真实异步资源迟到后跨激活的完整运行路径仍待专项验收，未新增测试或提交。

2026-09-28 项目日志出口接入：`UIErrors.Sink` 提供进程级 `Action<Exception>`，Core 在隔离接收器异常后继续通知原有 `Reported` 观察者；uGUI 将 UIHost、View 与 Element 的硬编码异常日志统一收敛到默认 Unity 适配器，多个 UIHost 仅注册一次。项目配置出口后默认适配器不再强制写 Unity Console；未配置时保持原生错误可见。`MUI.Core` 与 `MUI.UGUI` Release 编译零警告、零错误，597 个 C# 文件格式与成员布局检查通过。独立 Unity 2022.3.62f3 完成脚本编译及域重载后，以两台临时 UIHost 验证：项目出口收到 1 次，Console 无该错误；恢复默认出口后 Console 仅记录 1 次。第二轮探针确认项目出口与 `Reported` 各收到 1 次，`Reset()` 清除项目出口后 Unity 回退仍保留；Console 中项目出口错误为 0 条、重置后的回退错误恰好 1 条。探针对象只存在于 Play Mode 内存，已退出 Play 并恢复未变脏的 `ResourceBinding 1` 场景；Editor 工具和 Samples 的演示日志仍由各自代码输出，项目业务日志不归 UI 框架托管。未新增测试或提交。

2026-09-28 固定高度虚拟列表追加成本与键回调复核：稳定的 `string`、`int`、`long`、`Guid` 键先校验新增键，完成来源版本与模板核对后原地扩展索引，避免每次单项追加复制全部旧键；自定义键继续在隔离候选索引中执行哈希/相等回调，并在每次回调后核对来源与激活。`MUI.UGUI` Release 编译零警告、零错误，596 个 C# 文件的成员布局与格式检查以及补丁空白检查通过。独立 Unity 2022.3.62f3 重新导入后，现有 Navigation Play Mode 演示输出 `MUI Navigation shutdown complete`。不初始化渲染层的内存探针连续追加 10000 个整数键，来源、快照与索引数量均为 10000；四段各 2500 项耗时约 27/35/46/57 ms，只能作为编辑器内此路径的观察值，不能推断完整渲染帧或真机吞吐。另用自定义键哈希回调在候选索引构建中同步结束父 Lifetime，得到旧快照/索引均保留 1 项、没有半提交；回调同步换源后，新来源快照/索引均为 1 项且键为 99，没有被旧追加覆盖。所有探针只存在于 Play Mode 内存中；已退出 Play 并恢复未变脏的 `ResourceBinding 1` 场景。尚需目标设备性能、跨激活异步迟到任务及长期内存验收；未新增测试或提交。

2026-09-28 当前工作树纯同步 Tab 运行复验：仅在独立 `mui-unity-validation@8bc64b8a` 的 Play Mode 内存中接线既有 `SynchronousTabsDemo`，内容区与 TabBar 为兄弟区域且 TabBar 位于后序；未创建脚本、测试或场景资产。inventory/quests 往返切换均为 Ready，缓存复用时创建模型数保持 2；离开守卫返回 Rejected/Denied，仍显示 inventory。故意让 quests 准备失败返回 Failed，原页保持显示；取消故障后重试 Ready。移除当前 quests 定义后切回 inventory，恢复定义成功。结束父 Lifetime 后 `IsEnded=True`、`DisplayedTab=null`、缓存数 0、父 View 输入关闭。已销毁临时对象并退出 Play，原 `ResourceBinding 1` 场景未变脏。该证据不覆盖物理输入、异步 Tab 竞态或所有回调重入；未新增测试或提交。

2026-09-28 当前工作树纯同步链路运行复验：仅在独立 `mui-unity-validation@8bc64b8a` 的 Play Mode 内存中接线既有 `SynchronousNavigationDemo` 与 `SynchronousVirtualListDemo`，不创建脚本、测试或场景资产。同步导航以 `Navigator.Mode=Synchronous` 打开页面并发布 Ready；参数更新 Applied，故意提交失败返回 CommitFailed 且 RecoveryFailed=False，换绑 Applied；替换准备失败保留原页 Open，正常替换 Committed 且旧页清理 Complete。关闭后缓存数 1，缓存重开后创建次数不增加；两个批次页同步关闭 Completed，预加载占用 1 清理后为 0；原生 Button 事件完成结果 `Completed(42)`、清理 Complete，显式宿主 Shutdown 成功。同步虚拟列表初始 1000 项物化 8 个节点，追加后 `Ready/1001`，同步定位第 500 项 Ready，三列物化 27 个；移除首项、切回单列仍 Ready 且 Error 为空。销毁示例后列表 Inactive、来源引用为空，随后父 Canvas 与示例对象全部销毁。两次 Play 结束后 `ResourceBinding 1` 场景未变脏。此证据不覆盖同步 Tab 当前工作树、资源后端失败、物理输入、IL2CPP 或全部生命周期交错；未提交。

2026-09-28 虚拟列表追加路径性能复核：增量追加在复制候选键索引前先识别测量高度、已有变高行索引或新增显式高度，直接交给完整快照路径，避免注定回退时白做整份索引复制。固定高度追加仍沿用候选索引的原子提交。`MUI.UGUI` 离线 Release 编译零警告、零错误，596 个 C# 文件格式与大括号检查及补丁空白检查通过。独立 `mui-unity-validation@8bc64b8a` 刷新脚本后运行既有 Navigation Play Mode 演示，分页重试、窗口 Append/Prepend 均正常，最终输出 `MUI Navigation shutdown complete`；退出后恢复未变脏的 `ResourceBinding 1` 场景。当前 Runtime 抽查未发现文件、网络、存档、低内存监听或具体资源后端调用，通知、Loading、主题和本地化模块仍只管理 UI 表现状态；这不是全部模块的最终职责验收。固定高度单项高频追加成本、自定义键重入、跨激活异步迟到任务及目标平台仍待验证；未新增测试或提交。

2026-09-28 虚拟列表尾部追加索引提交修复：增量追加先构建包含新键的候选索引，每次可能执行项目键比较后复核来源和激活；快照与索引在最终版本核对后一起提交，避免旧回调在部分写入期间关闭父界面或换源。此路径需要复制当前键索引，单项高频追加的成本尚待性能测量。`MUI.UGUI` 离线 Release 编译零警告、零错误，596 个 C# 文件格式与大括号检查、补丁空白检查通过。仅在 `mui-unity-validation@8bc64b8a` 重新导入并运行既有 Navigation 演示：列表锚点保持、分页加载和窗口 Append/Prepend 日志正常，最终输出 `MUI Navigation shutdown complete`；退出 Play 后恢复未变脏的 `ResourceBinding 1` 场景。自定义键在候选索引构建时同步换源、单项高频追加性能及目标平台仍待专项验收；未新增测试或提交。

2026-09-28 虚拟列表测量缓存专项运行复验：仅在独立 `mui-unity-validation@8bc64b8a` 中临时开启既有 Navigation 动态高度演示，不保存场景或新增测试。2000 项列表的首次定位、缩窄视口、字体失效重测和同实例内容更新重测均为 `Ready`，可见单元保持 4 到 5 个。Play Mode 内存探针随后对活动列表通知屏外项更新，已测量实例缓存保留；通知已测量项更新，旧缓存立即失效（原 4 条变为 3 条）；用同键新实例替换另一已测量项后，旧实例缓存移除、新实例尚无缓存且快照已指向新实例。动态高度预览分支按示例设计提前返回，不输出完整 Navigation shutdown 日志。退出 Play 后丢弃临时启用的预览设置并恢复未变脏的 `ResourceBinding 1` 场景。自定义键在候选索引构建时重入、跨激活异步迟到任务、目标平台和长期性能仍未验收；未提交。

2026-09-28 虚拟列表测量缓存提交复核：缓存改按 `VirtualListItem` 实例索引，读取高度和批次测量不再执行项目稳定键的比较逻辑；集合变更路径仅准备候选缓存，在自定义键索引构建及归属检查通过后与快照一起提交，避免回调重入时提前发布缓存。`MUI.UGUI` 使用 Unity 2022.3.62f3 对应程序集离线 Release 编译零警告、零错误，596 个 C# 文件格式与大括号检查及补丁空白检查通过。独立 `mui-unity-validation@8bc64b8a` 刷新脚本后，既有 Navigation Play Mode 演示输出 `MUI Navigation shutdown complete`；结束后恢复未变脏的 `ResourceBinding 1` 场景。此运行验证主流程，尚未专项覆盖同实例测量保留、新实例失效、测量期间自定义键重入、跨激活异步迟到任务和目标平台行为；未新增测试或提交。

2026-09-28 虚拟列表选择与定位键回调边界：选择键校验、选择状态和样式、点击选择、同步/异步定位入口及定位结果查找在自定义键比较后复核激活与代际；锚点搜索改为逐项比较并在回调后复核来源。定位不再用 `List.Find` 在可能清空的单元池上继续搜索。`MUI.UGUI` 离线 Release 编译零警告、零错误，596 个 C# 文件格式与大括号、补丁空白检查通过。独立 Unity 2022.3.62f3 Play Mode 内存探针：不同对象的同值键在 `CompleteReveal` 中同步释放列表，`before=1; callbacks=1; inCompleteReveal=True; outcome=Inactive; failure=none; cleanup=none`；另一探针在 `SetSelection` 中同步释放，`before=1; callbacks=1; inSetSelection=True; failure=none; cleanup=none`。既有 Navigation 演示输出 `MUI Navigation shutdown complete`；退出后恢复未变脏的 `ResourceBinding 1` 场景。探针不落盘、不新增测试；异步迟到任务、测量键查找及目标平台行为仍待专项验收，未提交。

2026-09-28 虚拟列表自定义键比较父关闭专项运行验收：仅在独立 `mui-unity-validation` 的 Play Mode 内存中构造同步列表和自定义键，不创建脚本或场景资产。先物化 2 个单元，再换来源；键的 `Equals` 仅在 `FindReusableCell` 调用栈中同步释放列表。结果为 `before=2; callbacks=1; inFindReusableCell=True; failure=none; cleanup=none`，旧刷新没有因节点池清空抛出集合修改或越界异常。退出 Play Mode 后原 `ResourceBinding 1` 场景未变脏。该探针只覆盖同步键比较引发的父级释放；跨激活迟到异步条目、原生赋值回调及目标平台行为仍待验收，未新增测试或提交。

2026-09-28 虚拟列表子视图换绑重入复核：在回收旧单元和绑定新单元的 `NestedViewElement.ViewModel` 赋值后，立即核对来源版本与父激活，再读取子视图准备信号；赋值回调若关闭父界面或换来源，旧刷新直接退出，不再触碰已释放或已复用节点。`MUI.UGUI` 离线 Release 编译零警告、零错误，596 个 C# 文件格式与大括号检查及补丁空白检查通过。独立 Unity 2022.3.62f3 验收项目刷新脚本后运行既有 Navigation 演示，输出 `MUI Navigation shutdown complete`；退出 Play Mode 后恢复未变脏的 `ResourceBinding 1` 场景。此次运行只证明现有正常流程未回归，赋值回调同步关闭和跨激活迟到任务仍需专项运行验收；未新增测试或提交。

2026-09-28 虚拟列表父激活与键比较重入复核：节点复用查找改为索引遍历，在自定义键比较后立即核对刷新归属，避免比较回调关闭父界面并清空节点池时由旧迭代器抛出集合修改异常。父激活先完成来源、节点和几何重置，再通知 `Empty`；重置期间拒绝外部修改，并在可触发回调的步骤后核对激活身份，避免 `Empty` 观察者设置的新来源被后续重置覆盖。Unity 2022.3.62f3 的 Navigation 既有 Play Mode 演示输出 `MUI Navigation shutdown complete`；编辑器内存探针在 `Empty` 观察者设置来源后得到 `sourceRetained=True`，没有创建脚本或场景资产。退出 Play Mode 后恢复未变脏的 `ResourceBinding 1` 场景。当前 38 个离线构建项目和 1 个生成器项目共 39/39 Release 编译零警告、零错误；596 个 C# 文件格式与大括号检查、补丁空白和 Unity 元数据缺失/孤立/重复 GUID 检查通过。自定义键比较实际触发父关闭、跨激活迟到任务及目标平台行为尚未完成专项运行验收；未新增测试或提交。

2026-09-28 自动化程序集职责拆分：将 `UIAutomation` 会话、条件轮询、截图快照和追踪导出移入可选 `MUI.UGUI.Automation`，保留原 `MUI.UGUI` 命名空间、公开方法及已有 Unity `.meta` GUID；基础 `MUI.UGUI` 仅保留输入适配契约和结果枚举，不再直接引用 ScreenCapture/ImageConversion 程序集。新增显式 asmdef、离线构建工程和受限的程序集内部访问声明；UPM 包仍声明截图模块依赖，以便可选程序集编译。使用自动化会话的项目 asmdef 需新增 `MUI.UGUI.Automation` 引用。`MUI.UGUI.Automation` 连同依赖离线 Release 编译零警告、零错误，596 个 C# 文件的成员布局、空白与大括号检查通过；Unity 2022.3.62f3 导入后生成新程序集，刷新后的 Console 无错误，反射确认原公开方法仍存在且所属程序集正确。未运行截图时机、自动化输入或目标平台验证，未新增测试或提交。

2026-09-28 当前 Unity 编译与既有导航演示复验：仅向 `/private/tmp/mui-unity-validation` 补齐此前缺失的 Navigation、Settings、Tabs 和 ResourceIntegration 示例文件，保留已有文件与 `.meta`；Unity 2022.3.62f3 重新导入后四个示例程序集均已生成，清空历史 Console 并再次刷新后无编译错误或警告。既有 Navigation 场景在 Play Mode 输出虚拟列表定位、复用、失败重试和观察者换源结果，最终输出 `MUI Navigation shutdown complete`；MCP 标作 Exception 的示例生命周期记录实际来自 `Debug.Log`。退出后恢复未变脏的 `ResourceBinding 1` 场景，MUI 与 fun-slg 编辑器均仍运行。`MUI.UGUI` 离线 Release 编译零警告、零错误；594 个 C# 文件的成员布局、空白与控制流大括号检查通过。本轮静态抽查未发现 Runtime 直接读写文件、网络、存档或调用具体资源后端；Editor 的文件读取用于页面向导与构建目录校验，`JSONSerialize` 仍是页面向导实际依赖。既有演示不覆盖旧激活迟到条目、键比较重入和所有故障组合，不能据此认定完整设计验收完成；未新增测试或提交。

2026-09-28 两类列表刷新归属复核：`VirtualListElement.RefreshCells` 旧迭代器在异步条目准备后若晚于父激活重新开始，只使旧激活的范围失效，不再清空新激活的 `first/last`；构建期稳定键比较、池节点复用查找和选择比较返回后复核刷新归属。`RecyclingListElement` 同步协调的异常与 finally 收尾只写所属激活的 Error/running。`MUI.UGUI` 离线 Release 编译零警告、零错误，594 个 C# 文件格式与大括号检查、补丁空白检查通过。MUI 验收实例加载现有 Navigation 场景并进入 Play Mode 后自行退出，未产生演示完成日志；临时项目 `Assets/Navigation` 缺少该场景引用的 `NavigationDemo.cs`，`Library/ScriptAssemblies` 也没有 `MUI.Samples.Navigation.dll`，本次不能作为运行通过证据。已恢复未变脏的 `ResourceBinding 1` 场景；迟到的真实异步条目及键比较重入仍未完成 Unity 运行验收，未新增测试或提交。

2026-09-28 虚拟列表重试失败归属修复：`RetryAsync` 在读取或校验来源同步失败时，将本次故障任务发布给 `PendingChange`，避免外部观察者误读上一次成功任务；异步与纯同步 Retry 在读取自定义来源后均复核激活、来源赋值、快照代际和来源版本，旧重试不提交快照。`MUI.UGUI` 使用 Unity 2022.3.62f3 对应程序集离线 Release 编译零警告、零错误，594 个 C# 文件格式与大括号检查以及补丁空白检查通过。验收实例仍为 `/private/tmp/mui-unity-validation`，但 MCP 内存探针在代码编译阶段因引用多份当前不存在的 `ScriptAssemblies` 而失败，未执行运行验证；未操作其他 Unity 编辑器、未新增测试或提交。

2026-09-28 虚拟列表持续重入与格式门禁复验：仅在 `mui-unity-validation@8bc64b8a` 的编辑器内存中创建空列表，`Empty` 状态观察者反复切换来源；第 6 次换源后刷新按配置预算停止，`Status=Error`、`PendingChange=Faulted`，错误为 `Virtual list refresh did not stabilize within its callback budget.`。探针在 finally 释放 Lifetime 与临时原生对象，原 `ResourceBinding 1` 场景仍未变脏；未创建脚本、测试或场景资产。格式工具的检查模式改由已有 Roslyn 语法工具直接校验控制流大括号，不再因逐工程 `dotnet format style` 的 MSBuild BuildHost 连接超时而漏检；连续 using 共用最终代码块。`python3 Tools~/format-code.py --check --braces` 已覆盖 594 个 C# 文件，成员布局、空白和大括号违规均为 0。此探针只覆盖空列表上的持续状态重入；跨激活迟到异步条目、目标平台与长期性能仍待验收。

2026-09-25 虚拟列表当前代码运行复验：`mui-unity-validation@8bc64b8a`（`/private/tmp/mui-unity-validation`，Unity 2022.3.62f3）完成脚本刷新并运行既有 Navigation 场景；虚拟列表 Loading 观察者拿到当前未完成任务，Ready 回调换源后得到 `final=Empty; cells=0`，VM 绑定层 Ready 回调换源后得到 `vmCount=0; sameSource=True; status=Empty; cells=0`，演示输出 `MUI Navigation shutdown complete`。退出 Play Mode 后恢复未变脏的 `ResourceBinding 1` 场景，MUI 与 fun-slg 编辑器均仍运行。`MUI.UGUI` 离线 Release 编译零警告、零错误，594 个 C# 文件基础格式检查与补丁空白检查通过；定向 IDE0011 检查仍在 Roslyn/MSBuild BuildHost 连接处超时。此运行只覆盖正常重入，不证明持续重入达到预算上限、旧激活迟到收尾或目标平台行为；未新增测试或场景资产。

2026-09-24 虚拟列表跨激活刷新收尾：异步刷新捕获所属 Lifetime 与已发布任务，状态通知、循环继续、错误及 finally 写入均核对归属；同步刷新在状态回调后复核激活，父激活结束取消已发布的同步兼容完成信号并清除运行标记。异步刷新也限制单次收敛轮数，避免状态观察者持续改写来源时占满 UI 线程。`MUI.UGUI` 使用 Unity 2022.3.62f3 对应程序集离线 Release 编译零警告、零错误，补丁空白检查通过。全仓格式工具的成员布局和空白阶段通过，逐工程 IDE0011 阶段在 Roslyn/MSBuild BuildHost 连接处超时，未得到完整大括号检查结果；本次未操作 Unity Editor，跨激活运行交错与持续重入仍待验收。

2026-09-24 普通回收列表异步子视图准备修复与运行复验：真实异步演示发现 `NestedViewElement.ChangeAsync` 和失败恢复分支仍调用同步 `ChildViewScope.Prepare`，当子 View 等待异步资源时直接失败；现统一使用 `PrepareAsync`，等待返回后复核令牌、元素、请求代际和所属 Scope，再提交或清理候选。`MUI.UGUI` 使用 Unity 2022.3.62f3 程序集离线 Release 编译零警告、零错误，594 个 C# 文件格式检查通过。仅在 `mui-unity-validation@8bc64b8a` 的 Play Mode 中以既有回收列表层级和异步示例加载器创建不落盘对象，子 View 开启 `WaitForResourceSources`、加载延迟 500 毫秒且忽略取消：初始两项等待后 Ready，2 个节点/4 份在用凭证；新增第三项仍在途时移除首项，最终显示 `Async 2` 与 `Async 3`、第三节点隐藏留池、PendingChange 完成且 Error 为空，凭证创建 10、归还 6、在用 4；再新增一项期间关闭，最终创建 12、归还 12、在途与持有均为 0，关闭任务正常完成。修复前相同异步准备触发 `Required child view preparation needs the asynchronous entry point`，旧显示仍保持，关闭后 6 份凭证全部归还。退出 Play Mode 后原场景未变脏，MUI 与 fun-slg 编辑器仍运行。未新增测试或场景资产；仍需验收资源/绑定失败恢复、缓存复开、外部来源重入及长期性能。

2026-09-24 普通回收列表跨激活异步收尾修复：父激活结束或控件释放时重置刷新运行标记；旧刷新任务完成后仅在 Lifetime 和 PendingChange 仍对应本轮时写入 Error、结束运行，避免下一次激活因旧任务迟到而停在脏状态。`MUI.UGUI` 使用 Unity 2022.3.62f3 程序集离线 Release 编译零警告、零错误，594 个 C# 文件格式检查及补丁空白检查通过。仅在 `mui-unity-validation@8bc64b8a` 编辑模式执行不落盘内存探针：旧任务失败完成后，新激活的运行标记与待处理任务保留、Error 仍为空；场景未变脏，两个 Unity 编辑器仍运行。探针通过反射注入状态，只验证跨激活收尾保护，不代表异步条目真实加载、回收与凭证释放已运行验收。

2026-09-24 帧驱动快照复用：`Navigator.PumpChildRequests` 改用独立的可复用页面快照；`ChildViewScope.Pump` 的同步关闭请求与子请求、`RefreshTicks` 的句柄扫描分别改用可复用列表。回调仍按入口快照迭代，重入与归属复核保持原有规则；三处不再在每次派发时创建 List/数组。Navigation 与 ChildViews 离线 Release 编译各零警告、零错误，594 个 C# 文件格式检查和补丁空白检查通过。仅在 `mui-unity-validation@8bc64b8a` 请求脚本刷新，编辑器恢复空闲，未见新增 C# 编译错误；`fun-slg` 编辑器仍运行。尚未测量整帧 GC、CPU 或长期性能，不能据此宣称整个帧驱动零分配。

2026-09-24 异步资源显示换键与在途关闭运行复验：仅在 `mui-unity-validation@8bc64b8a` 的既有 `ResourceBinding 1` 场景内存中临时启用异步模式，加载器延迟 500 毫秒并故意忽略取消。连续两次换键后，6 个被取代的请求产生的凭证均已归还，最终三个显示键均为 `Warm`，创建 9、归还 6、持有 3。在新一轮 3 个请求在途时关闭，关闭任务正常完成，最终创建 12、归还 12、持有 0、在途 0。退出 Play Mode 后从磁盘重载场景，`synchronous=true`、场景未变脏；MUI 与 fun-slg 两个编辑器仍运行。Console 中唯一被 MCP 标为 `Exception` 的示例记录，调用栈指向 `ResourceImageDemo.FinishClose` 的 `Debug.Log` 清理统计，不是抛出的异常。此复验覆盖正常换键及忽略取消的迟到凭证归还，不覆盖加载失败、原生赋值故障或目标平台运行。

2026-09-24 资源显示链路与代码规范复核：检查 ResourceSlot、ElementResourceOwner、ContentViewProvider、生命周期预加载和导航非活动内容清理；当前实现只在 UI 显示位置或界面实例边界请求、持有并归还项目提供的凭证，未发现共享加载合并、资源预算、后端缓存、平台低内存监听或文件/网络持久化进入 Runtime。资源加载策略和物理卸载仍由项目提供方负责。全仓执行 `python3 Tools~/format-code.py --check --braces`，594 个 C# 文件成员布局/空白检查及全部离线工程的大括号规则检查返回成功；Roslyn 工程加载报告警告，尚未据此证明全部工程编译或功能行为。`git diff --check` 通过，未新增测试或提交。此源码与规则复核不能替代异步资源取消、迟到凭证归还、原生赋值故障和目标平台运行验收。

2026-09-24 普通回收列表同步示例运行复验：仅在 `mui-unity-validation@8bc64b8a` 加载既有 `RecyclingList` 场景并进入 Play Mode，使用 `SynchronousRecyclingListDemo` 的真实生成绑定和同步资源加载器。初始 3 项/3 个物化节点、6 份资源凭证；新增后 4 项/4 节点/8 份在用凭证；移除首项后 3 项/4 个保留池节点/6 份在用凭证；清空及恢复首项字体后分别为 5/6 份在用凭证。关闭时凭证创建 15、归还 15、仍持有 0，列表 Error 为空。退出 Play Mode 并恢复未变脏的 `ResourceBinding 1` 场景，Console 无错误，MUI 与 fun-slg 编辑器均仍运行。未新增测试或场景资产；此结果只覆盖同步正常增删与资源清理，不证明异步条目回收、缓存复开、原生指针输入或长期性能。

2026-09-24 来源适配器换绑收敛与职责抽查：Loading/Notifications 两个 uGUI 适配器在候选快照读取、旧来源退订和新来源订阅期间校验赋值代际；订阅访问器同步通知时保留较新显示，抛错时失效监听并尽力撤销，当前绑定失效后隐藏提示，清理错误与原错误一并报告。Runtime 抽查未发现直接资源后端、文件/网络持久化或低内存系统监听；拖放提交仍由项目委托负责，通知和偏好保持 UI 表现状态职责。两个受影响的 uGUI 模块使用 Unity 2022.3.62f3 对应程序集离线 Release 编译均为零警告、零错误，594 个 C# 文件格式检查及补丁空白检查通过。仅在 `mui-unity-validation@8bc64b8a` 的编辑模式执行内存探针：两个适配器各自验证正常绑定、事件 `add` 抛错后撤销及隐藏、订阅时立即通知优先、事件 `remove` 抛错后隐藏、退订时同步换源保留后发绑定，十项结果均为 True。探针不创建脚本、测试或场景资产；原场景未变脏，Console 无错误，MUI 与 fun-slg 编辑器仍运行。原生渲染故障、订阅期间直接换源及多重清理失败组合尚未运行验收；这次抽查不代表全部模块职责边界已完成逐项证明。

2026-09-24 借用来源与列表回调边界复核：Loading/Notifications 两个 uGUI 适配器在来源退订抛错时仍分别尝试解除引用、移除通知按钮监听及隐藏提示，清理错误继续向 Element 释放结果传播。普通回收列表与虚拟列表在集合通知期间核对来源、订阅、激活及版本代际，防止读取项目集合时换源或嵌套通知后提交旧快照、覆盖新来源的错误状态；虚拟列表提交后的本次布局错误仍归当前操作。相关三个 uGUI 离线模块 Release 编译零警告、零错误，594 个 C# 文件格式检查和补丁空白检查通过。仅在 `mui-unity-validation@8bc64b8a` 的既有 SynchronousVirtualList 场景运行正常来源路径：1000 项初始物化 8 个，切三列物化 24 个，定位第 500 项保持 Ready，移除首项后 999 项、物化 27 个且 Error 为空。退出 Play Mode 后恢复未变脏的 `ResourceBinding 1` 场景，MUI 与 fun-slg 编辑器均仍运行。自定义来源 getter/枚举器重入、退订抛错及原生渲染异常分支仍缺针对性运行验收；未新增测试文件。

2026-09-24 异步 Tab 缓存故障收尾：`InvalidateCacheAsync` 在逐项淘汰以外发生异常时，现在保留清理诊断、完成显式 `ClearCacheAsync` 等待者的失败结果，并继续释放维护占位，使最终销毁仍可逐项尝试缓存清理。MUI.Tabs Release 编译零警告、零错误，594 个 C# 文件格式检查通过。仅在 `mui-unity-validation@8bc64b8a` 的编辑模式执行不落盘的反射故障探针：注入缓存目录读取异常后，清理等待任务立即完成且为 Faulted，最终生命周期清理报告保留的 AggregateException；未创建脚本、测试或场景资产。原 `ResourceBinding 1` 场景未变脏，MUI 与 fun-slg 编辑器均仍运行。此探针验证异常完成信号，不覆盖真实缓存实例释放、取消交错或原生 UI 交互。

2026-09-24 提交后纯同步现有示例复验：仅控制 `mui-unity-validation@8bc64b8a`（`/private/tmp/mui-unity-validation`，Unity 2022.3.62f3）。SynchronousNavigation 场景中，打开 Ready、参数更新 Applied、注入的参数提交失败为 CommitFailed 且清理 Complete/恢复失败 False；替换候选准备失败时旧页仍 Open，随后替换 Committed、换绑 Applied、关闭 Closed、缓存重开 Succeeded、预加载占用 1 后清除为 0、批量关闭全部完成。SynchronousTabs 场景中，背包与任务切换复用缓存；注入任务页准备失败后显示仍为 inventory，恢复任务页后 Ready，移除任务定义后回到 inventory。SynchronousVirtualList 场景中，1000 项初始物化 8 个，切为三列物化 24 个，定位第 500 项返回 Ready，移除首项后物化 27 个，仍受视口约束。三条链路均为已有场景与运行时入口，没有新增测试、脚本或场景资产；故意注入的异常在 Console 中作为预期诊断。退出 Play Mode 后恢复未修改的 `ResourceBinding 1` 场景，复查 MUI 与 fun-slg 两个编辑器均运行。此证据不覆盖纯同步所有故障组合、原生输入、帧分配、目标平台或长期内存；完整目标仍未完成。

2026-09-24 拖放线程边界编辑器内存复验：只定位并刷新 `mui-unity-validation@8bc64b8a`（`/private/tmp/mui-unity-validation`，Unity 2022.3.62f3），未创建脚本、测试或场景资产。CodeDom 内存探针中，后台取消后由 UI 线程 Pump 收尾得到 `Cancelled`、视觉回调 1 次、所属 Lifetime 清理完成；业务提交在后台完成并配置非 UI 线程同步上下文后，UI 线程 Pump 得到 `Committed`、视觉回调 1 次、源和目标 Lifetime 清理完成。探针没有单独记录派发回调的线程 ID。原 `ResourceBinding 1` 场景文件未改。探针后 MCP 实例索引间歇短暂缺少 MUI，随后列出 MUI 与 fun-slg，但针对 MUI 的项目信息路由仍报实例缺失，因此停止控制；不能据此宣称最终编辑器状态或原生拖拽输入已验收。未操作 fun-slg，未新增测试文件、未提交。

2026-09-24 拖放线程边界复核：异步拖放会话的后台取消即使 UI 上下文投递失败或回调落在错误线程，也会留下待处理标记；uGUI 帧驱动的 Pump 和所属线程显式 Cancel 可结束尚未提交的会话。异步提交在其他线程完成时先暂存结果，再请求 UI 线程发布；派发故障交由 Pump 收尾，会话销毁先等待提交函数退出再排空结果。实际业务提交仍由项目目标委托负责，框架只协调会话、输入捕获和收尾。DragDrop、UGUI.DragDrop、示例 3 个离线工程零警告、零错误，594 个 C# 文件格式检查通过。未新增测试、未操作 Unity Editor、未提交；自定义上下文派发故障及禁用后的原生拖放竞态仍待运行验收。

2026-09-24 子视图内容代际契约复核：纯同步提供方现在可独立实现 `IViewContentVersion`，子视图句柄与导航、内容挂载统一读取该契约；非空引用令牌在三个入口均得到校验，避免值类型反复装箱导致每次检查误判失效。ChildViews、Navigation、UGUI、Tabs 及其示例 6 个离线工程零警告、零错误，594 个 C# 文件格式与元数据检查通过。未新增测试、未操作 Unity Editor、未提交；同步提供方换代后的实际缓存淘汰与原生界面释放仍待运行验收。

2026-09-24 Tooltip 基础交互复验：仅在 `mui-unity-validation@8bc64b8a` 的 Play Mode 中创建不保存的临时 Canvas、锚点与浮层，手动推进 TooltipTrigger 时钟。指针进入后延迟未满不显示，达到阈值显示；指针离开关闭；选中后显示，按下后保持抑制，失焦再选中后重新显示。临时对象在 finally 中销毁，验收实例已退出 Play Mode 并恢复原场景；复查 MUI 与 fun-slg 两个编辑器均仍运行。此探针未覆盖真实设备悬停、多指针、目标失效、View 门控与外部点击。未新增测试文件、未提交。

2026-09-24 标准模块职责与依赖复核：对照设计第 18 章检查 Runtime/Modules 的通知、Loading、主题、本地化、对话框与拖放边界；Runtime 未直接访问文件、网络、PlayerPrefs、Unity Resources、Addressables 或全局低内存事件，具体 Resources/JSON 后端仍位于 Samples。`MUI.Dialogs` 源码没有直接使用资源契约，已从 asmdef 与离线工程移除冗余的 `MUI.Resources` 直接引用；启用非传递项目引用后 `MUI.Dialogs` Release 编译零警告、零错误。架构文档同步修正 Tab 的同步/异步职责描述。MUI 验收实例请求脚本刷新后恢复空闲，未查到 C# 编译错误；MCP 实例列表在域重载期间曾短暂清空，未操作其他编辑器。594 个 C# 文件格式检查及补丁空白检查通过。此轮未新增测试、未提交；上述扫描和编译不替代各模块的运行及目标平台验收。

2026-09-24 菜单故障收尾修复：`ContextMenuController` 在返回登记释放前先撤销会话资格，导航设置逐项恢复，即使某一步抛错也清空菜单引用；禁用、帧刷新和动态按钮刷新故障共用逐项清理，分别尝试撤销会话、隐藏浮层与恢复焦点，错误交由 UIErrors 报告。`MUI.UGUI` 使用匹配的 Unity 2022.3.62f3 程序集离线编译，零警告、零错误；594 个 C# 文件格式检查通过，验收实例脚本重编译后未查到 C# 错误。仅在 MUI 验收实例的 Play Mode 中用不保存的临时对象注入返回登记释放异常，观察到浮层关闭、会话失效、焦点恢复与一次错误报告；同次复跑 Down 跳过禁用项和 Cancel 关闭焦点恢复，均通过。退出 Play Mode 命令返回成功，进程级核对 MUI 与 fun-slg 编辑器仍在；随后 MCP 实例索引短时仅返回不相关项目，未继续控制任何编辑器，故未取得最终状态资源确认。未新增测试文件、未提交；真实设备输入和项目覆写回调重入仍待验收。

2026-09-24 菜单 EventSystem 输入复验：仅在 `mui-unity-validation@8bc64b8a` 的 Play Mode 中建立不保存的临时菜单与 EventSystem，经 `ExecuteEvents` 派发原生 `IMoveHandler` 的 Down 后，焦点从 First 跳过禁用的 Middle 到 Last；向 Last 派发 `ICancelHandler` 后事件标记为已消费，菜单关闭且焦点恢复 Anchor。临时对象在 finally 中销毁，验收实例已退出 Play Mode、原场景保持不变；复查 MUI 与 fun-slg 编辑器仍在运行。此探针覆盖 Unity 事件接口派发，不等同真实键盘/手柄设备输入，也未覆盖自定义 IsInteractable 回调重入、异步命令与外部点击。未新增测试文件、未提交。

2026-09-24 菜单焦点运行复验：仅在 `mui-unity-validation@8bc64b8a` 的 Play Mode 中创建不保存的临时 Canvas、EventSystem、锚点与两个 Button。显示菜单后选中 First，禁用 First 并调用 RefreshItems 后选中 Second，隐藏菜单后选中恢复到 Anchor；浮层显示与隐藏状态符合预期。探针在同次执行的 finally 中销毁临时对象，验收实例已退出 Play Mode 并恢复原场景；复查 MUI 与 fun-slg 两个编辑器仍在运行。此验证未触发原生 Move/Cancel 事件，也未覆盖项目覆写的 IsInteractable 回调重入。未新增测试文件、未提交。

2026-09-24 ContextMenu 回调边界复核：Unity 2022.3.62f3 的 uGUI `Selectable.IsInteractable()` 为可覆写方法，菜单筛选和恢复焦点会进入项目代码。`ContextMenuController` 的帧内导航刷新现延后回调中请求的层级重建，并在每次可用性回调后复核会话、按钮身份与输入资格；帧内刷新异常会尝试撤销菜单、隐藏浮层及恢复焦点，分别报告清理错误。焦点恢复重新读取当前选择，并核对旧恢复目标、EventSystem 和浮层锚点，避免旧菜单覆盖新菜单焦点。`MUI.UGUI` Release 构建零警告、零错误，594 个 C# 文件格式检查与补丁空白检查通过；仅对 `mui-unity-validation@8bc64b8a` 请求脚本重编译，编辑器恢复空闲且 Console 未查到 C# 编译错误。真实 Unity 菜单、原生 Move/Cancel 及项目覆写回调重入仍未运行验收。未新增测试、未进入 Play Mode、未操作其他 Unity Editor、未提交。

2026-09-24 拖放捕获释放重入修复：`DragSession` 释放项目传入的指针凭证时拒绝从该释放回调重入 Drop 或等待本会话销毁；最终收尾尚未发布结果时也拒绝重入提交。业务修改仍只由项目 `DropTarget` 委托执行。`MUI.DragDrop` Release 构建零警告、零错误，594 个 C# 文件格式检查通过；仅在 `mui-unity-validation@8bc64b8a` 的编辑模式执行内存探针，投放和取消两条路径均观察到重入拒绝、原结果完成及源/目标生命周期清理完成。未创建测试文件或进入 Play Mode；原生 EventSystem 拖拽、多指针及不合作业务提交仍需运行验收，未操作其他 Unity 编辑器、未提交。

2026-09-24 分页改动后运行复验：仅在 `mui-unity-validation@8bc64b8a` 加载已有 Navigation 场景并进入 Play Mode，观察到分页首次加载、失败保留旧条目、显式重试、前插、双方向窗口淘汰及查询重置日志，随后收到 `MUI Navigation shutdown complete`。这证明当前分页代码的现有演示链路完成，不覆盖自定义 `IPagedListSource` getter 抛错或通知重入。验收实例已退出 Play Mode，恢复未修改的 `ResourceBinding 1` 场景；复查 `mui-unity-validation` 与 `fun-slg` 两个编辑器均仍运行，未新增测试、未提交。

2026-09-24 分页来源状态故障复核：`VirtualListElement` 的显式分页入口捕获来源 `IsActive` 读取失败并返回 Failed；自动预取逐项读取 `IsActive`、`HasMore`、`IsLoading`、`Error` 与 `Insertion`，每次项目 getter 返回后复核来源归属，故障只进入独立的 `PageError`，不把已有列表刷新置为 Error。同步返回的自动请求在来源仍有效时才登记，旧请求不能覆盖新来源的在途状态。使用 Unity 2022.3.62f3 程序集顺序编译 37 个离线工程，全部退出码 0、零编译警告及错误；594 个 C# 文件的成员布局与格式检查通过。隔离 Unity 实例的现有 Tabs 与 Navigation 演示分别收到 `cleanup complete`、`shutdown complete`；Navigation 控制台被 MCP 标为 Exception 的 98 条均从 `Debug.Log` 发出，未见真实异常。现有演示不注入自定义 getter 故障，此分支仍需针对性运行验收。验收实例已退出 Play Mode 并恢复 ResourceBinding 1 场景，未操作其他编辑器、未新增测试、未提交。

2026-09-24 列表来源故障与职责复核：两类列表在换源前后比对集合版本，订阅期间静默变更时重新读取快照；事件 `add` 失败后先使回调失效，再尽力撤销项目来源监听，并保留订阅及回滚的全部异常。虚拟列表分页的 `QueryVersion` 读取失败现返回 Failed、显示独立的 PageError，恢复读取后清除过期错误；实现版本接口的来源返回空标记会明确失败。重新扫描 Runtime、Editor、Samples 和生成器，Runtime 未发现具体资源后端、网络/文件持久化、低内存监听或业务事务实现；Editor 的文件写入属于页面和 Prefab 制作。使用 Unity 2022.3.62f3 匹配程序集顺序编译 37 个离线工程，全部零警告、零错误（日志 `/tmp/mui-full-build.7sx25s`）；发布生成器 DLL 与 Release 产物哈希一致，594 个 C# 文件格式检查及 Unity 元数据缺项检查通过。未新增测试、未提交、未操作 Unity Editor；自定义来源 getter 与原生回调的运行交互、完整平台验收仍需补齐。

2026-09-24 两类列表换源归属复核：`VirtualListElement` 与 `RecyclingListElement` 在读取候选、退订旧来源、订阅新来源和应用快照前后核对赋值代际与父激活；自定义事件 `add` 中同步换源时，旧监听在 `add` 返回后撤销。虚拟列表分页先使旧回调失效再退订，来源 `IsActive`/`QueryVersion` 等项目 getter 返回后重新核对归属；新来源绑定不再为了初始化状态调用 `QueryVersion`。当前源码的 37 个离线工程全部通过，均零警告、零错误；生成器发布 DLL 与 Release 产物一致，594 个 C# 文件格式检查通过。未新增测试、未操作 Unity Editor、未提交；自定义来源事件访问器故障、Unity 原生回调重入和完整平台运行验收仍待完成。

2026-09-24 全量离线构建与虚拟列表分页复核：在修改分页通知前，37 个 `Tools~/Build` 工程顺序构建全部通过，各日志零警告、零错误；生成器 Release 构建通过，发布 DLL 与源码产物 SHA-256 一致。随后发现分页来源在状态观察者回调中再次变化时，原有 `PublishPageState` 会丢弃嵌套通知；现有界补发最新状态，来源代际改变时停止旧轮属性发布，单项观察者异常不跳过其余属性。修改后的 MUI.UGUI、Navigation 示例及编辑器依赖定向离线编译零警告/错误，594 个 C# 文件格式检查通过；修改后尚未重跑全部 37 工程，也未进行 Unity 原生回调重入验收。未新增测试、未操作 Unity Editor、未提交。

2026-09-24 本轮职责与清理故障复核：抽查通知、Loading、拖拽、主题、本地化、用户偏好和本地 UI 自动化，未发现项目文件/网络持久化、资源后端或业务事务实现。`LoadingScope.Begin` 在登记或输入阻挡失败后的令牌清理也失败时，现保留原始异常与清理异常；`FontSizeBinding` 的组合退订会分别尝试偏好与主题，失败项保持可重试，创建失败的原异常也不再被退订异常覆盖。Tab 内容区新增同级 TabBar 绘制顺序与内容区 Override Sorting 的运行时/编辑器校验，更新对应接入说明。MUI.Loading、MUI.Themes、MUI.UGUI.Tabs.Editor 及依赖定向离线编译零警告/错误，594 个 C# 文件格式检查通过；这些新改动尚未进行 Unity 运行验收，也未重新执行全部 38 个离线工程。未新增测试、未操作 Unity Editor、未提交。

2026-09-24 最近改动后的全量离线复编：使用 Unity 2022.3.62f3 匹配程序集，顺序编译 37 个离线工程及生成器，共 38/38 通过，均为零警告、零错误；594 个 C# 文件格式检查、元数据检查和补丁空白检查通过。CLI 的沙箱进程列表与桌面 Unity 窗口状态不一致，无法据此确认验收项目是否独占运行，因此本次未启动、关闭或控制任何 Unity Editor；Tab 延迟提示、内容提供方取消和手动帧仍缺当前代码的运行验收。

2026-09-24 内容提供方准入复核：`ContentViewProvider` 在调用项目同步/异步创建接口前复核内容宿主；异步入口还在读取提供方代际前后检查取消，避免已销毁内容区域或已取消请求继续触发资源创建。返回凭证后的挂载复核与失败归还保持原协议。MUI.UGUI.Tabs 及依赖离线编译零警告/错误，594 个 C# 文件格式和元数据检查通过；外部提供方取消不合作、释放异常与 Unity 原生回调仍需运行验收。

2026-09-24 Tab 延迟提示线程边界：异步 LoadingPlaceholder 的非零延迟要求所属 UI 调度上下文，构造时拒绝缺失上下文；计时续体发布前再次检查所属线程，错误上下文只报告诊断，不在后台线程写入 Tab 状态。零延迟及纯同步路径不增加计时任务。MUI.Tabs、MUI.UGUI.Tabs 离线编译均为零警告/错误，594 个 C# 文件格式及元数据检查通过；Unity 延迟提示与错误上下文运行验收仍待完成。

2026-09-24 手动帧入口边界复核：`UIHost.AdvanceFrame` 现在拒绝在组件停用时推进导航请求与 Tick，与自动模式只在启用期间驱动的行为一致。重新检查 Runtime 的资源后端调用、网络/文件持久化、业务事务和 Unity 对象空值写法，未发现需要迁出框架的新实现；界面实例 Prefab 创建、显示位置资源替换和显式本地诊断仍归 UI 适配层。`MUI.UGUI` 离线编译零警告/错误，594 个 C# 文件格式检查、元数据检查和补丁空白检查通过；本次未操作 Unity Editor，停用状态与手动帧回放仍需运行验收。

2026-09-24 设计契约与格式复核：`UIHost` 增加 `AutomaticFramePump` / `AdvanceFrame(delta)`，在组件保持启用时可用项目时钟统一驱动 Pump 与 Tick，自动模式保留原行为；手动模式在自动驱动开启时拒绝调用，非法时长在派发前拒绝。设计第 9/13 章改用实际独立同步/异步 Provider 契约，明确语言目录、字体及共享资源由项目准备与持有，UI 只刷新文本和布局。格式工具新增基于 Roslyn token 范围的连续空行检查，修正 8 个文件；字符串与注释内部换行不参与压缩。当前只完成离线编译和静态检查，手动帧回放、资源切换布局及 Unity 运行交互尚未验收，未新增测试或操作其他 Unity Editor。

2026-09-24 输入门控重入与全量编译复核：`InputGate.Block` 在通知观察者后复核门控是否已销毁，避免返回实际不再阻挡的令牌；`Changed` 的重入修改改为有界发布最新状态，销毁期间仍发送最终状态。复核 Runtime 的分页、主题、本地化、Loading、Notifications、Dialogs、DragDrop 和 Tab 缓存边界，未发现新的资源后端、网络、持久化或业务事务实现。使用 Unity 2022.3.62f3 匹配程序集、禁用构建服务器并单进程编译，37 个离线工程及生成器工程共 38/38 项通过，零警告、零错误；594 个 C# 文件格式检查及元数据检查通过。此轮未新增测试或操作 Unity Editor，运行时重入交互与完整平台验收仍待完成。

2026-09-24 异步资源绑定视觉复验：从独立项目目录启动 Unity 2022.3.62f3，将既有 Resource Binding 场景临时设为异步模式。Game 截图 `/tmp/mui-unity-validation/Assets/Screenshots/mui-resource-async-warm-20260924.png` 与 `/tmp/mui-unity-validation/Assets/Screenshots/mui-resource-async-cool-20260924.png` 显示 Warm 橙色和 Cool 蓝色图像、资源键文字及 3 份当前凭证，画面非空且无洋红。长延迟请求发出时读取 `Pending=3`、`LiveLeases=3`，证明该时刻旧显示凭证仍被持有；由于工具调用超过请求延迟，没有获得加载中瞬态截图，不能据此断言其逐帧画面。关闭任务完成时 `Pending=0`、`LiveLeases=0`。日志 `/tmp/mui-validation-async-visual-20260924.log` 未出现内置 Shader include 错误。验收只覆盖该示例的现有资源键路径，不证明其他资源后端、原生赋值异常或设备渲染。独立 Editor 已退出，未新增测试或修改 MUI 运行时代码。

2026-09-24 独立项目视觉复验：同一 Unity 2022.3.62f3 安装中，原先从 MUI 仓库目录启动临时验收项目时，内置 Shader 编译器报告找不到实际存在的 `HLSLSupport.cginc`；仅重建 `ShaderCache` 仍复现。保留旧缓存副本后，从 `/tmp/mui-unity-validation` 作为工作目录重新启动该项目，日志 `/tmp/mui-validation-shader-projectcwd-20260924.log` 不再出现 Shader error。Resource Binding 示例的同步模式 Game 截图 `/tmp/mui-unity-validation/Assets/Screenshots/mui-resource-projectcwd-20260924.png` 与 `/tmp/mui-unity-validation/Assets/Screenshots/mui-resource-cool-projectcwd-20260924.png` 分别显示 Warm 橙色和 Cool 蓝色图像、对应文字与凭证计数，画面非空且未出现洋红。该对照只证明此启动方式下的示例同步渲染，不证明异步逐帧视觉、其他页面、设备或所有平台。独立 Editor 已退出；未修改 Unity 安装或 MUI 运行时代码。

2026-09-24 当前代码 Unity 复验：在沙箱外只读确认 Unity CLI 登录与 Personal 许可证有效；当前运行的业务 Editor 仍是 `fun-slg`，本次批处理始终显式指定独立项目 `/tmp/mui-unity-validation`。首次导入发现临时项目缺少 Navigation 的 `PageArgs`、`PagePresenter` 等 6 个源文件、Tabs 的 `TabPageViewModel` 及部分示例 asmdef；只补齐缺失的示例文件和原 `.meta`，保留已有场景与文件。随后 Unity 2022.3.62f3 的 Navigation、Settings、Tabs 三个批处理 Play Mode 演示均退出码 0，分别收到 `shutdown complete`、`Unbound/saveExecuting=False` 和 `cleanup complete`。日志为 `/tmp/mui-navigation-20260924-recheck2.log`、`/tmp/mui-settings-20260924-recheck.log`、`/tmp/mui-tabs-20260924-recheck.log`。这证明当前导入及三个既有演示成功，不覆盖异步资源槽换键与迟到释放、真实画面和原生输入、全部故障分支或设备/IL2CPP。未新增测试，未提交。

2026-09-24 异步资源槽运行复验：独立验收 Editor 的 `ResourceImageDemo` 使用 500 ms 延迟且加载器故意忽略取消。初始持有 3 份；连续两次换键后 6 个请求全部结束，累计创建 9 份、归还 6 份，仍持有当前显示的 3 份。三项预期加载失败后持有量仍为 3，失败计数为 3；重试成功后累计创建 12 份、归还 9 份。再次换键并在 3 个请求在途时关闭，异步清理任务最终完成，`Pending=0`、`LiveLeases=0`、`Created=Released=15`。计数证明该演示中的迟到凭证最终全部归还；未采集每次原生赋值的顺序。该场景只覆盖示例后端和现有控件路径，不证明任意项目后端、原生 setter 抛错或全部竞态。当次 Editor 日志 `/tmp/mui-validation-direct-20260924.log` 出现内置 `UI/Default` 等 Shader 找不到 `HLSLSupport.cginc`，当次画面不能记为通过；上方的独立同步视觉复验已消除该启动问题。独立 Editor 已退出。

2026-09-24 边界与一致性复核：再次扫描 Runtime、Editor、Samples 和生成器，Runtime 未发现网络请求、持久化、具体 `Resources`/Addressables 后端、低内存事件监听或业务事务实现；Core/Resources/Navigation 仍保持无 Unity 引用。`LifetimePreloadExtensions` 保留为 `IPreloadViewProvider` 的 UI 预加载协议协调，只负责取消、结果代际校验和凭证托管，不加载、缓存、下载或卸载资源。清理了 Navigation、Tabs、动态内容和同步设计文档中已删除的 `BudgetedResourceLoader`、`TrimMemoryAsync`、`MemoryPressure` 现行描述，统一说明全局资源策略由项目负责。594 个 C# 文件通过成员布局和空白格式检查；当前工作树使用 Unity 2022.3.62f3、uGUI 与 TMP 程序集完成 38/38 个离线项目构建（37 个 Unity/Editor/Samples 项目及 1 个生成器项目），Runtime、Editor、Samples 的 37 个 asmdef 与脚本元数据核对通过。未新增测试、未提交；此时异步资源槽和完整平台验收受 Unity LicensingClient 环境阻塞，随后三个既有演示已按上方记录复验。

2026-09-24 本轮完整职责与规范审计：对 Runtime、Editor、Samples 和生成器重新扫描文件、网络、持久化、具体资源后端、低内存监听、业务事务及 Unity 对象空值写法，未发现需要从框架移出的新运行时代码。`UIAutomation` 的截图和 PNG 编码只保留调用方拥有的内存快照，不写文件、不上传、不安装远程服务，作为显式本地 UI 诊断能力保留。导航文档已修正共享依赖拓扑排序、外部拥有者 `InUse` 检查和进入转场的当前描述；README 已更新为 38/38 项目和 594 个 C# 文件。使用 Unity 2022.3.62f3 程序集的全量离线构建结果为 38/38，格式检查为 594/594，Unity 导入目录 C# 元数据缺失为 0。当时重跑 Navigation、Settings、Tabs 的三个独立 Unity 进程均在启动阶段因 LicensingClient IPC 超时退出码 199；该环境失败已由上方最新复验覆盖，不作为源码失败。异步资源槽和完整平台验收仍未完成。

2026-09-24 同步能力文档一致性复核：对照 `Navigator.CreateSynchronous`、`UIHost.InitializeSynchronous`、同步打开/关闭/替换/批量关闭/预加载/缓存清理/参数更新/模型换绑/退出入口，修正文档中仍把顶层同步导航、子视图同步事务描述为“尚未接通”的历史措辞。当前结论改为“源码入口已接入，Unity 生命周期、原生输入、资源后端回滚和设备/IL2CPP 运行验收仍待完成”。本轮只修改文档，不新增测试或提交。

2026-09-24 第二轮状态一致性复核：将 Route 同步能力推导明确为生成器保留的显式声明，避免从不完整静态类型信息误判运行时资源和子树能力；同步更新 Navigation、Commands 与 Code Architecture 中的早期实现阶段描述。`format-code.py --check` 仍为 594 个 C# 文件通过，成员布局调整为 0；本轮无运行时代码修改。

范围纠偏：以 FUI 为主要参考、MUFramework 为次要参考，完整目标聚焦 UI 框架职责。表单草稿、字段校验和业务提交不作为框架交付项；本轮未接入的 Forms 模块及其构建项目已撤回。输入绑定、命令和通用关闭守卫仍属于框架能力。

字体职责纠偏（2026-09-21）：TMP 字体、图集、回退字体及共享资源由 TMP 与项目资源系统管理；框架保留 FontAsset 借用绑定和 Lifetime 通用凭证托管。历史记录中把专用 TMP 字体资源槽列为必做缺口的描述不再适用。

## 当前职责整改核对（2026-09-21）

规划边界补充：跨启动存储/迁移、热更新、跨区域业务事务、DI 容器、资源重试及远程工具服务归项目系统。主设计第 18 章已明确 UI 接入边界，不能再将这些项目基础设施列入框架待实现项。参数更新保持共享依赖契约，DependencyChangeRequired 是明确拒绝；跨拥有者状态决策由项目负责。此前将自动联合改写共享依赖列作必补缺口的记录不再适用，不表示已实现联合事务。

- 已删除：共享资源加载、资源预算及全局内存回收协调器，不保留可选策略程序集。
- 已移出运行时：具体 Unity Resources 与 JSON 语言目录后端，以及面向任意资源的 Lifetime.Load/LoadAsync 扩展，现为 Resource Integration 接入示例。Lifetime 只提供通用的取消、操作跟踪和凭证托管。
- 已收缩：主题及本地化服务只接受已准备的目录，通过 SetCatalog 同步刷新；移除目录加载入口、资源槽及 Resources 程序集依赖。目录存活期由项目负责。
- 已收缩：导航只清理自身预加载与停用页面，Tab 只清理自身缓存；不订阅 Application.lowMemory、不登记外部池。
- 已核对仍属 UI：Loading 的提示进度聚合、Notifications 的屏幕展示队列、Dialogs 的模态确认调度、DragDrop 的指针会话与项目提交回调、Automation 的控件查询及交互入口。它们不实现资源下载、网络请求、存档或业务规则。
- 已移出运行时：LoadedPrefabViewProvider/SynchronousLoadedPrefabViewProvider 驻留账本归项目接入示例；PagedList 游标请求实现归 Navigation 示例，Core 只保留分页来源接口及交互结果类型。
- 已封装：ResourceSlot 与赋值异常移入 uGUI 内部，创建槽的方法改为控件私有方法；业务使用对象绑定或 Source 绑定。
- 文档整改：主设计及架构图已移除资源预算/全局回收/模块内加载职责；主题、本地化示例改为目录注入；Tab 文档使用自身缓存清理入口。历史实现日志仅作为历史证据保留。
- 尚待核准：上述 API 收缩后的 Unity 导入、运行链路和资源释放验收，以及完整设计剩余能力。整体设计目标仍未完成。

### 本次职责复核及验证

最终边界复核：再次检查 Runtime/Modules、Core 集合/偏好、资源契约、uGUI 自动化和编辑器接线，没有发现新的网络、持久化、资源后端或业务规则实现。已修正本文件的当前待验收表，移除跨拥有者事务等越界待办，并更新 README 与架构文档中的提供方位置。当前源码重新执行 37 个离线构建项目，全部通过；日志目录为 `/tmp` 指针文件 `mui-boundary-build-path` 对应的 `mui-boundary-build-q0f_6mg9`。28 个 Runtime/Editor 框架程序集依赖无环、不引用 Samples，metadata 齐全。此轮只补正文档，未新增功能或测试。

- 进一步将任意资源的 `Lifetime.Load/LoadAsync` 接线移至 Resource Integration 示例，保留同步和异步独立实现。框架中的加载调用仅限视图提供方或控件显示位置接入项目后端；不存在内建下载、共享资源缓存、持久化或全局回收服务。
- 逐项核对导航、子视图、列表、主题、本地化、Loading、通知、对话框、拖拽、偏好、Automation、生成器和编辑器边界；职责表见 CODE-ARCHITECTURE。保留的列表分组和树展开用于显示，不包含数据查询。
- 当前工作树 37 个离线构建项目全部通过；最终扩展移动和生成器调整后，Navigation.Editor 及依赖在 `UNITY_EDITOR` 条件下再次编译，零警告、零错误。37 个 asmdef 引用以及 Runtime/Editor/Analyzers 的 meta 完整性检查通过，Runtime 不引用 Samples。
- `[ViewRoute]` 已生成类型化工厂；同步 Navigation 示例使用生成工厂并显式提供 Presenter，变体通过参数创建。同步声明、模型工厂与业务依赖仍由项目决定，不引入 DI 或运行时扫描。生成器发布成功，自动 Presenter 选择和错误诊断分支尚未专项运行验收。
- 未新增测试、未启动 Unity、未提交。上述结果是源码和离线编译证据，不能替代 Unity 导入、运行、IL2CPP 及资源释放验收，也不代表完整设计全部完成。

## 当前运行验收入口修正（2026-09-21）

- 补齐主包 `com.unity.modules.jsonserialize` 1.0.0 依赖。`Editor/Authoring/PageAssemblyInspection.cs` 读取 asmdef/asmref 时直接使用 JsonUtility，故此前“仅示例需要 JSONSerialize”的判断不成立。此依赖用于编辑器制作工具，不将项目 JSON 资源后端移回 Runtime。
- 修正 Settings/Navigation/Tabs 既有批量预览入口：不再进入 Play Mode 三秒后直接返回成功，而是等待各自演示末尾的清理完成日志；未完成在 45 秒截止时返回失败。进入 Play Mode 前安装观察者，兼容关闭域重载；完成后继续观察短暂窗口内的错误。该入口证明的仍仅是对应演示完成，不证明全部设计条目。
- 三个示例 Editor 项目及依赖在 UNITY_EDITOR 条件下编译通过，零警告/错误；未新增测试。验收项目同步了修改后的示例及源码哈希快照。
- 环境结论更正：受限环境中的 `Not signed` / codesign 输出不能证明 Unity 安装损坏。在受限环境外通过 Unity CLI 已成功启动指定的 2022.3.62f3 并进入编译；无需用户重装编辑器。完整签名核验仍有异常，但不是本次导入失败的直接原因。
- 真实导入发现 Navigation 编译器崩溃：Unity 自带 Roslyn 在异常过滤器、await 与嵌套 catch 组合中抛出 StackOptimizerPass1 的 KeyNotFoundException（error）。PrepareDependenciesAsync 的两个降级分支改为在 await 前保存异常，保留原始错误与清理错误的聚合语义。同一内置 csc、原始 Unity 响应文件重新编译成功；补齐下述程序集引用后，完整 Unity 导入已通过。
- Unity MCP 只读列表仍仅显示 fun-slg 项目；未在该业务项目执行 MUI 验收。CLI 未发现 Pipeline 实例不代表机器没有运行中的 Unity。

## Unity 2022.3.62f3 当前代码复验（2026-09-21）

- 实际导入发现并补齐直接程序集依赖：MUI.Editor 引用 MUI.Resources；Samples.ResourceIntegration 引用 MUI.Core。离线构建此前由传递引用掩盖了缺项；现在各 csproj 与 asmdef 的 MUI 直接引用对齐，并禁用传递项目引用，避免重复漏报。
- 直接引用对齐并禁用传递引用后，37 个离线构建项目全部通过；日志目录 `mui-boundary-build-xay6dsnd`，结果见 `/tmp/mui-boundary-build-path` 指向目录的 results.json。
- 独立项目 `/tmp/mui-unity-validation` 导入退出码 0；Package Manager 已解析 JSONSerialize、ScreenCapture、ImageConversion，主包声明生效。日志 `/tmp/mui-current-import-refs.log`。
- Navigation 既有批量演示退出码 0，收到 shutdown complete：取消准备、关闭等待取消后继续清理、模态/焦点、万条列表与固定网格、静态/动态子视图等已执行。日志 `/tmp/mui-navigation-current-play.log`；未启用的可选演示分支不计入验证。
- Settings 既有批量演示退出码 0，保存 80%、重置 50%，销毁后 Unbound 且 saveExecuting=False。日志 `/tmp/mui-settings-current-play.log`。
- Tabs 既有批量演示退出码 0，Loading 时 TabBar 可用，失败后重试 Ready，快切旧请求 Superseded，守卫拒绝、等待取消、父关闭 ParentInactive，最后 cleanup complete。日志 `/tmp/mui-tabs-current-play.log`。
- 上述为 batchmode/nographics 自动演示，仅检查既有流程及日志，没有新增测试；不证明真实渲染/输入、所有故障分支、纯同步独立完整链路、IL2CPP、真机性能或长期内存。

## 交互编辑器洋红显示修复（2026-09-21）

用户截图显示整个验收编辑器洋红，日志包含内置 BlitCopy、GUIRoundedRect、GUITexture 等 Shader 的 HLSLSupport.cginc 缺失错误。安装中的 include 文件实际存在且可读，旧编译器日志只有初始化。仅关闭独立验收编辑器，将项目 Library/ShaderCache 重命名保留备份，再从验收项目目录重新启动 2022.3.62f3；新编译器日志出现对应内置 Shader 的 ok=1，启动日志未再出现 Shader error。未修改 MUI 渲染代码或 Unity 安装文件。日志 `/tmp/mui-shader-cache-rebuild.log`；画面尚待窗口确认，不能把此前 nographics 演示当作视觉通过证据。

## 已有代码

### 成员分组规范与虚拟列表热路径（2026-09-21）

- 按用户截图补齐成员布局规则：字段集中在前部，随后为构造方法、事件、属性/索引器、方法和嵌套类型；字段区与构造方法及各方法间保留空行，不穿插字段。首轮整理 320 个文件，保留字段之间原有顺序及所属注释。ViewInstance 中唯一初始化顺序冲突为枚举初值与独立完成对象，人工核对后调整。
- 新增开发工具 `Tools~/CodeStyle`，与生成器共用 `MemberLayout` 语法重写规则。`format-code.py --check` 同时检查成员分组、空行和原有空白规则；冲突会失败，不静默改变初始化顺序或跨条件编译搬动成员。工具使用本机 .NET 10 SDK，不改变 Unity 2022.3.62f3 或运行时目标。
- 重新发布生成器，并检查实际 SettingsViewModel 生成文件：三个命令字段集中在类型前部，属性、构造方法与方法分隔正确，绑定调用和长参数显式换行保留。成员布局与空白复检通过。
- 动态高度虚拟列表此前在已完成测量的每一帧仍分配批次集合及单元数组；现在无待测量单元时直接返回。刷新中捕获 item 的查找委托改为显式遍历，保持已有键优先及同模板空闲单元复用顺序。真正测量、换源和异步刷新仍可能分配，不宣称整个列表零分配。
- Unity 热重载发现 Element 与 VirtualListElement 的私有 initialized 同名序列化冲突；基类字段改为 elementInitialized，更新内部引用。重新编译及运行后 Console 无错误。
- 真实同步测量列表中，100/1000/10000 条数据的顶部及底部均为 8 个物化单元，定位状态 Ready；三列网格为 22 个，失效重测后仍 Ready。稳定视口连续执行 10000 次测量入口，已测条目数量保持 8。ProfilerRecorder 的 GC.Alloc 当前线程记录：100 次显式 1KB 分配阳性对照为 100 个样本，10000 次稳定视口测量为 0 个样本；只覆盖该方法，不含整帧或滚动重绑路径。
- 全部 37 个离线构建项目通过，日志目录 `mui-boundary-build-u52xghwr`，结果位于 `/tmp/mui-boundary-build-path` 指向的 results.json。未新增测试文件、未提交；已退出验收项目 Play Mode。
- 成员布局和 Element 字段更名后再次顺序构建 37 个项目，37/37 退出码为 0，日志目录更新为 `mui-boundary-build-ic0x7879`。`MUI.UGUI`、生成器定向构建也均为零警告、零错误。
- 2026-09-24 尝试重新启动 `/tmp/mui-unity-validation` 以补验异步资源槽时，Unity LicensingClient IPC 超时，编辑器退出码 199；未将该环境失败记为运行时失败，也未操作 fun-slg 或其他 Unity 实例。异步资源换键、迟到凭证归还和关闭清理仍保留为待运行验收。

### TMP 向导、已有 View 关联及 Prefab Mode 复验（2026-09-21）

- 修复 TMP 基础资源未导入时的空引用：先调用 `TMP_Settings.LoadDefaultSettings()` 判断配置存在，再访问默认字体。缺失配置返回中文制作提示，不触发 TMP 导入窗口；实际缺失配置场景已验证。
- 页面向导默认关闭文字改为 `×`，无障碍标签仍为“关闭”。TMP 默认 LiberationSans 不含中文“关闭”，旧模板实际显示为方框；新模板所用乘号存在于默认字体中。字体选择、中文字体和回退配置仍归项目，框架没有新增字体加载或管理。
- 使用实际向导生成 TMPProfile，Unity 编译完成、绑定清单校验为空；同步打开后显示 TMP Profile 与 ×。截图 `/tmp/mui-unity-validation/Assets/Screenshots/screenshot-20260921-162623.png`。原生 Submit 关闭后快照：TotalInstances、PendingRequestCount、PostedRequestCount、PendingCleanupCount 均为 0。
- 关联 TMPProfile 原 Prefab 生成 TMPAssociatedProfile：原资产依赖哈希不变，没有重复生成 Prefab；新代码已编译，生成清单对原 View 校验无错误。此处验证的是实际向导方法和资产结果，不包含窗口逐控件操作。
- 在独立验收 Prefab 的真实 Prefab Mode 中调用 Element Authoring Scan/Apply，Element 数量为 2；同组 Undo 后 0，Redo 后 2。通过编辑器保存并关闭 Stage，重新读取磁盘资产，确认 ButtonElement/TextElement 均保留。未清空全局 Undo，未修改业务项目。
- TMP.Editor 定向编译零警告、零错误（`/tmp/mui-tmp-editor-current-build.log`）；592 个 C# 文件格式检查通过（`/tmp/mui-format-tmp.log`）。这两处 TMP 修复之后未重复执行全部 37 个项目构建；未新增测试文件。中途失败回滚、完整诊断交互及跨平台输入仍需验收。

### 页面向导中途失败回滚复验（2026-09-21）

- 在验收项目通过现有 PageTextBackend 扩展注册临时工厂：第一个文本正常创建，第二个文本挂到向导层级后抛出预期异常。随后调用真实 PageWizard.Create，不改写正式模板或新增测试文件。
- 结果：工厂调用 2 次，向导报告创建失败并清理；新页面目录、磁盘目录及目录 meta 均不存在，两个已创建对象均已销毁，PreviewScene 数量增量为 0。
- 验收结束移除临时注册并销毁临时窗口；现有场景保持未修改。该结果覆盖 Prefab 制作中途失败，不代表磁盘写入中断、清理自身失败或批量挂载中途失败也已通过。

### 同步页面 500 次逐帧开关复验（2026-09-21）

- 在 Unity 2022.3.62f3 编辑器 Play Mode，以向导生成的 TMPProfile Prefab、实际生成路由和 PrefabViewProvider 执行 500 次同步打开/关闭。每个游戏帧执行一轮，下一帧先检查上一轮清理完成；没有创建或等待异步导航任务，没有新增测试脚本。
- 500 次 Open 均为 Succeeded、Close 均为 Closed 且无错误。每轮开始前实例、请求、待清理和 Canvas 子树 View 数为 0；最终 CommitVersion 为 1000，历史、缓存、预加载、投递请求、退役缓存及隔离清理计数均为 0。
- 每 50 次采样一次，11 个采样点的原生 GameObject 数相对基线增量均为 0。Profiler 托管已用内存为 24,752,128–27,291,648 字节，起止为 25,493,504/25,350,144；Unity 总分配内存为 503,025,856–504,271,072 字节，起止为 503,313,030/503,705,002。该短轮次未观察到持续增长，不等于进程 RSS、框架独占内存或长期零泄漏证明。
- 记录同步 Open 调用耗时：首次 40.4183 ms，跳过前 10 次后的 490 次 P50/P95/P99 为 0.3932/0.5097/3.3132 ms，最大 3.6458 ms。资源常驻、实例每次重建；数据包括编辑器干扰，不含随后渲染帧、资源冷加载和动画，不能作为真机首帧延迟或统一预算。
- 完成后更新回调自动解除，读取结果后清空临时状态并退出 Play Mode。仍需列表热路径分配、异步资源循环、实际目标设备和更长时间的同配置比较。

### 控件批量挂载 Undo/Redo 运行复验（2026-09-21）

- 在验收 Unity 的编辑模式下创建临时 Button/Text，调用现有 ElementAuthoringWindow 的 Scan/Apply。确认当前撤销组为 Add MUI Elements 后，在同一次编辑器调用内 FlushUndoRecordObjects、PerformUndo、PerformRedo，Element 数量依次为 2 → 0 → 2。
- 只清除临时对象关联的 Undo 记录，销毁临时窗口与场景对象；未清空用户全局 Undo 历史。该结果补足此前跨 MCP 调用未得到可靠结果的普通场景 Undo/Redo 验收。
- 故障中途回滚及 Prefab Mode 的保存/Undo 尚未由本次覆盖，不能据此宣称所有制作流程已验收。

### 导航可选故障分支运行复验（2026-09-21）

- 验收 Unity 已恢复响应；直接在现有 Navigation 场景启用缓存、参数更新、换绑、关闭超时和同步守卫选项，未再次切换场景。既有演示运行至 `MUI Navigation shutdown complete`，本轮截取日志 `/tmp/mui-navigation-full-latest.log`。
- 缓存命中复用模型和视图、创建新句柄并采用新参数；过期项重建，LRU 保留最近使用项，超限/未知大小项不入缓存，清空后数量归零。
- 参数准备及提交故障均恢复原参数与模型状态；更新期间关闭取消候选并释放；候选释放错误在后续更新成功后仍于最终关闭报告，没有被覆盖。
- 换绑失败保留原模型，成功只释放旧的自有模型一次；借用模型关闭后释放数为零；延迟释放期间换绑与关闭均等待，允许释放后分别返回 Applied/Closed。
- 关闭超时返回 ClosedWithCleanupPending/Pending，隔离数为 1，容量满时拒绝新页面；允许迟到清理后返回 Closed/Complete，隔离数归零。子视图参数更新、停用取消和换绑也输出预期结果。
- 日志异常来自演示主动注入的参数、释放、拒绝模型和超时分支。此记录不覆盖全部输入设备、编辑器 Undo 或性能长稳；未新增测试文件。

### 格式与列表目录整理（2026-09-21）

- 增加 `Tools~/format-code.py`，显式枚举 Runtime、Editor、Samples 和生成器中的源码；提供改写和 `--check` 模式。修复了只向格式工具传目录时可能检查不到文件的问题。
- 按 `.editorconfig` 统一 84 个文件的空白和换行；当前 592 个手写 C# 源文件格式复检通过。该入口不执行命名、控制流或 Unity 对象语义分析，不能替代代码审查。
- `VirtualListElement` 与 `RecyclingListElement` 的所有 partial 文件集中至已有的 `Runtime/Rendering/UGUI/Lists`，原 meta 随文件移动；虚拟列表刷新调度和条目准备拆至 `.Refresh.cs`，保留同一类型、命名空间和序列化字段。
- 新增 `CODE-STYLE.md`，明确中文注释、Unity 对象判空、纯同步路径、所有权与线程要求、程序集边界及目录规则；README 修正已删除的主题/本地化加载 API 说明。
- 增加 `--braces` 选项，逐构建项目执行 Roslyn IDE0011；37 个构建项目及生成器的大括号复检通过。格式化工具未展开的单行异常处理块另行整理，预期取消及容量错误的空处理分支补充中文说明。
- 生成模板展开构造函数与自动属性，补齐路由的控制流大括号；保留参数和绑定清单的显式换行，避免长声明被压到一行。重新发布 `Analyzers/MUI.Generators.dll`。这属于生成源码排版调整，不改变模型、绑定或路由协议。
- 本轮 37 个离线项目全部通过，零编译警告/错误，日志 `mui-boundary-build-oq3g8dc4`。模板最终排版调整后另行重编译 Navigation、Settings、Tabs，均通过；实际检查了生成的路由与嵌套模型绑定源码。最终 592 个源文件空白格式复检通过，未新增测试，Unity 交互仍受待处理的场景保存对话框阻挡。

### 确认框串行队列等待实际清理（2026-09-21）

- 源码复核发现：DialogService 收到 `CleanupStatus.Pending` 的超时结果后立即释放串行许可，但 Navigator 仍保留该实例并计入路由容量，下一确认请求可能被拒绝。
- 现在正常结果和取消后强制关闭两个分支都会记录未完成清理，并在归还许可前调用 `WaitForCleanupAsync`。该等待不使用已取消的调用者令牌，不重复关闭或释放资源；原超时错误保留，实际清理失败与原错误汇总。
- 非合作清理会延长当前确认请求及服务销毁的等待，符合服务“前一弹窗完成清理再展示下一项”的约定。服务不尝试替项目强制终止工作。
- Dialogs 及其依赖离线编译通过，零警告、零错误。随后在验收 Unity 中通过已有 RebindPageViewModel 延迟释放能力及真实 uGUI 实例运行：首个弹窗超时后 created=1/pending=2，后续请求仍等待；放行释放后 created=2/pending=1，原请求保留 TimeoutException。
- 调用者取消分支同样在实际释放之前保持 created=3/pending=2；放行后第四个弹窗正常创建，原请求保留取消与超时错误。最终四个模型各释放一次，pending/instances/cleanup 均为 0，服务、提供方、原生对象与临时状态均已清理。日志标记 `MUI DialogQueue review complete`；未新增测试文件。

### 同步实例加载后端约束移出框架（2026-09-21）

- `ISynchronousInstantiableResourceLoader` 只有 ResourceIntegration 中两个示例适配器使用；它约束源资源归还后的克隆依赖存活，属于项目资源后端策略。接口及原有 meta 已移至该示例，命名空间改为 `MUI.Samples.ResourceIntegration`，Runtime 不再声明这项后端能力。
- 框架继续通过 `ISynchronousViewProvider` 接收可同步创建、归还的视图实例；具体加载适配器如何保证克隆依赖有效由项目实现。同步路径未增加异步等待。
- ResourceIntegration 及其框架依赖离线编译通过，零警告、零错误；验收项目已同步新增示例接口。MCP 当前报告 `stale_status`，不能据此宣称此次改动已经通过 Unity 实际导入和运行。未新增测试。
- 迁移后重新执行全部 37 个离线构建项目，全部退出码 0；日志目录 `mui-boundary-build-h6w1hu51`，结果入口为 `/tmp/mui-boundary-build-path`。覆盖当前 Runtime、Editor、TMP 适配与示例的编译，不替代 Unity 导入、交互和性能验收。
- MCP 超时已定位：验收 Editor 的主线程采样显示 `SaveCurrentModifiedScenesIfUserWantsTo` 对应的场景保存对话框进入 `NSAlert runModal`。进程仍存活，未因超时重启；需处理该对话框才能继续运行导航故障分支。

### 纯同步回收列表交互编辑器复验（2026-09-21）

- 验收项目已独立连接 MCP，编辑器内读取确认 `com.coplaydev.unity-mcp` 为 10.2.0，Unity 仍为 2022.3.62f3；未操作 fun-slg。
- 运行既有 RecyclingList 场景，真实 Game View 截图发现窄窗口两侧裁切。演示 CanvasScaler 改用 Expand，使固定尺寸面板完整进入视口；重新编译进入 Play Mode 后截图确认标题、图标及按钮完整显示，未出现洋红渲染。
- 调用既有同步增删、字体清空/恢复和关闭入口，日志最终为创建 13、归还 13、仍持有 0；退出 Play Mode 的再次清理保持同一计数。证据日志 `/tmp/mui-validation-mcp-10.2.0.log`，截图 `/tmp/mui-unity-validation/Assets/Screenshots/screenshot-20260921-134340.png`。
- 此次验证了该示例的真实渲染及同步入口，未验证物理鼠标点击、全部同步导航/Tab 链路、平台构建或长期内存；未新增测试。

### 纯同步导航缓存重开修复与复验（2026-09-21）

- 独立验收项目接线既有 SynchronousNavigationDemo，实际发现缓存重开 PreparationFailed：CreateModel 取得缓存内容后，RequiresAsync 在新激活开始前查询了上一轮已结束的子视图作用域。
- ViewInstance 现在记录本轮激活准备完成状态。异步 Presenter 仍提前拒绝；子视图准备检查只在本轮 Prepare 成功后执行，不放宽 View 的生命周期校验，也不引入任务或异步等待。
- Unity 2022.3.62f3 重新编译成功。同一链路复验：参数更新 Applied，故意提交失败 CommitFailed 且 RecoveryFailed=False，换绑 Applied，故意替换失败保留原页 Open，成功替换 Committed，关闭 Closed，缓存重开 Succeeded。重复缓存重开时创建次数保持不变。
- 两个批量页面同步关闭 Completed，全部关闭且清理 Complete；预加载 Ready，清除后驻留占用 0。通过原生 Button 事件入口触发确认，后续同步帧泵发布 Completed/42/Cleanup Complete。不是物理鼠标输入验收。
- 场景 `/tmp/mui-unity-validation/Assets/MUI Samples/SynchronousNavigation.unity`，日志 `/tmp/mui-validation-mcp-10.2.0.log`。未新增测试。完整同步 Tab、共享依赖及其他设计验收仍待完成。

### 纯同步 Tab 生命周期复验（2026-09-21）

- 在独立验收场景配置既有 SynchronousTabsDemo 的父 View、TabBar、内容区和页面模板，运行已有同步入口，没有增加测试代码。
- inventory/quests 往返切换均 Ready，创建模型数保持 2、缓存数 1；禁止离开返回 Rejected/Denied，显示页仍为 inventory。
- 模拟 quests 准备失败返回 Failed，仍显示 inventory；取消模拟故障后重试 Ready。移除当前 quests 定义后回到 inventory，恢复定义成功。
- 同步结束父 Lifetime 后 IsEnded=True、DisplayedTab=null、缓存数 0、父 View 输入关闭。结果说明本示例的同步切换、缓存、守卫、失败恢复和父关闭链路通过；不覆盖所有重入/故障组合或物理输入。
- 场景 `/tmp/mui-unity-validation/Assets/MUI Samples/SynchronousTabs.unity`，日志 `/tmp/mui-validation-mcp-10.2.0.log`。

### 纯同步共享依赖所有权复验（2026-09-21）

- 独立场景接线既有 SynchronousDependenciesDemo；首次验收模板误用 Label，改为泛型基类契约 ItemLabel 后重跑。首次绑定失败不计为框架回归，以下为正确模板的运行结果。
- 两个父页面打开 Succeeded/Succeeded，共享界面仅创建一次、父拥有者数 2。直接关闭返回 InUse；冲突父页面被 ConflictingData 拒绝且清理 Complete。
- 保持依赖参数的父标题更新与换绑均 Applied；改写依赖参数返回 DependencyChangeRequired；直接换绑被持有依赖返回 InUse。独立路由变体创建不同依赖 Handle，原共享关系不变。
- 关闭第一个父页面后共享仍 Open、拥有者数 1；关闭第二个后共享 Destroyed，实例/历史/缓存/待清理均为 0。
- 显式持有时，两个父页面关闭后共享仍 Open；撤销显式持有返回 Released 后销毁。强制关闭共享依赖时两个父页面均 Destroyed，实例、历史、待清理归零。
- 场景 `/tmp/mui-unity-validation/Assets/MUI Samples/SynchronousDependencies.unity`，日志 `/tmp/mui-validation-mcp-10.2.0.log`。只覆盖 RequiredBefore 同步入口；AttachedAfter、异步竞态和降级失败组合未由本轮证明。未新增测试。

### 纯同步标准对话框复验（2026-09-21）

- 使用 Unity API 在独立验收项目创建两种契约匹配的 Prefab，配置已有 SynchronousDialogsDemo 与 UIHost 目录。此次没有经过保存对话框向导，不能作为向导交互通过的证据。
- 默认焦点为 Cancel；原生 Cancel 事件得到 Completed(false)，实例归零。确认后发布成功结果，示例下一帧打开 Alert，默认焦点 Acknowledge；原生 Submit 事件结束提示框，清理 Complete。
- Back 得到 Dismissed/Back，普通关闭得到 Dismissed/Closed，均未进入确认业务回调。最终实例和待清理数为 0。
- 环境发现：未聚焦编辑器且后台运行关闭时，Play Mode 可以停在第 1 帧，即使 MCP 执行代码正常。临时启用 Application.runInBackground 后帧数推进，Alert 后续打开得到验证。此前同一轮代码内的同步 API 调用证据有效，但不能当作自然帧循环、TTL 或长期运行证据。
- 场景 `/tmp/mui-unity-validation/Assets/MUI Samples/SynchronousDialogs.unity`；日志 `/tmp/mui-validation-mcp-10.2.0.log`。未新增测试；中文字体、物理输入、异步关闭守卫确认联动仍待验收。

### 同步 Tab 实际帧循环与空缓存维护（2026-09-21）

- 启用验收运行的后台帧循环，切到 quests 后闲置缓存为 1；超过示例 5 秒 TTL 后缓存自动为 0，quests 继续显示。再次选择 inventory 创建第 3 个模型，证明过期后重新建立内容。
- RefreshCacheSynchronous 在空缓存时直接返回，避免每帧无效创建维护委托、登记同步操作和构造空快照。非空缓存仍执行原有生命周期保护和失效检查。
- Unity 编译成功后重复运行，TTL 清理与回切重建正常。未新增测试。
- 此环境 GC.GetAllocatedBytesForCurrentThread 连显式创建 10 KB 数组也返回 0，因此丢弃该测量结果，不把任何本轮数值作为零分配证据；有缓存热路径仍需可靠 Profiler 验收。

### Loading 提示令牌持有修复（2026-09-21）

- 检查发现 LoadingScope.Begin 每次向 Lifetime 登记令牌，提前 Dispose 只结束提示，不移除 Lifetime 中的历史记录。长驻界面反复提示会累积已结束的令牌和清理委托。
- 改为每个 Lifetime 只托管一个加载提示活动集合，令牌提前释放时立即移除；多个 LoadingScope 共用该生命周期时仍分别维护显示状态。集合仅处理 UI 提示令牌，不跟踪或取消业务加载。
- Unity 编译成功，运行中连续创建/释放 1000 个提示后，Lifetime 托管记录数为 1；再创建两个活动提示并结束 Lifetime，两个令牌均结束，OperationCount/BlockingCount 为 0、IsVisible=False。未新增测试文件。

### macOS Player 首次构建验证（2026-09-21）

- Unity 2022.3.62f3 macOS Mono Development 构建成功：0 errors、3 warnings，包含同步导航、Tab、共享依赖、对话框、回收列表及 Settings 六个既有验收场景。输出 `/tmp/mui-mono-build/MUIValidation.app`，摘要 `/tmp/mui-mono-build-status.txt`。
- 无图形启动独立 Player，首场景同步导航日志 Ready/Succeeded；启动后已停止该验收进程。日志 `/tmp/mui-mono-player.log`。Null 图形设备的 Shader 不支持信息不用于渲染判定，其他场景运行和实际画面尚未由此验证。
- 构建校验报告项目未登记 UI 构建目录，因此构建成功不证明页面/路由资产覆盖率。构建警告仍需进一步核对。
- IL2CPP 尝试在实际编译前失败：当前安装缺少 mac-il2cpp。已恢复 Mono/原裁剪设置，正在通过官方 CLI 补装 2022.3.62f3 arm64 的 mac-il2cpp 模块；安装和 AOT 验证尚未完成，不能将 Mono 结果记作 AOT 通过。

### macOS IL2CPP 构建与启动（2026-09-21）

- 官方 CLI 已完成 mac-il2cpp 模块安装；仅重启独立验收编辑器后模块被识别，实际执行 arm64/x64 C++ 编译，构建成功、0 errors。产物 `/tmp/mui-il2cpp-build/MUIValidation.app` 包含 GameAssembly.dylib。构建后已确认后端恢复 Mono，编辑器不再构建或编译。
- 启动该 IL2CPP Player，首场景同步页面 Ready、Open Succeeded，未见 MissingMethod/ExecutionEngine/NullReference 异常；随后停止验收进程。日志 `/tmp/mui-il2cpp-player.log`。这是 Low 裁剪级别下首场景生成绑定/同步导航启动证据，不证明所有场景、所有泛型组合、视觉输入或其他平台。
- 两个标准对话框 Prefab 的显式构建目录只读校验通过：目录 1、页面 2、路由 2。其他程序化示例尚未建立完整资产目录，不声称全项目覆盖。
- 构建的 3 条警告包括未登记目录，以及两条 Samples~ 根目录孤立元数据警告。删除不应存在的 Samples~.meta，ensure-meta.py 跳过忽略目录根但保留内部可导入资产元数据。脚本重跑及差异格式检查通过；警告消除仍需后续构建复核。

### IL2CPP 泛型继承绑定与打包警告复核（2026-09-21）

- 将既有 SynchronousDependencies 场景放到首场景重新构建 IL2CPP，成功、0 errors、1 warning。Samples~ 根元数据两条警告已消失，唯一剩余项为未登记完整构建目录。
- 启动 AOT Player，两个父页面 Succeeded/Succeeded；ThingItemViewModel 继承 LabeledItemViewModel<string> 的绑定在实际 Player 中完成，共享创建次数 1、父拥有者数 2、状态 Open。随后停止验收进程。
- 产物 `/tmp/mui-il2cpp-dependencies/MUIValidation.app`，构建摘要 `/tmp/mui-il2cpp-dependencies-status.txt`，运行日志 `/tmp/mui-il2cpp-dependencies-player.log`。此证据覆盖该闭合泛型继承链，不泛化到全部泛型组合或完整交互。

### 独立同步虚拟列表修复与 Grid 复验（2026-09-21）

- 独立接线 SynchronousVirtualListDemo 发现未登记 ThingItemViewModel 绑定工厂，首项创建抛错并结束示例。现在由示例显式注册生成工厂，不依赖其他场景的初始化或框架运行时扫描。
- Unity 编译后重新进入 Play Mode，1000 条及 10000 条数据均物化 8 个单元格；定位第 500 项后首可见索引 494，切三列网格后首行索引 492、物化 27 个，状态 Ready。
- 清空来源后 Empty/0；结束父 Lifetime 后 Inactive/0。测试用的是已有示例和控件入口，没有新增测试文件。动态高度、多模板、原生输入和性能数据尚未由本轮覆盖。
- 场景 `/tmp/mui-unity-validation/Assets/MUI Samples/SynchronousVirtualList.unity`，日志 `/tmp/mui-validation-il2cpp-editor.log`。

### 同步变高、多模板及布局测量复验（2026-09-21）

- 复用既有 VirtualList 模板构造代码，以同步入口设置 1000 条显式变高、多模板数据。定位第 500 项 Ready，同键同模型从 featured 换回默认模板后仍 Ready。
- 将视口前第 0 项高度从 30 调为 130，首可见索引保持，内容偏移增加 100；切三列 Grid 后高度 23400、物化 18 个、首行索引 495，状态 Ready。
- 独立测量场景启用既有 VerticalLayoutGroup 模板与每帧测量预算，注入无显式高度的长短文本。真实帧循环后单元格高度约 36.93/146.12，物化数量由估算的 8 收敛为 5，保持 Ready。
- 测量场景 `/tmp/mui-unity-validation/Assets/MUI Samples/SynchronousMeasuredList.unity`。这是既有 API 的运行验收，未新增测试；字体切换、宽度变化、全量性能及异常回调组合仍未由此证明。

### 页面向导实际资产链路复验（2026-09-21）

- 在编辑器调用现有 PageWizard 创建入口，生成 Inventory 的 Scripts/Prefabs、类型化 Args/Result、Presenter、ViewModel 和 Page 路由入口，Unity 编译通过，生成 Manifest 对 Prefab 校验无问题。
- 用生成路由同步打开生成 Prefab，返回 Succeeded；Game View 截图确认标题显示为传入参数，关闭按钮执行后实例/待清理数均为 0。截图 `/tmp/mui-unity-validation/Assets/Screenshots/screenshot-20260921-145604.png`。
- 重复创建同名页面被拒绝，不覆盖原目录。通过临时文本后端模拟创建异常，向导移除本批目录，残留目录检查为 false；临时后端随后撤销，未新增测试文件。
- 产物位于 `/tmp/mui-unity-validation/Assets/MUI Wizard Validation/Inventory`。此次调用向导实际制作代码而非手写替代模板，但未验证窗口控件操作、已有 View 关联、TMP 模板及 Undo 的原生交互。

### 批量挂载异常回滚修正（2026-09-21）

- 源码复核发现 ElementAuthoringWindow 挂载失败只记录异常，仍保留部分组件并输出成功数量，与命名工具的整批回滚行为不一致。现改为异常时撤销当前 Undo 组、重新扫描并明确报告失败；Undo.AddComponent 返回 null 同样进入失败路径。
- Unity 编译通过，实际挂载 Button/Text 得到 2 个 Element，再次应用仍为 2，没有重复组件。
- Undo 未能取得有效运行证据：工具撤销返回的是选择记录，不是挂载记录。因此不宣称组件 Undo 或异常回滚已实际通过，后续须在可确认 Undo 组的原生制作流程复核。已清理本轮临时层级，未新增测试文件。

下表按新增记录倒序保留实现历史。早期记录中的“尚未接入”可能已由上方后续记录补齐；最终能力仍以当前源码及对应验证证据为准，历史编译或演示不能证明后续改动已经运行验收。

| 模块 | 实际实现 | 验证范围 |
|---|---|---|
| 共享依赖参数职责纠偏 | 保留 DependencyChangeRequired/InUse 冲突拒绝及单界面候选回滚，不增加自动跨拥有者参数事务；按主设计第 10.1 节既有规则，由项目显式选择独立路由变体。同步依赖示例补充不同参数的独立实例与正常关闭入口 | 对照 FUI Navigator.State 的取得/释放所有权实现及 MUI 参数冲突检查；Navigation 示例及依赖编译零警告/错误。此项为职责修订，不是联合事务完成；实例隔离与关闭顺序仍需运行验收。未新增测试或启动 Unity |
| 泛型模型及闭合工厂 | 生成类型参数和语义约束，支持泛型基类的继承绑定；泛型绑定/路由工厂由项目闭合使用，模块仅自动登记非泛型模型。默认泛型路由键使用闭合模型类型；Inspector 跳过开放工厂，类型参数纳入名称冲突检查。ThingItem 示例继承项目泛型基类，不增加资源或业务系统职责 | 生成器发布、Navigation.Editor 及依赖、最终 Navigation 示例、MUI.Editor 的 UNITY_EDITOR 编译均零警告/错误；核对泛型模型、约束、路由输出和非泛型派生注册。class 以外约束组合、错误诊断、跨程序集闭合继承及 AOT 未专项验收；泛型外层和泛型 Presenter 推导仍不支持。未新增测试、未启动 Unity |
| 最小项目依赖及验收副本同步 | 主包补充实际使用的 ScreenCapture/ImageConversion 内置模块依赖；JSONSerialize 仅作为 Resource Integration 示例前置要求。现有 /tmp/mui-unity-validation 更新 184 个示例文件，保留场景和既有 GUID，新增当前源码哈希快照 | 对照本机 Unity 2022.3.62f3 内置 package.json 核对模块 ID/版本，样例内容与 MUI 引用检查通过。只读查询当前连接为 fun-slg，MUI 验收项目未连接；未启动 Unity，未执行包解析、导入或运行。最小验收项目启用 JSONSerialize 仍待处理，不能使用旧场景存在或离线 DLL 作为运行通过证据 |
| 参数更新与换绑的依赖资格统一 | 准备前及提交前共用实例/所有权/取消/关闭请求/更新/换绑资格；参数工厂与比较器回调后立即复核，不再对已失效依赖继续进入候选准备。复用 Navigator.Mutations 内部状态检查，不新增事务服务 | Navigation 示例及依赖 Release 编译零警告/错误；同步和异步入口共用检查，无新增任务。未新增测试或启动 Unity，取消与控制通道竞态未运行验收；依赖参数联合切换仍未实现 |
| 非泛型嵌套 ViewModel 生成 | 模型、绑定上下文/工厂及 ViewRoute 工厂沿用每层 partial class 作用域，模块注册使用完整类型路径；同级冲突与跨程序集基类元数据检查沿用相同作用域；现有同步嵌套示例改为组件内模型 | 生成器发布成功；Navigation.Editor 及依赖、最终 Navigation 示例编译零警告/错误，核对嵌套模型的属性/绑定/路由输出及完整注册路径。泛型模型/外层仍不支持，非法声明及跨程序集嵌套继承分支未专项验收；未新增测试或启动 Unity |
| 持续软键盘布局适配 | SafeAreaFitter 可选读取键盘矩形并从已应用安全区/轴配置的区域中选择最大无重叠矩形；项目可覆盖区域，空矩形恢复布局，默认关闭；不管理键盘、输入内容或焦点，不创建异步任务 | UGUI 与 Navigation 示例及依赖离线编译零警告/错误；现有示例增加底部、浮动和收起流程。Android 2022.3 需平台注入区域，设备坐标、窗口调整与视觉效果仍未验收；不代表跨平台输入体系全部完成 |
| 页面向导统一路由生成 | ViewModel 声明 ViewRoute，显式选择向导 Presenter 和同步生命周期；Page 保留项目策略入口，改用生成的 Route.Create/Resource，不重复接线 BindingFactory 或资源键 | MUI.Editor 在 UNITY_EDITOR 下编译通过；复用现有离线导出工具生成 48 组页面、160 个源码文件并联合生成器编译，零警告/错误，覆盖 uGUI/TMP、同步/异步、全屏/弹窗、Presenter/类型化参数及特殊控件名。未新增测试、未启动 Unity，真实向导资产操作仍未验收 |
| ViewModule 程序集注册配置 | 新增程序集声明，配置生成注册类的命名空间与名称；无声明保持原默认；非法名称/类型冲突报 MUI001，注册调用按完整工厂名排序；Settings 使用显式生成入口 | 发布生成器、Settings.Editor 与未配置模块的 Navigation.Editor 及依赖 Release 编译零警告/错误；核对 Settings 生成注册包含两个工厂、发布 DLL 一致及新增 meta。未新增测试或启动 Unity；错误诊断分支、重复初始化和域重载运行验收待完成 |
| 项目控件命名规则 | Editor 提供 ElementNaming.SetRule/SuggestDefault，项目配置纯命名函数；建议计算改为整批候选发布，规则失败或层级变化保留原预览；实际写入沿用冲突校验与 Undo | MUI.Editor 及依赖在 UNITY_EDITOR 下 Release 编译零警告/错误；未新增测试或启动 Unity。规则初始化、异常预览和真实 Undo 仍待编辑器运行验收 |
| 子视图绑定工厂基类回退 | Create/GetManifest 共用最近注册基类解析；顶层 Route 默认使用 CreateExact 按声明模型类型创建，消除泛型绑定强转；新增注册推进代际，重复相同注册保持幂等 | Navigation.Editor 及依赖 Release 编译通过，零警告/错误；源码核对动态/静态子视图共用解析，工厂在锁外执行，不新增任务或解析缓存。未新增测试或运行 Unity；真实派生模型绑定及缓存失效运行验收待完成 |
| 职责收缩与项目实现分离 | 删除资源预算/共享加载/全局回收；主题与本地化改为目录注入；驻留 Prefab 提供方和分页请求实现移至示例；资源槽成为 uGUI 内部类型 | 中间快照全部 37 个构建项目通过；最终目录收缩后 Navigation.Editor 及依赖重新编译通过。37 个 asmdef 引用与 meta 完整性通过；Runtime 不依赖 Samples，主题/本地化仅依赖 Core。未新增测试或启动 Unity；旧文档逐段迁移与运行验收仍需完成 |
| 资源策略程序集解耦 | 共享同步/异步加载器、预算包装器及预算账本共 10 个源文件移至 Adapters/ResourcePolicies，保留原 meta；新增显式引用的 MUI.ResourcePolicies，Navigation 示例更新引用；后端回滚异常开放构造并校验参数 | Navigation.Editor 及依赖 Release 编译零警告/错误；检查基础运行时无策略程序集引用、移动文件 meta 完整。未启动 Unity、未新增测试。全局内存协调仍待解耦，本次不代表全部职责整改完成 |
| 页面资源键单一来源 | 生成绑定工厂公开 ViewContract 资源键，拒绝空契约键；页面向导通过生成工厂常量构造 Resource | 生成器发布、MUI.Editor（UNITY_EDITOR）及 Settings.Editor 离线编译通过；未执行 Unity 向导生成及资产交互验收 |
| TMP 字体资源槽释放前提复核 | 对照 TMP_Text.font、TextMeshProUGUI.LoadFontAsset/ClearMesh 与 TMP_TextInfo.Clear，确认 null 回退及网格/文本清理不能证明字体引用解除；明确现有 FontAsset 仅借用且与材质槽互斥 | 本轮仅源码核对与资源接入文档修正，未修改运行时代码、未重复编译或启动 Unity；FontSource/字体资源槽仍未实现，需要完成所有权及原生引用释放闭环，不能沿用 uGUI Font 清空规则 |
| OneTime 绑定完整接线 | 追加枚举值保持旧模式编号，首次正向写入后不订阅两端变化；生成器、清单、运行时/编辑器写入冲突检查一致；资源键策略允许 OneTime，Settings 字符上限示例接入 | 发布生成器、Settings.Editor 与 MUI.Editor 及依赖 Release 编译零警告/错误；核对生成委托无反向读写且模式为 OneTime，DLL 一致；未新增测试或启动 Unity，重新绑定、资源键生命周期及冲突诊断运行验收未完成 |
| 控件接口继承绑定查找 | 属性、事件及交互状态查找支持父接口；菱形同一成员去重，最近声明遮蔽祖先，独立同名声明拒绝歧义 | 发布生成器编译独立 Base/ Page DLL 示例，覆盖父接口属性/事件与菱形来源，再完成 Navigation.Editor 及依赖 Release 编译，均零警告/错误；发布 DLL 一致；未新增测试或启动 Unity，歧义诊断分支与运行交互未专项验收 |
| 计算属性显式依赖通知 | 新增 NotifyPropertyChangedFor 字段声明，生成 setter 在实际变化及回调后发布目标属性通知；校验目标、重复、自身和来源，沿用批量去重；资源示例 FontCaption 改为计算属性 | 生成器发布及 Navigation.Editor 依赖 Release 编译零警告/错误，核对生成 FontKey setter 包含 FontCaption 通知，发布 DLL 一致；未新增测试或启动 Unity，诊断分支及批量/重入通知尚未专项运行验收；不实现依赖图自动推断 |
| 动态自动换绑的类型与命令边界 | 自动复用要求模型运行时类型相同，避免不同契约遗留控件值；CanRebind 排除自身命令执行链，内容命令更新继续走候选替换；核对关闭经 EndActivationAsync 等待 Rebinding 后清理绑定与激活资源 | Navigation.Editor 及依赖 Release 离线编译零警告/错误；未新增测试或启动 Unity，类型切换、命令内更新和关闭交错仍待运行验收 |
| 动态子界面原实例换绑 | 同资源键、同提供方、版本有效且首次绑定完成时，DynamicViewElement 分别调用槽的同步/异步换绑；其他情况保留候选替换；当前槽变化清除提供方归属，版本查询回调后复核配置 | Navigation.Editor 及依赖 Release 离线编译零警告/错误；同步与异步示例加入活动 View 身份对照日志；未新增测试或启动 Unity，实际实例复用、模型类型变化、失败恢复及连续切换尚未运行验收 |
| 子视图槽同步/异步换绑入口 | ChildViewSlot.Rebind/RebindAsync 对明确目标换绑；异步复用现有请求队列、取消和排空，执行时重新验证当前句柄；同步直接调用既有生命周期；不对原实例换绑进行候选超时隔离 | Navigation.Editor 及依赖 Release 离线编译零警告/错误，新文件 meta 与格式检查通过；未新增测试或启动 Unity，换绑/替换/清空交错未运行验收；DynamicViewElement 自动复用尚未接入 |
| 动态内容准备结果归属 | 异步 Refresh 捕获槽与请求代际，回调已切换激活或推进新请求时只观察旧任务，不覆盖当前 PendingChange/Preparation；同步路径仍直接执行 | Navigation.Editor 及依赖 Release 离线编译零警告/错误；源码检查捕获与发布顺序，未新增测试或启动 Unity，激活切换与重入时序尚未运行验收；原节点换绑仍需纳入槽的串行协调，未直接绕过槽调用句柄换绑 |
| 动态内容同值更新与准备错误一致性 | SetContent 同资源键/模型引用且无失败时不重建；取消准备也允许同值重试；异步 Ready 携带的旧内容清理错误不再被忽略，与同步路径保持一致 | Navigation.Editor 及依赖 Release 离线编译零警告/错误；源码核对同步直接状态分支，未新增测试或启动 Unity；重建次数、取消重试和清理失败仍待运行验收，真正更换模型的原节点换绑尚未接入 |
| 当前工作树全量离线编译与归属复核（2026-09-21） | 37 个项目全部 Release 编译；595 个 C# 文件与最近 asmdef/Compile 归属逐一对应，720 个元数据 GUID 无缺失/孤立/重复；发布生成器与源码构建一致；返回适配补同帧约束与已消费状态快照 | 37/37 项目零警告/错误；日志与 results.json、metadata.json、ownership.json：/var/folders/x3/gp7v18_s5_g1366bq0pcb4qc0000gn/T/mui-build-current-91yj_03a；源码核对 Unity 输入模块事件复用，未新增测试或启动 Unity；不覆盖全部 Unity 条件编译、运行交互、Player/IL2CPP、性能与内存，不代表整体设计已完成 |
| 返回适配接入导航示例 | Confirm/Close 在绑定时连接 UIBackInput 与宿主，重复绑定复用组件，自动演示禁用人工返回；同适配器已消费 Cancel 优先于同帧全局回调 | Navigation.Editor 及依赖 Release 离线编译零警告/错误；未新增测试或启动 Unity，人工输入、事件顺序和焦点迁移仍待运行验收 |
| EventSystem 返回适配 | 可选 UIBackInput 接收选中控件 Cancel 或项目按下回调，LateUpdate 复核消费、焦点与宿主后转发；禁用清除请求，同步不创建任务 | Navigation.Editor 及依赖 Release 离线编译零警告/错误，已补 meta 和接入边界说明；未新增测试或启动 Unity，事件消费顺序和真机输入尚未验收；原生输入框/下拉框、平台映射与软键盘仍需专项适配 |
| 跨程序集生成绑定元数据 | 生成属性复制 Bind/BindCommand 并显式保留 ElementType；模型标记元数据版本，引用程序集只读取公共属性声明，避免重复私有声明；旧基类版本明确诊断，手写属性需显式 ElementType | 发布 Analyzer 编译独立 Base.dll 后，由 Page 工程仅通过 DLL 引用编译派生模型；两次编译零警告/错误，派生清单包含 Title 与 CloseCommand 两条绑定；Navigation.Editor 及依赖离线编译通过，发布 DLL 匹配；未新增测试或启动 Unity，跨程序集转换器/复杂层级与运行绑定尚未验收 |
| 同程序集继承绑定合并 | 按基类到派生类合并源码中的属性与命令绑定，继承绑定通过声明类型访问，不重复生成成员；共用正向、反向和命令事件冲突检查；遮蔽或重写绑定来源、跨程序集生成基类明确诊断；Navigation 示例把标题与关闭移入抽象基类 | Navigation.Editor 及依赖 Release 离线编译通过，0 警告/错误；核对派生上下文和清单含基类 Title/CloseCommand，发布 DLL 匹配源码产物，新文件 meta 与源码空白检查通过；未新增测试或启动 Unity，冲突诊断分支、深层继承和运行绑定尚未专项验收 |
| 抽象 ViewModel 成员生成 | 允许抽象顶层非泛型模型生成公共可观察属性、变化回调和已实现的命令；具体模型继承成员，工厂仍只绑定已有实例，不实例化抽象类型；基类绑定合并仍明确拒绝 | 使用发布 Analyzer 独立编译抽象基类与派生模型示例，核对生成属性、回调调用和同步命令；Navigation.Editor 及依赖 Release 编译均零警告/错误，发布 DLL 与源码产物一致；未新增测试或启动 Unity，不代表继承绑定合并或运行交互已验收 |
| Prefab 提供方结束时断开配置引用 | 常驻提供方清除实例配置回调；同步加载提供方结束时清除借用加载器、键解析和配置回调；异步提供方排空创建与回滚后清除相同引用，外部凭证仍保持独立释放权 | Navigation.Editor 示例及依赖 Release 离线编译通过，0 警告/错误；核对凭证释放不依赖上述字段，未新增测试或启动 Unity；实际对象回收与异步交错尚未运行验收 |
| 资源准备等待前终态复核 | 收集未完成任务后再次检查当前准备账本，避免收集前刚失败的任务被遗漏、进而受另一项慢加载阻挡；取消仅唤醒共享变化信号，退出仍由原令牌检查决定 | Navigation 示例及依赖 Release 离线编译通过，0 警告/错误；源码检查两次状态读取间的完成窗口，未新增测试或启动 Unity，后台完成与取消交错尚未运行验收 |
| 首帧准备记录聚合 | 任务与诊断名称合并为单个 Preparation 记录，换键仅替换当前任务，消除两份字典同步维护；等待者集合按需创建并在最后一个等待退出时解除引用；已提交或结束激活不再为新资源槽创建准备追踪回调 | MUI.Editor 及依赖 UNITY_EDITOR Release 离线编译通过，0 警告/错误；源码检查换键、提交、释放与等待者退出路径，未新增测试或启动 Unity，不代表分配量和运行竞态已实测 |
| 首帧资源准备只读诊断 | View 提供有上限的资源准备快照，按控件 Source 属性记录当前任务状态与全部计数，条目不持有 Unity 对象/任务/异常；Inspector 独立手动采集、显示截断及无上下文提示；标签在配置时截断至 256 字符，提交/释放清除标签账本 | MUI.Editor 及依赖 UNITY_EDITOR Release 离线编译通过，0 警告/错误；检查新文件 meta、任务完成后才读取结果、同步无准备账本路径；未新增测试，未启动 Unity，实际等待/失败快照与截断显示尚未运行验收 |
| 宿主参数在原生创建前校验 | 同步初始化先检查缓存容量、预加载容量及字节预算；两个初始化入口在创建默认 Provider 前检查内存协调器模式，避免可提前拒绝的配置触发 staging 节点创建和回滚 | Navigation.Editor 示例及依赖 Release 离线编译通过，0 警告/错误；源码核对与 Navigator 参数边界一致，未新增测试或启动 Unity；无效参数与原生层级回调场景未运行验收 |
| 模型通知中的命令刷新去重 | 模型变化直接刷新当前绑定，不再从每条绑定广播命令状态；保留刷新异常隔离、重入收敛与命令自身状态事件，减少共享命令多按钮场景的重复求值 | Settings 示例及依赖 Release 离线编译通过，0 警告/错误；源码检查模型通知与命令事件两条路径，未新增测试；Unity 多按钮、条件求值异常和实际刷新次数未运行验收 |
| Inspector 首帧等待范围提示 | 选定契约后，根据 WaitForResourceSources 与内置 Source 声明数量展示准备范围；无声明时说明手动槽/直接资源/业务任务不自动等待；折叠资源列表仍可见，并补充 UIHost 默认目录配置入口 | MUI.Editor 及依赖 UNITY_EDITOR Release 离线编译通过，0 警告/错误；未新增测试，未启动 Unity；Inspector 实际重绘与操作未验收，不查询运行时资源就绪状态 |
| 生成命令实现与基类待生成名称检查 | 名称预留纳入基类 ObservableProperty/Command 将生成的公共属性，命令命名复用同一辅助函数；拒绝没有实现的 partial void 命令，避免空操作命令 | 生成器发布及 Navigation 示例与依赖 Release 离线编译通过，0 警告/错误；发布 DLL 与源码产物 SHA-256 一致；未新增测试，名称冲突和缺失实现的诊断分支未专项执行，不代表继承绑定合并已实现 |
| 子控件准备等待取消 | View 记录每次激活令牌，等待子控件准备时同时观察调用者与激活取消，取消先结束等待但保留底层任务的原所有权；准备/提交协调移入 View.Preparation.cs，同步查询不调用异步等待器 | Navigation.Editor 示例及依赖 Release 离线编译通过，0 警告/错误；核对 NestedView、普通列表和虚拟列表已有任务观察入口，补齐新文件 meta；未新增测试，未启动 Unity，不合作加载、取消后清理与关闭耗时仍待运行验收 |
| 首帧准备的激活归属复核 | 资源上下文明确区分提交与释放，查询前检查所属 Lifetime 取消；等待登记自身激活取消以唤醒，不能因账本释放变空而返回成功。View 准备/提交固定开始时的子作用域与资源上下文，并在子准备查询、await 及提交后复核，不跨缓存激活继续执行 | Navigation.Editor 示例及依赖 Release 离线编译通过，0 警告/错误；核对独立示例均先 BeginChildActivation 再 Commit，未新增测试；未启动 Unity，关闭、缓存重开和多等待者取消仍待运行验收 |
| View 首帧资源键准备 | 增加 WaitForResourceSources 显式开关，将默认加载器的图标/纹理/材质/uGUI 字体键操作接入现有 View 隐藏准备；按槽跟踪最新请求，变化/取消唤醒复核，提交清除账本，后续更新仍渐进；同步加载不创建或读取任务；资源示例增加等待开关 | Navigation.Editor 示例及依赖 Release 离线编译通过，0 警告/错误；源码核对同步查询、生成 Source 接线及导航 RequiresAsync 复用；未新增测试，未启动 Unity；换键、失败、多个等待者、取消与首次显示仍待运行验收，不覆盖 TMP 字体或任意业务后台任务 |
| UIHost 初始化重入与销毁边界 | 两种初始化入口共用直接初始化标记，finally 解除；创建默认目录后复核销毁/退出状态，失败沿用提供方回滚；未交付 Navigator 的 OnDestroy 仅记录销毁，不启动异步 Shutdown | Navigation.Editor 示例及依赖 Release 离线编译通过，0 警告/错误；源码检查嵌套初始化拒绝、回滚归属与无任务退出分支，未新增测试；未启动 Unity，层级回调内销毁及失败重试仍待运行验收 |
| UIHost 初始化提供方回滚 | 异步模式改为 Navigator 构造成功后接管外部提供方，失败时归调用者；内部默认提供方在构造失败时同步回滚，两个初始化入口共用保留初始化与清理双异常的回滚方法，不创建任务 | Navigation.Editor 示例及依赖 Release 离线编译通过，0 警告/错误；源码核对默认提供方 IDisposable 和所有权提交顺序；未新增测试，未启动 Unity，构造失败、重试与原生 staging 销毁尚未运行验收 |
| UIHost 默认目录实例配置 | Initialize/InitializeSynchronous 增加 configureDefaultView，直接传入默认 PrefabViewProvider 的现有配置回调，在新根 View 激活前统一接入加载器；外部 provider 与默认目录回调同时传入时明确拒绝，不增加提供方包装或加载器所有权 | Navigation.Editor 示例及依赖 Release 离线编译通过，0 警告/错误；源码确认配置复用 PrefabViewFactory 的隐藏创建、资格复核及回滚，同步路径直接调用；未新增测试，未启动 Unity，Inspector 目录实际打开、缓存复用与资源释放仍待运行验收 |
| Inspector 绑定目标定位 | 逐项定位属性/命令的目标控件，复用校验器的 View/Element 边界；唯一匹配 Ping，缺失提示，歧义列出全部对象；查询仅在点击时扫描，层级变更及契约切换清空快照，禁用 Inspector 时退订层级事件 | MUI.Editor 及依赖 UNITY_EDITOR Release 离线编译通过，0 警告/错误；源码检查边界复用与事件退订，未新增测试，未启动 Unity；实际 Prefab/场景 Ping、歧义列表与层级通知仍待运行验收 |
| 绑定声明源码定位 | BindingEntry 保留旧构造并增加可选位置元数据；生成器在 UNITY_EDITOR 分支记录 Bind/BindCommand 声明的路径及行号；View Inspector 展示成员到控件映射并通过 AssetDatabase 或外部编辑器打开源码，旧清单和失效文件给出提示 | MUI.Editor、Navigation.Editor 示例及依赖的 UNITY_EDITOR 构建与 Navigation 默认构建均通过，0 警告/错误；核对生成行号及编辑器 DLL 中的路径常量，默认 DLL 中该常量不存在。补齐离线 UGUI 工程的条件 UnityEditor.CoreModule 引用；未新增测试，未启动 Unity，点击跳转及实际 Player 构建未验收；晚于下方全量快照 |
| OnChanged 与 Tab 渲染修订后的全量离线复核 | 顺序构建 37 个项目，对照 37 个 asmdef 核对 587 个运行时、编辑器与示例 C# 文件的 Compile/Exclude 和最近程序集归属；核对 711 个唯一元数据 GUID、包示例目录与 Analyzer 标记；发布生成器与源码产物一致 | 37 份独立 Release 日志全部成功且均为 0 警告/错误，无源码归属或元数据差异。日志：/var/folders/x3/gp7v18_s5_g1366bq0pcb4qc0000gn/T/mui-build-audit-3prkh2hp，results.json 含全部结果；生成器 SHA-256 为 7fcb724d48dcc8df1f1eea46f11ccef2bfeed0f86b3a0127eda5bfe67d244df8。未新增测试或启动 Unity；不覆盖运行回调、Unity 导入、Player、IL2CPP 与性能，也不证明仍缺失的设计能力已完成 |
| 生成属性变化回调 | 增加 OnChangedAttribute 与独立生成处理，支持无参数、新值、旧值与新值的同步实例方法；同值赋值跳过，非法目标和签名明确诊断，不创建任务；资源绑定示例通过 FontKey 回调更新 FontCaption | 生成器发布及 Navigation 示例与依赖 Release 离线编译通过，0 警告/错误；核对真实生成的新值回调及文字绑定，发布 DLL 与源码产物一致；未新增测试，零/双参数、诊断、重入、异常和 Unity 显示未专项运行验收 |
| 继承绑定遗漏的编译诊断 | 生成器在模型准入时遍历基类，发现 Bind/BindCommand 即报告 MUI001 和成员位置说明，避免派生契约静默丢失基类绑定；普通状态与逻辑继承保留，完整继承绑定合并仍未实现 | 生成器发布及 Navigation 示例与依赖 Release 离线编译通过，0 警告/错误；发布 DLL 与源码构建产物 SHA-256 一致；未新增测试，继承诊断分支及跨程序集场景未专项执行 |
| 动态 Tab 嵌套刷新合并 | 渲染回调内的更新使旧轮失效并记录待刷新，由 LateUpdate 处理，避免递归 Instantiate；换绑期间创建的按钮先登记清理归属，排序独立于条目同步并只移动未就位的直接子节点；全程不创建任务 | Tabs 示例及依赖 Release 离线编译通过，0 警告/错误；未新增测试，未启动 Unity，层级回调换绑及跨帧收敛尚未运行验收 |
| Tab 按钮渲染与导航保护 | TabBarElement 为原生控件写入及动态条目增删、排序增加渲染代际检查，失效候选不再加入集合；删除后的重复清理与回调销毁使用存活检查；导航排除隐藏按钮，焦点回退尊重其他已选中对象 | Tabs 示例及依赖 Release 离线编译通过，0 警告/错误；未新增测试，未启动 Unity，动态模板原生回调递归、选中恢复及销毁时序仍待运行验收 |
| Tab 内容提示渲染重入保护 | AsyncContentElement 在加载遮罩启停、错误文字启停及文字更新后复核渲染代际、存活与画面保留状态，阻止旧渲染覆盖回调中发布的新状态；访问错误文字前重新检查原生组件存活，不引入任务 | Tabs 示例及依赖 Release 离线编译通过，0 警告/错误；未新增测试，启停回调换绑、销毁与画面保留尚未进行 Unity 运行验收；不代表 TabBar 动态按钮渲染已完成同类验收 |
| Tab 控件模型与控制器换绑一致性 | TabBarElement 和 AsyncContentElement 直接更换 ViewModel 时解除不匹配的旧控制器，避免显示 B 状态却向 A 发送选择/重试；同模型赋值保留直连，Bind 新控制器继续同时接入模型，解除后沿用请求事件 | Tabs 示例及依赖离线编译通过，0 警告/错误；源码核对 Bind 顺序及两个控件事件回退路径，未新增测试；模型换绑后的按钮和重试交互尚未 Unity 验收 |
| 资源绑定示例场景入口 | 增加 Open Resource Binding Scene，与奖励列表共用独立场景创建/保存方法，保存后选中示例对象以配置同步模式及延迟；播放切换期间拒绝创建，导航场景菜单补齐相同保护 | Navigation.Editor 示例及依赖离线编译通过，0 警告/错误；源码核对保存提示、唯一场景路径及不自动播放；未新增测试或实际创建场景，Unity 菜单和播放交互尚未验收 |
| 异步资源演示接入字体 | ResourceImageDemo 增加 FontPreview 和生成 FontSource 绑定，Next/Fail/Retry/Clear 同时操作图标、纹理及字体；字体沿用延迟和忽略取消路径，Warm/Cool 均借用内置字体，凭证归还不销毁共享字体；状态显示已提交 Font 键 | Navigation 示例及依赖离线编译通过，0 警告/错误；检查字体生成委托和 Manifest，未新增测试；稳定三个凭证、迟到归还、失败保留及关闭归零仍为待 Unity 验收预期，不证明字体外观切换 |
| 异步资源取消语义源码核对 | 对照 ReplaceCoreAsync、ResourceLoadCleanup 与 Source 观察器整理直接槽调用的结果表，区分取代返回 false、所有者/调用方取消、普通加载失败及回滚失败；明确 Source 绑定与直接调用者的错误处理职责 | 本轮为源码审查与文档整理，未修改运行时代码、未新增测试或重复编译；取消竞争、迟到凭证回收与后端回滚失败仍待运行验收 |
| 字体演示的界面操作入口 | 自动创建的奖励列表底部并排提供 Add reward、Clear font、Retry font、Fail font，直接复用现有纯同步方法；公共按钮创建方法负责登记及随 Lifetime 解除监听，手工场景仍可使用组件菜单 | Navigation.Editor 示例及依赖离线编译通过，0 警告/错误；源码核对按钮占位、回调与监听清理，未新增测试；实际画面、点击、失败提示和资源计数仍待 Unity 验收 |
| 生成绑定控件类型准入 | 属性和命令绑定共用 ValidateElementType，检查引用类型、封闭泛型和独立顶层上下文可访问性；避免值类型 IElement 或私有嵌套控件生成无法编译的代码 | Navigation 示例及生成器离线编译通过，0 警告/错误，发布 DLL 已同步；未新增测试，非法类型诊断分支尚未专项验证 |
| TMP 字体直接赋值依赖准入 | FontAsset 切换非空字体前检查首张图集及默认材质，避免原生 setter 先写入字体再因缺失依赖失败；沿用材质槽独占和 null 默认字体语义，不将 null 冒充字体资源解除引用 | 对照本地 TMP 3.0.7 的 TMP_Text.font、LoadFontAsset 和 atlasTexture 源码；TMP.Editor 及依赖离线编译通过，0 警告/错误。未新增测试，缺失依赖拒绝、默认字体回退及原生渲染尚未运行验收，TMP 字体资源槽仍未完成 |
| 字体清空、加载失败与同键重试演示 | 纯同步奖励列表示例增加首项字体清空/恢复/重试及一次性故障菜单，RewardItem.RefreshFont 显式通知同键绑定；加载器计数字体失败，故障标记在同步请求结束后复位，不污染后续条目；沿用资源凭证状态显示 | Navigation 示例及依赖离线编译通过，0 警告/错误；源码核对同值通知和故障复位，未新增测试；失败保留旧字体、清空与恢复的画面和凭证计数均为待 Unity 运行验收预期 |
| 资源槽最终清理解除外部引用 | 同步/异步最终清理在目标清空并处理凭证归还后，共用清理加载器、赋值委托、外部令牌和最后请求引用；清空目标失败保留原状态，清理诊断仍可查询 | Resources 及 Core 离线编译通过，0 警告/错误；源码核对最终回调排空顺序、同步路径和销毁后变更拒绝；未新增测试，引用回收和取消竞态未运行验证，保留的诊断异常仍可能包含项目引用 |
| 控件资源持有者成功清理后的引用解除 | ElementResourceOwner 在槽成功释放并解除控件独占后清空 Source 通知、Lifetime、键及槽引用；失败仍保留原清理状态。异步 Source 观察器改为静态方法并显式捕获本次 Lifetime，取消过滤不再读取已清空字段 | Navigation 示例及依赖离线编译通过，0 警告/错误；源码核对同步/异步成功出口及失败前保留行为，未新增测试；未进行 Unity 内存测量或取消竞态验收，不代表外部持有 ResourceSlot 时其引用也已解除 |
| 奖励列表接入字体资源生成绑定 | RewardItem 增加 FontKey → TextElement.FontSource；纯同步示例加载器扩展为 RewardResourceLoader，图标由示例创建，DefaultFont 借用 Unity 内置字体；字体凭证只计数归还，不销毁共享字体。父加载器继承、条目回收及资源计数沿用已有演示入口 | Navigation 示例及依赖离线编译通过，0 警告/错误；检查实际生成的字体赋值委托和清单，并阅读本地 uGUI Text.font 的跟踪解除/重建语义。初始六个凭证及关闭后零凭证均为待运行预期，未新增测试或启动 Unity；不证明缓存、原生字体内存或渲染已验收 |
| uGUI Text 字体资源槽 | TextElement 增加 FontSource、同步/异步配置及显式字体槽，复用 ElementResourceOwner 和 View 加载器继承；Font 直接赋值检查槽独占，绑定策略识别 Font/FontSource 写入冲突，销毁解除原生借用；Inspector 显示 Font 资源类型，增加独立 FontResourceViewModel 示例 | Editor、Navigation 示例及依赖离线编译通过，0 警告/错误；检查实际生成的 FontSource 委托与 Manifest，补齐 meta；未新增测试，字体显示、替换失败及释放尚未 Unity 运行验收；不覆盖 TMP 字体、字体材质原子替换或本地化布局协调 |
| 自动生成成员的继承名称保护 | 名称保留集纳入可访问基类成员，属性、命令及后备字段复用现有冲突诊断；避免自动生成成员意外遮蔽通知事件、SetProperty 或项目基类命令；不可访问的私有基类成员不占用名称 | Navigation 示例及依赖、生成器 Release 编译通过，0 警告/错误，发布 DLL 同步；未新增测试，继承名称冲突诊断分支尚未专项验证；此次修改晚于下方全量编译快照 |
| 生成器、编辑器及列表修订后的全量离线复核 | 顺序构建当前 37 个离线项目，并核对 37 个 asmdef、584 个运行时/编辑器/示例 C# 文件的最近程序集归属与项目 Compile/Exclude；核对上述目录 708 个唯一元数据 GUID；发布生成器与源码 Release 产物一致 | 37 个项目全部 Release 编译通过，每份日志均为 0 警告/错误；没有源码编译归属差异、缺失或孤立 meta、重复 GUID。日志位于 /var/folders/x3/gp7v18_s5_g1366bq0pcb4qc0000gn/T/mui-full-build-xcb2xadm，results.json 含 37 个独立结果。未新增测试或启动 Unity，不覆盖原生交互、Player、IL2CPP 与性能验收；这是本次工作树快照，后续修改需按影响范围重新验证 |
| 虚拟列表入口的所属线程前置检查 | 列数、数据源、选择、重试、分页、测量与配置入口共用 RequireListAlive，初始化后先拒绝错误线程再访问 Unity 存活状态或修改订阅；FirstVisibleIndex 同样检查，活跃集合通知在进入状态失败处理前检查 | Navigation 示例及依赖离线编译通过，0 警告/错误；源码核对入口与公共检查顺序，未新增测试；后台线程通知仍需运行验证；初始化前配置仍要求调用者遵守 Unity 主线程约定，不宣称支持跨线程数据绑定 |
| 虚拟 Grid 换列锚点与回调复核 | 换列时将旧行内偏移上限限制为候选行高，容量准入和实际滚动使用同一个候选偏移；提交列数时先失效可见范围，再在原生尺寸及滚动写入后复核存活与来源代际，回调已提交新布局则停止旧候选后续处理 | Navigation 示例及依赖离线编译通过，0 警告/错误；源码对照测量更新已有的偏移限制与代际检查，未新增测试；变高换列、原生回调重入和视口位置仍待 Unity 运行验收；不承诺原生回调已发生副作用的事务回滚 |
| 虚拟 Grid 几何查询一致性 | FirstVisible 与 GetRange 共用非有限坐标拒绝逻辑，避免 NaN/Infinity 经整数转换产生无效首项索引；行数查询统一拒绝负数量并检查变高索引行数，范围查询不再仅依赖之前的 ContentHeight 校验 | Navigation 示例及依赖离线编译通过，0 警告/错误；源码检查空列表、尾行及固定/变高查询共用边界，未新增测试；异常坐标、超大数据量和 Unity 滚动行为尚未专项运行验收 |
| 列表刷新任务的本轮引用保护 | 普通回收列表与虚拟列表在发布异步刷新边界时保留局部任务引用，启动操作后只观察本轮任务；生命周期清理清空 pending 或回调替换 pending 时，不再空引用或观察另一轮刷新。修改仅位于异步分支 | Navigation 示例及依赖离线编译通过，0 警告/错误；源码核对两类 pending 清理位置与任务观察位置，未新增测试；关闭重入和立即完成回调尚未在 Unity 运行验收 |
| Inspector 契约显式选择 | 多个生成契约时默认未选择，不再以字母排序首项作为当前 View 契约；未选择时禁用绑定校验并隐藏资源清单，结构及不依赖契约的无障碍检查保留；单契约自动选择，切换时清除相关旧结果 | Editor 及依赖离线编译通过，0 警告/错误；源码复核占位项与清单索引偏移、无选择访问路径，未新增测试；Unity Inspector 下拉交互尚未运行验收 |
| 资源继承的 Inspector 接入提示 | 资源键绑定面板显示当前层级最近父 View 的只读引用，区分关闭继承、无父节点和可继承候选；明确独立 Prefab 挂载后重新解析及纯同步后端要求，继承字段增加中文 Tooltip | Editor 及依赖离线编译通过，0 警告/错误；源码检查使用 Unity 空判断、禁用引用编辑且不加载资源；未新增测试，Inspector 实际显示及 Prefab 阶段交互未在 Unity 验收 |
| 接入文档与资源继承现状校正 | README 同步示例先检查 OpenOutcome.IsSuccess 再操作句柄，提醒处理关闭拒绝；增加 ThingItem/列表条目的父加载器继承与独立资源归属说明；修正架构文档旧的“不继承”描述，并将历史全量编译数字标注为历史证据 | 对照 View.Resources 和 NavigationOutcomes 源码核对，重新统计当前 37 个构建项目与 37 个 asmdef；本轮仅修改文档，未新增测试或重复编译，不新增 Unity 运行验收结论 |
| 生成器源码职责整理 | UIGenerator 保留候选收集及模型输出，Commands 负责命令输出，Symbols 集中 Element 类型推断、继承查找、访问器资格及 CanExecute 解析；整理空白格式、查找方法控制流和中文职责注释，不新增运行时模块 | Navigation 示例及依赖离线编译通过，0 警告/错误；本次编译目录中的 10 份生成源码重构前后逐文件哈希一致，最终生成器重新编译并同步 DLL；项目模式格式化超时后改用文件模式完成；未新增测试，不能据此证明全部契约分支或 Unity 运行行为 |
| 绑定转换器的类型可访问性 | 转换器实例化前按独立顶层 BindingContext 的程序集权限检查类型，拒绝不可访问的嵌套类型及未指定参数的泛型类型，在原始绑定声明报告 MUI001；保留精确接口匹配和公共无参构造要求 | Settings 示例及依赖离线编译通过，0 警告/错误；未新增测试，现有 Settings 示例不使用转换器，因此此编译不证明转换器访问权限各分支已验收；Unity 运行未验证 |
| Element 绑定成员遮蔽检查 | 属性、命令事件和交互属性共用 FindElementMember，在最近同名声明停止；派生类的字段或其他成员不再被跳过，避免校验基类成员却生成派生类访问 | Navigation 示例及依赖离线编译通过，0 警告/错误；检查 GraphicResource 的继承属性绑定及 RewardItem 的同步命令生成结果，生成器 DLL 已同步；未新增测试，遮蔽错误分支尚未专项验证，Unity 交互未验收 |
| 命令条件的继承成员解析 | CanExecute 从命令声明处使用 Roslyn LookupSymbols，支持可访问的基类条件，遵循成员遮蔽并单独检查属性 getter 权限；同步和异步命令共用解析，不新增运行时抽象 | Settings 示例及生成器依赖离线编译通过，0 警告/错误；检查当前生成文件保留私有 CanSave 属性调用和纯同步 ResetVolumeCommand；未新增测试，跨程序集继承、遮蔽和不可访问 getter 分支尚未专项验证，Unity 交互未验收 |
| 列表架构文档现状校正 | 对照当前列表、测量、模板、分页与增量路径源码，移除只支持单模板/固定高度、所有通知全量刷新等过期结论；纠正 FixedGridLayout 为实际 VirtualGridLayout；增加普通/虚拟列表接入表、同步重试及功能边界，早期 Unity 演示数据明确为历史记录 | 本轮仅修改文档，逐项核对实际类型、方法与增量准入条件；未新增测试、未重复编译，也不将源码存在或旧运行记录视为当前 Unity 验收通过 |
| 列表订阅立即通知后的提交复核 | 普通/虚拟列表在 Changed 订阅返回后复核存活、订阅身份、当前来源及快照代际；回调已提交较新快照时不再覆盖为订阅前的候选，虚拟列表同时丢弃旧候选几何；订阅期间关闭或换源时停止旧提交 | Navigation 示例及依赖离线编译通过，0 警告/错误；源码检查复核位于候选提交前、两类代际更新与元数据/格式；未新增测试，Unity 自定义集合订阅立即通知尚未运行验收；本项不代表任意集合枚举器或事件访问器副作用均已支持 |
| 虚拟列表最终清理异常隔离 | 最终 Dispose 复用来源/引用清理，随后独立执行原生滚动监听移除、状态节点更新和逐个池节点释放，汇总错误；先移出池再释放节点，防止重入沿旧工作集继续访问；同步刷新回调中销毁时保留已发布完成信号给原执行器收尾 | Navigation 示例及依赖离线编译通过，0 警告/错误；源码检查每段异常隔离、节点逐项释放、父清理回调身份和元数据/格式；未新增测试，Unity 退订异常、原生回调直接销毁和资源最终释放未运行验收 |
| 虚拟列表来源通知隔离 | 每次集合订阅持有独立身份，身份匹配后才进入版本/数量检查与增量处理；换源、重新激活、最终释放与父激活清理都在事件移除前撤销身份，移除原委托；同集合重新绑定也不接受上一轮通知 | Navigation 示例及依赖离线编译通过，0 警告/错误；源码检查所有集合订阅与退订入口、增量处理前身份条件、元数据与格式；未新增测试，Unity 中旧来源迟到通知、巧合相同版本和同来源重新绑定尚未运行验收 |
| 普通列表来源订阅身份隔离 | 每次集合订阅创建独立身份与对应委托，回调匹配当前订阅后才处理；退订前清空身份，移除准确的旧委托，旧集合排队通知或同集合上一轮订阅均不能触发新来源刷新 | Navigation 示例及依赖离线编译通过，0 警告/错误；检查订阅身份捕获、退订失效顺序及元数据/格式；未新增测试，Unity 自定义来源迟到通知与同集合重新绑定尚未运行验收；来源若拒绝退订仍报告失败，本检查不保证外部委托已被移除 |
| 普通列表退订失败后的清理 | 解除集合引用后才调用自定义事件移除器；父激活清理先清空作用域、快照及完成任务等状态，再退订；最终销毁时收集退订错误并继续逐项清理池节点；已取消或退订阶段的通知不再重新填充快照 | Navigation 示例及依赖离线编译通过，0 警告/错误；源码检查异常聚合、状态清理先后、迟到通知及元数据/格式；未新增测试，Unity 自定义事件移除器异常及原生节点释放未运行验收；集合若拒绝移除监听，框架会报告错误，不能保证该外部集合不再持有委托 |
| 命名预览的节点位置复核 | ElementNamingWindow 扫描快照记录控件直属父节点与兄弟索引，应用前重新采集比对；控件重挂载或排序改变但扫描对象顺序仍相同时，也拒绝旧预览并要求重扫；保持原有整批 Undo、Prefab 属性修改记录与失败回滚 | Editor 及依赖离线编译通过，0 警告/错误；源码核对扫描/应用检查顺序、元数据及格式；未新增测试，Unity 拖动层级、预览应用、Prefab 保存和 Undo 实际交互未运行验收；不将此检查声明为整个场景任意变化的检测 |
| 控件建议命名稳定性 | 补齐 Sbr_/Msk_/Lyt_ 已有前缀识别，避免重复执行建议时叠加控件类型前缀；保留窗口去重生成的数字后缀，如 Btn_Confirm_2，不再改成 Btn_Confirm2；只修改建议算法，不自动改名已有资产 | Editor 及依赖离线编译通过，0 警告/错误；静态核对全部输出前缀均被识别，检查数字后缀分支及元数据/格式；未新增测试，Unity 命名窗口预览、应用及 Undo 尚未运行验收 |
| 奖励列表资源继承示例 | 奖励模型增加实际 SpriteSource 生成绑定；仅父 View 配置 RewardIconLoader，克隆子 View 继承配置并按条目激活持有图标；加载器只实现同步接口，现场生成 Sprite/Texture 并在凭证归还时销毁；界面显示条目/池/资源凭证数量，组件提供关闭后计数入口 | Navigation.Editor 及示例依赖离线编译通过，0 警告/错误；最后的启动保护修改后 Navigation 再次编译通过，生成绑定指向 ImageElement.SpriteSource；源码检查仅父配置、无异步加载接口、部分创建失败回收、计数与原生 Destroy 的区别，元数据/格式检查；未新增测试，图标显示、自删除释放、池复用及最终归零尚未在 Unity 验收 |
| 子 View 资源加载器继承 | 未显式配置的 View 默认在激活时借用最近父 View 的有效加载器，显式配置优先，可通过 InheritParentResources 关闭；中间无上下文或父激活结束时停止继承；为子激活创建独立资源上下文，不借用父 Lifetime；父上下文在子初始化前发布，最终资源归还后清空活动引用，纯同步模式拒绝继承异步后端 | Navigation 示例与 TMP.Editor 及依赖离线编译通过，0 警告/错误；源码核对原生层级解析、模式准入、登记先后和上下文引用清理；未新增测试，嵌套/列表克隆/动态挂载、显式覆盖和资源计数尚未在 Unity 运行验收；默认行为由独立配置改为继承，需隔离的子 View 应显式关闭 |
| 普通列表引用的绑定边界检查 | 运行时初始化与编辑器结构校验逐层检查内容/模板引用路径，拒绝经过其他 View 或 IElementBoundary；内容节点本身不能建立独立绑定边界，模板根允许 NestedViewElement 但不能同时为 View；非激活节点同样检查，避免父列表接管另一子界面的节点 | Editor 及 Core/Resources/Navigation/ChildViews/UGUI 离线编译通过，0 警告/错误；检查端点例外、根节点/外部层级及非激活路径和元数据/格式；未新增测试，Unity Inspector 实际诊断及错误 Prefab 初始化尚未运行验收 |
| 当前工作树完整离线程序集覆盖 | 新增 Settings.Editor 与 Tabs.Editor 构建项目，补齐此前 35 项检查遗漏的两个示例菜单程序集；按最近 asmdef 归属核对 37 个构建项目的 Compile/Exclude，583 个 C# 文件无漏编或跨程序集混编；顺序完成所有 37 个项目的 Release 编译，核对 708 个 GUID、内部程序集引用、5 个示例目录和生成器发布 DLL | 37/37 编译通过，各项目 0 警告/错误；未发现重复/缺失/孤立元数据、缺失内部引用或样例路径；生成器与源码构建产物 SHA-256 一致。日志位于本机临时目录 mui-integration-audit-syr0j9_o；未新增测试、未启动 Unity。检查证明离线源文件覆盖和编译一致性，不等于 Unity 导入/菜单/视觉/输入/资源释放/性能/IL2CPP 验收，也不证明目标中尚未实现的能力已完成 |
| 普通奖励列表可启动示例 | Navigation 增加 Open Recycling List Scene 菜单，保存确认后创建唯一场景；未提供引用时在运行期生成 Canvas、ScrollRect、原生布局、非激活嵌套模板、标签与 Remove/Add 按钮，接入现有生成绑定和同步删除；自动节点归 Lifetime 清理，手工引用保持可用，布局独立 partial | Navigation.Editor 及运行时依赖离线编译通过，0 警告/错误；检查非激活构建、绑定后显示、射线/裁剪/模板位置、内置字体与输入后端说明、元数据/格式；未新增测试、未启动 Unity，菜单执行、场景保存、实际视觉及点击未运行验收 |
| 列表条目命令内自移除 | 普通/虚拟列表内部等待共用子准备接口，避免误触发面向业务的自身等待保护；公开列表 PendingChange 与虚拟重试/定位拒绝条目调用链的循环等待；同步命令内集合变化标记待刷新，在后续 LateUpdate 直接同步协调，准备检查反映待刷新状态；奖励示例增加实际生成的同步 Remove 命令与按模型身份移除 | Navigation 示例及依赖离线编译通过，0 警告/错误；检查内部准备/外部等待分离、同步脏标记路径与实际生成命令；未新增测试，Unity 点击自删除、异步命令排空、隐藏后恢复帧驱动尚未运行验收 |
| 虚拟列表父激活数据清理 | 父 Lifetime 释放时使来源代际失效，解除集合和分页订阅，清空快照、测量、键索引、选择及单元键、旧任务和作用域引用；退订分别执行并聚合错误，结束激活的通知不再更新布局；保留原生尺寸与池节点，不覆盖退场画面；开始激活时状态回调若已结束父激活则停止初始化 | Navigation 示例及依赖离线编译通过，0 警告/错误；检查 Lifetime 清理时机、分页代际/集合回调、选择监听与池引用范围，元数据/格式检查；未新增测试，Unity 缓存复开、退场保留、退订异常与实际内存回收尚未运行验收 |
| 嵌套视图父激活引用清理 | NestedViewElement 在父 Lifetime 清理阶段解除旧模型、句柄、作用域及完成任务引用，静态子视图与两类列表共同受益；关闭/资源归还仍由原 ChildViewScope 负责，不修改原生画面；最终销毁即使关闭请求抛错也清空引用，同步准备中已发布信号保留给原 finally 收尾 | Navigation 示例及依赖离线编译通过，0 警告/错误；检查 Lifetime 排空操作再逆序清理、Scope 独立拥有子句柄、激活身份隔离及元数据/格式；未新增测试，缓存复开、同步回调销毁与实际托管内存回收未在 Unity 验收 |
| 普通回收列表与生成绑定 | 对照 FUI RecyclingListElement 和设计 11.1.1 增加独立非虚拟列表；借用 ViewModel 集合、按位置复用 NestedViewElement、原生布局排布、容量限制、来源快照及重入代际检查；同步分支不创建或查询任务，异步分支等待子绑定；父激活结束退订，最终销毁池；增加模板/内容结构校验、Lst_ 命名、RewardsViewModel 与同步示例 | Navigation 示例与 Editor 及依赖离线编译通过，均为 0 警告/错误；检查实际 Rewards 及原 Page 生成绑定分别指向正确列表类型，7 个修改 C# 文件格式/元数据检查；未新增测试、未启动 Unity；示例需手工配置引用，原生布局、增删复用、异步清理和缓存复开尚未运行验收；单模板位置复用，不提供整批原子提交、键选择或虚拟化 |
| 即时布局主线程检查 | Unity 运行时及编辑器初始化回调记录主线程；View.FlushLayout 在存活检查和 transform 访问前拒绝后台调用，共用 ImmediateLayout.Rebuild 入口也独立检查；未初始化时明确拒绝，不使用任务、不把首次调用者当成主线程 | Navigation 示例及依赖、临时 UNITY_EDITOR 定义下的 UGUI 源码分别离线编译通过，均为 0 警告/错误；核对本地 Unity 初始化 API，检查线程守卫位置及格式/元数据；未新增测试、未启动 Unity，域重载开关、编辑器回调时序及真实后台调用尚未运行验收 |
| 虚拟列表测量宽度与整批有效性 | 每次回调后复核实际列宽仍匹配测量宽度；提交前逐一复核本批全部条目的激活、快照、单元和模型归属，后续回调回收前项时整批丢弃；非激活测量根跳过，不缓存无效高度；单元引用仅存于本次局部候选，不进入长期测量缓存 | Navigation 示例及依赖离线编译通过，0 警告/错误；源码检查提交前位置、宽度变化路径与局部引用范围，元数据/格式检查；未新增测试，Unity 实际宽度变化、交叉回收和锚点保持尚未运行验收 |
| 虚拟列表测量回调后的归属复核 | 可见单元测量捕获本次激活、来源代际和测量根；原生布局刷新后先检查归属再读首选高度，读取后再检查，覆盖 ILayoutElement getter 的重入；激活替换、条目回收/换绑、根失效或来源变化时丢弃本批结果，失效测量异常不污染新状态 | Navigation 示例及依赖离线编译通过，0 警告/错误；源码检查两次复核、Unity 对象有效性、字典/快照/模型身份及元数据/格式；未新增测试，Unity 实际布局重入、回收与动态高度尚未运行验收 |
| 受控即时布局入口 | View.FlushLayout 同步刷新当前活动 RectTransform 子树；与虚拟列表可见单元测量共用 ImmediateLayout，拒绝布局/图形更新期间及嵌套刷新，finally 恢复保护，刷新后复核原生根及 View 有效性；无任务、无全 Canvas 刷新 | Navigation 示例及依赖离线编译通过，0 警告/错误；核对 Unity 原生刷新/状态 API、两条调用路径、元数据/格式；未新增测试，Unity 布局重入、动态列表测量和真实尺寸未运行验收；不刷新祖先、不等待资源、不保证多轮布局自动收敛 |
| 原生布局辅助适配 | 对照 FUI 增加 LayoutSizeElement 的忽略布局、最小/首选/伸缩尺寸及优先级，AspectRatioElement 的模式与比例；有限值校验、保留负尺寸未指定语义，原生布局流程重建；批量识别作为主控件之后的候选，Lyt_ 命名 | Editor 与基础依赖离线编译通过，0 警告/错误；源码检查原生转发、无强制即时重建、候选优先级与元数据/格式；未新增测试，Unity 实际布局及编辑器挂载未运行验收；不负责多个布局驱动器仲裁，也不保证设置后立即取得最终尺寸 |
| 遮罩控件适配 | 对照 FUI 及设计文档基础控件要求补充 MaskElement/RectMaskElement，支持 MaskEnabled、ShowMaskGraphic、Padding、Softness；同步转发、有限边距/非负柔化检查，不拥有原生遮罩材质；接入批量识别与 Msk_ 命名，多控制组件沿用歧义提示 | Editor 与基础依赖离线编译通过，0 警告/错误；核对本地 RectMask2D 边距顺序和柔化 setter，元数据/格式检查；未新增测试，未提供遮罩示例 Prefab，Unity 嵌套裁剪、射线、柔化及编辑器挂载尚未运行验收 |
| Scrollbar 手柄与原生写入冲突校验 | 结构检查缺失手柄、根节点/外部层级及跨 View 引用；契约检查采集当前 View 层级原生 ScrollRect，报告它与模型同时写入滚动条 Value/Size，允许仅反向观察，不初始化或修改控件 | Editor 及依赖离线编译通过，0 警告/错误；检查非激活节点采集、属性/方向过滤、Unity 空值与元数据/格式；未新增测试，Unity 实际诊断未运行验收；不扫描其他场景对象或识别运行时动态/自定义驱动 |
| 独立 Scrollbar 控件适配 | 对照 FUI 增加 ScrollbarElement 的 Value、Size、NumberOfSteps、Direction、Interactable；纯同步属性设置、原生事件门控与销毁退订、通用浮点自动化输入；编辑器批量识别与 Sbr_ 命名，Settings 包提供独立三属性绑定契约 | Settings 与 Editor 及依赖离线编译通过，0 警告/错误；检查三个实际生成绑定及 Manifest、原生步数语义、元数据/格式；未新增测试，无配套 Scrollbar Prefab，Unity 拖动、门控、退订、自动化和挂载未运行验收；项目不得同时以独立模型和 ScrollRect 写入同一滚动条 |
| 普通滚动容器属性补齐 | 对照 FUI ScrollViewElement 增加 ContentPosition、Velocity、Horizontal、Vertical；坐标/速度有限值检查，位置变化停止惯性，同值回声不停止；原生滚动和程序写入按位置/速度快照通知，StopMovement 也发布；两种位置坐标共用绑定写入身份，拒绝同会话重复正向控制 | Editor 与 Core/Resources/Navigation/ChildViews/UGUI 离线编译通过，0 警告/错误；源码检查共享策略、同值路径及元数据/格式；未新增测试，Unity 惯性/弹性/布局/反向绑定未运行验收；速度通知不保证逐帧采样，位置与速度不构成原子事务 |
| 输入框创作引用校验 | 旧版 InputField 接入 View 结构检查，验证原生组件、必需文字、可选占位、所属层级、文字/占位共用和嵌套 View 越界；TMP 在既有结构规则上补充视口/文字/占位所属 View 检查，查找包含非激活祖先 | TMP.Editor 与基础 Editor/UGUI/TMP 等依赖离线编译通过，0 警告/错误；源码检查 Unity 空值、无初始化/无资产修改路径及元数据/格式；未新增测试，Unity Inspector/构建校验实际诊断未运行验收，运行时动态引用不由静态校验保证 |
| Settings 输入配置接入示例 | 新增显示名原生输入框与只读切换按钮，五条生成属性绑定覆盖内容预设、行模式、字符上限、只读和双向文本，同步命令切换编辑资格；布局构建拆到 Input 文件，Save 仅沿原示例显示本地结果 | Settings 及依赖离线编译通过，0 警告/错误；检查五条实际生成绑定、命令及 Manifest，元数据/格式检查；未新增测试，新增布局、真实输入、只读切换未在 Unity 验收；不提供账号校验/持久化，原 walkthrough 不覆盖新输入交互 |
| 输入控件原生配置绑定 | 对照 FUI 补充旧版/TMP 的 CharacterLimit、ContentType、LineType、CharacterValidation，以及旧版 ReadOnly；保留各后端枚举，拒绝非法数值，内容预设联动发布关联变化；旧版 Value 改为仅在实际变化且存活时通知，业务规则仍由项目处理 | TMP.Editor 及 Core/UGUI/TMP/Editor 等依赖离线编译通过，0 警告/错误；核对本地 InputField/TMP_InputField 配置联动和直接文本赋值差异，元数据/格式检查；未新增测试，输入法、键盘、软键盘和原生回调未在 Unity 验收；配置属性不提供业务校验或原子事务 |
| 当前工作树整体离线编译与包一致性 | 顺序构建 Tools~/Build 中全部 35 个项目，覆盖核心、模块、UGUI/TMP、编辑器及示例；检查 37 个 asmdef 的内部名称引用、685 个元数据 GUID、5 个示例目录及 README，核对发布 Analyzer 与当前生成器构建产物 SHA-256 一致；修正根 README 中过时能力描述和历史演示范围 | 35/35 编译通过，各项目 0 警告/错误，未发现重复程序集名/GUID 或缺失的具名 MUI 引用；未新增测试、未启动 Unity；本检查不涵盖 Unity 导入、交互、资源释放、性能、Player/IL2CPP，也不证明未实现项已完成 |
| Slider 范围与配置绑定 | 对照 FUI 增加 MinValue、MaxValue、WholeNumbers、Direction；范围/整数模式按原生裁剪后发布关联变化，普通 Value 与自动化输入统一拒绝非有限数值；Settings 示例先建立三个配置绑定，再绑定当前值 | Settings 示例及依赖离线编译通过，0 警告/错误；检查本地 Slider.direction 语义、实际生成顺序及 Manifest、元数据/格式；未新增测试，Unity 拖动、范围变化与原生回调未运行验收；多属性非原子配置，外部原生监听器仍可能执行 |
| 图片绘制与填充属性 | 对照 FUI 增加 ImageType、PreserveAspect、FillCenter、FillMethod、FillOrigin、FillClockwise；填充方法沿原生行为重置起点并通知关联变化；FillAmount 允许在类型切换前设置，编辑器仅在没有动态类型写入时要求 Prefab 为 Filled；示例增加类型和保持比例绑定 | TMP.Editor 与 Navigation 示例及依赖离线编译通过，0 警告/错误；对照本地 Image setter，检查两条生成委托与 Manifest、动态类型校验分支、元数据/格式；未新增测试，Unity 九宫格/径向显示与绑定切换未运行验收；方法与起点不是原子更新，动态操作须按原生规则排序 |
| 直接字体对象绑定 | TextElement.Font 与 TMPTextElement.FontAsset 借用原生字体并拒绝已销毁对象；TMP 换字体可能改变共享材质，因此映射到同一材质写入身份，资源槽持有时禁止直接换字体；字体变化发布关联属性通知，旧 Text 示例新增 LabelFont 生成绑定 | Navigation 示例与 TMP.Editor 及依赖离线编译通过，0 警告/错误；对照本地 TMP font/LoadFontAsset 的默认字体与材质回退路径，检查 LabelFont 生成委托/Manifest、共享绑定策略及元数据/格式；未新增测试，字体图集/材质渲染及切换未在 Unity 验收；未实现字体资源键槽、字体材质原子切换或自动回退字体管理 |
| 自动字号范围绑定 | TextElement/TMPTextElement 增加 MinFontSize/MaxFontSize，沿用原生整数/浮点语义，拒绝负数和非有限值；两端独立设置，不按绑定中间状态裁剪，项目负责最终有效范围；图形示例新增最小/最大字号及 BestFit 三条生成绑定 | Navigation 示例与 TMP.Editor 及依赖离线编译通过，0 警告/错误；对照本地 Text/TMP 原生 setter，检查三条生成委托与 Manifest、修改文件元数据/格式；未新增测试，Unity 自动字号布局及逐帧切换未运行验收；本项不补齐字体资源绑定或外部写入协调 |
| 文字基础排版属性 | 对照 FUI 补充 TextElement/TMPTextElement Typography 部分，支持字号、原生对齐、富文本、样式、行距与溢出；旧 Text 保留整数/九宫格语义，TMP 保留完整对齐并支持各类间距、自动字号、换行、可见字符数与 RTL 标记；同步设置并通知变化，校验有限数值与字号/字符数范围 | Navigation 示例与 TMP.Editor 及依赖离线编译通过，0 警告/错误；检查 LabelFontSize/LabelAlignment/LabelRichText 三条实际生成委托和 Manifest，修改文件格式/元数据检查；未新增测试，Unity 字体、布局、自动字号与 RTL 显示未运行验收；字体资源绑定、自动字号范围属性及主题/原生外部写入协调尚未补齐 |
| Inspector 资源键绑定清单 | 现有 View Inspector 按所选生成契约缓存并展示内置资源键的模型属性、目标、资源类型及方向；按类型与属性识别，支持继承图形层的材质绑定，不引用可选 TMP；提示统一配置入口及既有契约校验，明确不代表运行时加载器已配置 | TMP.Editor 及基础 Editor/UGUI/TMP 等依赖离线编译通过，0 警告/错误；元数据/格式与无初始化、无试加载路径源码检查；未新增测试，Unity Inspector 折叠、切换契约和实际显示尚未运行验收 |
| Prefab 配置与原生激活边界检查 | 根 View 执行创建配置时拒绝提前 BeginChildActivation，finally 解除限制；工厂在原生激活回调与代际复核后，检查 View/实例/父节点有效性、实际挂载及 activeSelf，异常沿既有创建回滚处理，不交付无效实例 | Navigation 示例及依赖离线编译通过，0 警告/错误；源码检查提前拒绝位置、回调异常退出、短路 Unity 空值判断与回滚入口，修改文件格式/元数据检查；未新增测试，OnEnable 重入、Unity 延迟销毁及实际失败清理尚未运行验收 |
| Prefab 提供方统一配置入口 | 常驻、同步加载和异步加载三个提供方增加可选 configureView，同一工厂在初始化/原生激活前同步调用；配置异常沿原创建事务回滚，回调后复核提供方状态、实例有效性及临时挂载，缓存复用沿用原 View 配置；同步导航 Resources 示例统一接入资源加载器 | Navigation 示例及 Core/Resources/Navigation/ChildViews/UGUI 等依赖离线编译通过，0 警告/错误；修改文件元数据/格式与回滚接线源码检查；未新增测试，Unity 回调抛错、原生销毁、缓存复开尚未运行验收；回调只允许配置，不接管项目外部副作用或加载器所有权 |
| View 激活级资源加载配置 | View 提供同步/异步加载器配置，每次激活将配置和当前 Lifetime 传给自身边界内的 GraphicElement；Source 首次写入才创建槽，普通对象绑定不预占资源属性，单控件显式配置优先；同步激活提前拒绝异步加载器，成功清理后可重新配置，加载器由项目持有 | Navigation 示例与 TMP.Editor 及依赖离线编译通过，0 警告/错误；检查生成绑定前的激活接线、同步入口、旧槽清理与退场释放顺序，元数据/格式检查；未新增测试，Unity 缓存重开、退场保留及资源计数尚未运行验收；嵌套 View 需独立配置，尚无宿主级自动注入或图片加载完成屏障 |
| 资源槽清空失败诊断与赋值状态 | 同步/异步最终清理共用 ClearTargetForRelease，清空控件失败进入 FirstCleanupFailure/CleanupFailureCount 和 UIErrors，异常仍只由当前清理回调交给 Lifetime 一次；HasUncertainAssignment 区分普通冻结和赋值结果不确定，成功清空目标后复位，不能代替最终归还结果 | Resources 与 Core 离线编译通过，0 警告/错误；源码检查两种清理接线、重复错误登记及状态复位，元数据/格式检查；仓库无现成资源槽测试，本轮未新增测试，行为与 Unity 原生故障路径仍未运行验收 |
| 原生赋值异常的候选持有保护 | 对照本地 uGUI 先改引用后发脏标记回调的实现，图形资源适配器在整个 setter 期间阻止 Source 重入；原生异常包装为 ResourceAssignmentException，同步/异步槽冻结并额外保留至多一份不确定候选；仅本次 SetTarget 失败接管候选，避免旧加载错误覆盖；清理成功清空目标后分别尝试归还候选与旧凭证，清空失败继续保留 | TMP.Editor 及 Core/Resources/UGUI/TMP/Editor 等依赖离线编译通过，0 警告/错误；源码检查本次候选过滤、代际失效、同步路径及双凭证清理，元数据/格式检查；未新增测试，原生回调抛错、冻结及最终归还尚未 Unity 运行验收 |
| 文字共用图形层与 TMP 材质后端 | TextElement/TMPTextElement 继承 GraphicElement，复用颜色、射线、遮罩和同步/异步材质资源接口；基类增加受保护的材质读写后端，TMP 使用 fontSharedMaterial，清空时恢复当前字体默认材质，不读取 fontMaterial；示例为 Label 增加独立文字材质键及共用属性绑定，基础 uGUI 不新增 TMP 依赖 | 最终 TMP.Editor 与 Navigation 示例离线编译均通过，0 警告/错误；检查 Label 的四条实际生成绑定及本地 TMP 材质实现，元数据/格式检查；未新增测试，字体图集兼容性、遮罩派生材质、渲染及销毁仍未 Unity 运行验收 |
| 共用 GraphicElement 与材质资源绑定 | 参照本地 FUI GraphicElement，Image/RawImage 共用 Color/Material/RaycastTarget/Maskable；MaterialSource 与独立同步/异步材质槽复用 ElementResourceOwner，图像与材质分别持有；绑定策略沿派生类到基类检查方向与 Material/MaterialSource 写入冲突；GraphicResourceViewModel 提供两种控件的继承属性绑定 | Navigation 示例与 Editor 两次离线编译均通过，0 警告/错误；检查实际生成的 8 条属性委托和 Manifest，元数据及格式检查；未新增测试，材质显示、遮罩派生材质、合批及原生销毁未运行验收 |
| 资源绑定失败与显式重试示例 | ResourceImageDemo 增加 Fail/Retry 与失败计数；加载器在请求开始时分配预期故障，独立同步更新保证第一项失败不跳过第二项；ViewModel 显式重新通知同一键用于重试；异步预期错误经 UIErrors 按加载器实例过滤，关闭后退订 | Navigation 示例及依赖离线编译通过，0 警告/错误；源码检查故障归属、同值重试及清理退订；未新增测试，失败保留旧图与重试恢复尚未在 Unity 运行验收 |
| 资源键绑定交互示例 | ResourceImageDemo 分为生命周期、布局和样例加载器三个文件；运行时生成 Image/RawImage 与切换/清空/关闭按钮，使用实际 ResourceImageViewModel 生成绑定；默认纯同步，异步延迟可故意忽略取消；显示在途/持有/创建/归还计数；关闭依次解绑、释放资源、销毁自己创建的原生对象，前一步失败不跳过后续清理 | Navigation 示例及依赖离线编译通过，0 警告/错误；元数据和格式检查；未新增测试，未启动 Unity，画面、按钮、迟到归还及关闭计数均为待运行验收项；计数不代表原生 Destroy 或内存释放已完成 |
| SpriteSource/TextureSource 生成绑定接入 | 参照本地 FUI AssetSourceSlot 增加 Source 属性及同步/异步 Configure 入口，共用 ElementResourceOwner.Source；模型只提供键，原生提交时更新已显示 Source，失败保留旧值；异步操作登记到显式 Lifetime，取消与迟到清理复用资源槽；IBindingPropertyPolicy 由运行时/编辑器共用，限制 Source 为 OneWay 并检测与直接资源属性的写入冲突；ResourceImageViewModel 提供实际生成示例 | Navigation 示例与 Editor 离线构建均通过，0 警告/错误；检查生成委托和 Manifest；未新增测试，Unity 连续换键/取消/释放/显示未运行验收；加载器自动注入、页面 Ready 联动、MaterialSource 与视觉保留自动接线未完成 |
| RawImage 纹理资源槽与共用持有者 | CreateSynchronousTextureSlot/CreateTextureSlot 将 Texture 槽托管到显式 Lifetime，独占写入、Unity 假空校验和提交后通知隔离与 Image 一致；提取内部 ElementResourceOwner 供两种控件复用，同步释放直接返回、不进入异步状态机，成功清理后解除独占并清除回调引用 | Navigation 示例及 UGUI/Resources/Core 等依赖离线编译通过，0 警告/错误；元数据/格式及清理路径源码检查；未新增测试，Unity 纹理替换/销毁与实际分配未运行验收；资源键生成绑定与视觉保留自动接线未完成 |
| RawImage 基础适配与生成绑定示例 | 参照本地 FUI RawImageElement 补齐 Texture/Color/UVRect；纹理借用、初始化后清理解除原生引用，UVRect 拒绝非有限值并保留平铺/翻转；批量挂载识别 RawImage，命名建议使用 Img_；TexturePreviewViewModel 提供三个属性的显式类型绑定 | Navigation 示例与 Editor 两次离线构建均通过，0 警告/错误；检查实际生成委托和 Manifest；未新增测试，Unity 显示/Undo/释放未运行验收；TextureSource/MaterialSource 尚未接入 |
| ImageElement 图标资源槽接入 | CreateSynchronousSpriteSlot/CreateSpriteSlot 将资源槽登记到显式 Lifetime；同步持有者暴露释放资格，异步持有者等待槽清理；同一控件拒绝重复槽及直接 Sprite 覆盖；销毁控件解除原生借用，候选检查 Unity 假空，通知错误不会回滚已提交赋值；成功释放后允许重新绑定 | MUI.UGUI 及 Core/Resources/Navigation/ChildViews 离线编译通过，0 警告/错误；未新增测试，图标替换/销毁/回调重入尚未 Unity 运行验收；资源 Key 生成绑定及自动视觉保留接线未完成 |
| 拖放会话持有收敛与双端清理保护 | 每来源 Lifetime/载荷类型只登记一个弱表索引的管理器，持有未结束会话并在收尾后移除，最多 256 个在途项，退出逆序清理；同步管理器不创建任务；异步提交也登记源与目标 Lifetime，资源释放不再依赖每次会话的登记顺序；Lifetime 跟踪异步操作调用链，在取消/释放状态改变前拒绝自等待，结束后清除链中 Owner 引用；示例 View 清理失败不跳过其他收尾 | 最终 DragDrop 示例及 Core/Navigation/ChildViews/UGUI 等依赖离线编译通过，0 警告/错误；弱索引/完成移除/容量前置/逆序清理/重复失败与操作链源码检查、元数据及行尾检查；未新增测试，长时间拖放内存、并发取消与实际销毁未运行验收 |
| 完全同步拖放及原生适配 | DragSession 沿用源 Lifetime 模式，完成信号按显式 Completion 读取创建，新增同步 Drop/Dispose/TryGetResult/IsCompleted/Pump；DropTarget.CreateSynchronous 提供独立同步委托并拒绝模式不匹配；同步提交在两端 Lifetime 登记操作，已提交成功优先于取消；后台取消仅记录意图；UGUI 改用直接状态并帧驱动取消，同步完成不启动任务观察；示例默认同步，可选择原异步延迟路径 | 最终 DragDrop 示例及 Core/DragDrop/UGUI.DragDrop 等依赖离线编译通过，0 警告/错误；源码检查任务惰性创建、重复投放、提交/视觉回调重入、捕获释放中重入、后台取消再 Cancel 及双端同步操作保护；未新增测试，实际输入/取消竞态/任务分配未运行验收；OS capture 与不合作异步提交隔离仍未完成 |
| JSON TextAsset 本地化同步适配 | TextAssetLocalizationProvider 增加独立同步加载器工厂、Load 和固定 Mode；错误模式在键解析/后端调用前拒绝；拆出共用 Parsing 文件，两模式共用语言/容量/条目/复数/回退校验；同步解析后先归还源再交付目录，解析失败清理源，源归还失败记录 SynchronousResourceLoadException；不拥有后端加载器 | Navigation 示例及 Localization/UnityResources 等依赖离线编译通过，0 警告/错误；解析提取、空原生对象、错误模式、失败与成功路径归还源码检查，元数据/行尾检查；未新增测试，Unity JSON 反序列化、Resources 加载和原生归还未运行验收；字体事务/RTL 排版仍未完成 |
| 本地化与主题完全同步服务 | 两种服务新增 CreateSynchronous、独立同步 Provider、ChangeLanguage/ChangeTheme、Mode/CanDisposeSynchronously/Dispose，复用同步 ResourceSlot 和原订阅/发布逻辑；纯同步模式提前拒绝异步切换，加载/发布期间销毁在修改服务状态前拒绝；错误目录同步归还，回滚失败记录 SynchronousResourceLoadException；新增内存语言提供方，内存主题提供方补同步入口 | Themes、Navigation 示例及 Localization/UnityResources 等依赖离线编译通过，0 警告/错误；检查模式准入、资源持有权、回滚失败、重复释放及重入，元数据/行尾检查；未新增测试，Unity 与任务分配未运行验收；JSON TextAsset 仍仅异步，字体事务/RTL 排版/列表联动未完成 |
| 树与分组展开增量通知 | TreeList 的单节点/祖先/全部展开共用先序可见序列合并，连续区间 Add/Remove、仍可见且展开状态改变的行 Update，一次事务发布；隐藏节点状态变化不触发可见集合版本；GroupedList 全部展开也只更新变化标题及正文；结构 Reset 保留完整重置，避免普通展开清空所有动态高度测量缓存 | 最终 Navigation 示例及依赖离线编译通过，0 警告/错误；检查集合事务回滚、元数据先提交、隐藏节点、空组及中间索引语义；元数据/行尾检查；未新增测试，Unity 锚点/测量复用未运行验收；仍保留快照及位置索引重建，不承诺展开复杂度与总节点数无关 |
| 完全同步的标准对话框示例 | 新增独立 Dialogs 示例及程序集、离线构建项目和 Package Manager 入口；复用 ConfirmationDialog/AlertDialog 同步路由与命令，通过 ObserveResult 接收结果，清理成功且确认后由 Update 打开后续提示；区分取消/无结果关闭/清理错误，重复请求保留当前句柄，组件销毁先撤销订阅再关闭独占宿主 | MUI.Samples.Dialogs 与 Core/Resources/Navigation/Dialogs/ChildViews/UGUI/UGUI.Dialogs 离线编译通过，0 警告/错误；示例注册、JSON、元数据及新增文件行尾检查通过；未新增测试，未运行 Unity，Prefab/字体/输入/焦点及任务分配尚未运行验收 |
| 异步分页构造前同步准入 | PagedList 在创建内部异步 Lifetime、完成信号和关联取消源前拒绝同步或已结束的父生命周期；无效参数/同步上下文检查先于内部 Lifetime 创建，所有权登记失败同步释放未启动的内部资源；纯同步列表/Grid 使用 ObservableList | Navigation 示例及依赖离线编译通过，0 警告/错误；首次构建因 NuGet 漏洞数据网络不可用失败，重试仅关闭本次 NuGetAudit；构造拒绝及回滚源码检查，未新增测试，未运行 Unity；不代表全框架同步任务分配审计完成 |
| 页面模板实际产物编译与生成器发布修正 | 离线调用已编译 Editor 的真实 PageSourceTemplate，覆盖两种文本后端、同步/异步、全屏/弹窗、无 Presenter/有 Presenter/类型化契约、新建/关联，共 48 组、160 份源码；发现包内旧生成器仍把同步方法输出为 AsyncCommand，现已用当前源码产物更新 Analyzers/MUI.Generators.dll | 旧包与当前源码生成器分别对 48 组源码编译，均 0 警告/错误；比较实际输出定位命令类型差异，当前输出为 SynchronousCommand；发布 DLL 与当前构建 DLL 的 SHA-256 完全一致（893dc83ac70f1762c3fbefbd7e36f70251e699cb7be334d7fc8eb5827d02c1bb）；包含中文/引号/反斜线/控制字符/代理对与保留字命名空间；未新增测试，临时生成编译产物不入库，Unity 原生资产流程与运行仍未验收 |
| 显式 UI 目录构建前校验 | UIBuildValidation 登记有界独立目录，强类型 AddPage 关联 Route/Prefab/Manifest；依赖闭合后复用路由图校验，检查跨根键冲突、资源键版本映射冲突及绑定资产；菜单与 IPreprocessBuildWithReport 共用入口，已登记目录错误阻止构建，未登记明确警告未覆盖；报告有界且收尾释放临时对象引用 | MUI.Editor 及依赖离线编译通过，0 警告/错误；目录封闭、空目录/采集异常、类型和根资产约束、构建失败分支源码检查；未新增测试，Unity 菜单/Player 构建回调未运行验收；不证明目录覆盖全部项目页面或运行时提供方映射一致，不含目标平台/AOT 验证 |
| 页面向导关联已有 View | 可选择普通/Variant Prefab 根资产，自定义标题与关闭按钮名称并按所选文本 Element 类型复用现有契约校验；支持嵌套边界与歧义检查，缓存使用结构化键且创建前复核；仅生成新源码与 PrefabAssetGuid，不复制/修改原资产；模板转义节点名，回滚限本次新目录 | TMP.Editor、基础 Editor 及运行时依赖离线编译通过，0 警告/错误；检查新建/关联分支、模板注释/转义、GUID 输出和回滚路径；未新增测试，实际资产选择、保存/导入、生成源码编译与绑定运行未验收；项目仍需登记资源键到原 Prefab |
| 页面向导目标程序集检查 | 从拟创建路径识别最近 asmdef/asmref，包引用通过 AssetDatabase 读取；使用 Unity 实际编译引用和 RoslynAnalyzerDllPaths 检查框架/文本模块及生成器，拒绝已知缺失、仅编辑器和禁用引擎引用；无编译快照明确未验证；缓存按目标/模板键和项目/编译变化失效，创建前复核 | TMP.Editor 与基础 Editor 及依赖离线编译通过，0 警告/错误；本地 Unity 2022.3 API 文档和程序集成员核对，路径边界/定义冲突/引用解析/创建前拒绝源码检查；未新增测试，未启动 Unity，真实项目编译元数据和编辑器交互尚未验收；不自动修复目标配置，不代表 Player/AOT 验证完成 |
| TMP 页面模板与同步路由骨架 | PageWizard 增加文本模板选择，TMP.Editor 显式注册 TMP 文本工厂并预检默认字体资产；标题 Element、标签组件和生成绑定类型一起切换，基础 Editor 不依赖 TMP；可扩展模板注册校验 Content 契约；默认勾选完整同步生命周期并生成路由声明，保留选择异步模式 | MUI.TMP.Editor 及基础 Editor/运行时依赖离线编译通过，0 警告/错误；检查代码模板分支、可选程序集依赖、字体前置验证和整批资产回滚接线；未新增测试，未启动 Unity，实际 Prefab 创建/导入/绑定生成结果编译与编辑器界面尚未验收；已有 View 关联与目标程序集配置发现仍未完成 |
| 参数更新提交前依赖复核 | 同步/异步 ArgsUpdateOperation 增加内部提交前校验点；导航复用准备前解析的依赖请求，复核父实例/拥有关系、依赖身份、活性与参数匹配，比较器后再做直接资格检查；正式可选降级可继续缺席，失败候选释放且不提交父参数；异步结束清除校验闭包 | Navigation 示例及依赖离线编译通过，0 警告/错误；检查同步/异步调用接线、提交前失败及清理、可选降级与比较器回调失效路径；未新增测试，运行竞态未验收；改变依赖参数与所有权的联合事务仍未实现 |
| 本地自动化截图与追踪导出 | CaptureSnapshot 在项目提供的渲染结束时机同步采集整个 Game View，记录相邻输入状态/帧号/时间/尺寸；检查播放和图形设备、前后像素预算，失败清理纹理；UIAutomationSnapshot 明确纹理所有权、主线程 Dispose 与显式 PNG 编码；ExportTrace 导出明确指定导航器的已有记录，不启动记录 | Navigation 示例及依赖离线编译通过，0 警告/错误；Unity 2022.3 官方截图时机与本地模块 API 核对，源码检查失败路径/空纹理/尺寸/线程/重复释放；未新增测试，实际截图、平台支持、原生销毁与内存峰值未运行验收；项目必须在帧渲染完成后调用，远程开发控制未接入 |
| 无任务自动化条件等待 | WaitForCondition 立即检查一次并返回外部帧驱动的等待句柄，Poll 单次求值；单调超时、令牌/显式取消、异常和成功分离，终态固定并释放条件引用；两种模式共用，条件检查期间拒绝会话派发和嵌套等待，不阻塞或启动后台工作 | Navigation 示例及依赖离线编译通过，0 警告/错误；零超时、超时前置/后置检查、回调取消、重入、异常及终态引用释放源码检查；未新增测试，实际 Unity 帧驱动尚未运行验收；截图与远程开发控制仍待完成 |
| 渲染元素激活去任务占位 | NestedViewElement/DynamicViewElement/VirtualListElement 构造与激活使用空值表示尚无操作，显式兼容查询才返回任务；异步等待补齐空状态分支；虚拟列表新激活清除上一轮任务，避免旧失败污染复用 | Navigation 示例及依赖离线编译通过，0 警告/错误；检查原生元素任务解引用和同步刷新分支；未新增测试，实际列表复用与任务分配未运行验收 |
| 绑定及共享基础设施同步去任务化 | BindingContext 的同步解绑/换绑/构建失败回滚直接记录清理状态与异常，不主动创建或探测任务；CleanupCompletion 显式观察才物化同步信号，并支持清理回调内观察同一次清理；MemoryTrimCoordinator 和 UIHost 同步回收不依赖任务状态；ChildView 转场和槽清理以空值表示尚无异步工作 | Tabs、Navigation 示例及依赖离线编译通过，0 警告/错误；检查延迟解绑、回滚错误合并、重复释放、异步观察与空状态等待路径；未新增测试，Unity 分配、解绑重入及低内存事件实际运行尚未验收，完整同步审计仍未完成 |
| 模型与 Tab 同步释放去任务化 | ViewModelOwnership 使用直接开始/完成/异常状态，Dispose 不创建任务；显式异步观察才物化兼容信号，重复释放不再次调用模型；Tab 同步释放独立记录状态和错误，预检不探测缓存维护任务，纯同步控制器不初始化异步维护占位 | Tabs 示例及依赖离线编译通过，0 警告/错误；成功/失败重抛、重入拒绝及同步后异步观察路径源码检查；未新增测试，Unity 实际 Task 分配与生命周期回调未运行验收，尚不能宣称全框架无任务审计完成 |
| TMP 与自定义输入自动化 | 新增 IUIAutomationInput<T>，UIAutomation.SetInput<T> 统一检查元素/实际 Selectable 的当前 View 归属、模式和输入门控；标准 uGUI 与 TMP 通过同步适配派发原生事件，无 UGUI 到 TMP 的依赖；TMP 文本不伪造键盘校验，下拉框只选择真实选项 | TMP、Navigation 示例及依赖离线编译通过，0 警告/错误；核对本地 TMP 原生赋值源码，检查新增接口 meta 与修改文件格式；未新增测试，Unity 原生事件/自定义适配器尚未运行验收；条件等待和截图仍待补齐 |
| 本地 UGUI 自动化入口 | 显式 UIAutomation 会话按现有索引查找并查询状态，按钮沿原生 onClick/绑定资格派发；文本/Toggle/Slider/Dropdown 输入沿原生变化事件，检查只读/范围/输入门控/模式与当前 View 归属，不初始化界面；同步入口无 Task，返回 Accepted 不伪造业务成功 | Navigation 示例及依赖离线编译通过，0 警告/错误；InputField 字符校验与绑定命令资格通过本地源码核对；未新增测试，Unity 实际事件/双向绑定/重入尚未运行验收；TMP/自定义控件、条件等待与截图仍待补齐 |
| 预加载与导航器退出请求追踪 | Preload 两种模式记录结果与 ReusedReservation，区别占位复用和物理缓存；Shutdown 两种模式及 UIHost 内部导航退出记录请求边界，重复异步等待共用原退出工作，同步兼容转发不重复记录；退出结果明确不涵盖外层提供方销毁 | Navigation 示例及依赖离线编译通过，0 警告/错误；预加载加入/取消/复用、同步转发与内部退出路径源码检查；未新增测试，Unity 预加载与退出实际运行未验收；维护操作编号、外层提供方及原生销毁追踪仍待补齐 |
| 返回与批量关闭请求追踪 | Back 的真实目标选择后补全句柄，局部处理/阻挡不猜测对象；CloseLayer/CloseAll 的两种模式记录范围、总结果与逐项状态，未请求项显式标记；明细受环形容量约束并为汇总留位，省略数明确，逐项时间标记为批次返回时采集 | Navigation 示例及依赖离线编译通过，0 警告/错误；容量一/尾部保留/批次取消和策略返回的接线源码检查；未新增测试，Unity 返回输入、批次守卫和清理未运行验收；预加载/宿主退出请求编号与资源关联仍待补齐 |
| 参数更新与模型换绑请求追踪 | UpdateArgs/Rebind 的同步与异步入口记录请求编号、句柄、路由键、总耗时和状态/拒绝/清理/恢复失败，不保存业务参数或模型；已有打开/替换/关闭的异步观察合并到通用内部实现，禁用追踪时直接返回原 ValueTask | Navigation 示例及依赖离线编译通过，0 警告/错误；泛型公共签名、原 ValueTask 单次消费和停止/重启隔离源码检查；未新增测试，Unity 事务恢复和运行时序尚未验收；Back/批量关闭等请求边界仍待补齐 |
| 关闭与清理等待请求追踪 | 普通/强制关闭、业务结果完成及显式清理等待记录请求编号、句柄、原始状态/清理状态和总耗时；类型化转发及 Complete 终态查询不重复记录，不存储业务结果；同步直接记录，异步仅启用追踪时单次观察原操作，不额外等待实际清理 | Navigation 示例及依赖离线编译通过，0 警告/错误；终态/重入/等待取消/超时 Pending 与内部转发路径源码检查；未新增测试，Unity 关闭守卫与超时回收未运行验收；Back/批量关闭/参数更新/换绑请求追踪仍待补齐 |
| 独立替换请求追踪 | 请求标识与起止记录抽到公共文件，Replace/ReplaceAsync 记录编号、源/目标句柄及结果并传入候选阶段；Open 超限替换复用原编号不重复包装，打开结束也记录被替换句柄；同步读取 SourceClose，异步源清理明确未采集，不访问 SourceCleanup 或额外等待 | Navigation 示例及依赖离线编译通过，0 警告/错误；替换准入/拒绝/异常/提交结果与追踪停用路径源码检查；未新增测试，运行替换/回滚与计时尚未验收；关闭/更新等操作编号及最终源清理关联仍待补齐 |
| 候选准备阶段耗时追踪 | 统一同步/异步准备入口记录前置依赖、模型、资源、激活、后置依赖起止；打开候选及超限替换、新建依赖沿用请求编号，复用共享实例不改写；资源缓存命中不伪造创建，未完成阶段区分正常返回；停用时不分配作用域，纯同步无任务，重启隔离旧完成 | MUI.Editor 及依赖离线编译通过，0 警告/错误；统一入口/依赖递归/替换接线源码检查；未新增测试，运行计时及故障时序未验收；阶段包含内部等待/嵌套/回滚，不可累加为总耗时，独立 Replace 尚无请求编号 |
| 打开请求起止与总耗时追踪 | Open/OpenAsync 包装实际返回结果和异常，记录宿主内递增 OperationId、起止、总耗时与结果元数据；BeginOpen 复用，受理异步 PostOpen 覆盖排队，同步 PostOpen 在实际执行时复用 Open；停止/重启后隔离旧请求的迟到结束；同步不创建任务，禁用追踪时异步原样返回 | MUI.Editor 及依赖离线编译通过，0 警告/错误；提前拒绝、ValueTask 单次消费、重启隔离和 PostOpen 接线源码检查；未新增测试，运行取消/重入/时间序列未验收；其他操作请求编号、阶段耗时与生命周期 OperationId 关联仍待补齐 |
| 纯同步公共请求与构造残留修复 | 请求计数器仅在异步模式创建/完成排空信号；同步宿主不构造异步预加载批次及清理 Lifetime；缓存与低内存任务字段按需创建，同步判忙使用直接标志与数量，异步清理正确处理尚无任务的空状态；内部异步排空/清缓存/清预加载入口拒绝同步模式 | Navigation 示例及依赖离线编译通过，0 警告/错误；共用入口、缓存淘汰及异步退出空状态源码检查；未新增测试或运行 Unity，Task 分配与实际缓存/退出行为尚需运行验证；此修复纠正此前对公共路径无任务的过宽表述 |
| 有界导航生命周期时间线 | 可选 Start/Stop/Capture 接入真实生命周期事件发生点，不依赖订阅或通知队列；环形缓冲、覆盖计数、单调时间、Handle/提交版本/结果/依赖降级元数据，无 Route/异常对象保留；纯文本导出限制名称长度并清理控制字符；UIHost 检查器显式记录/采集/复制 | MUI.Editor 及依赖离线编译通过，0 警告/错误；环形索引、停止保留、清空、通知溢出与无订阅路径源码检查；未新增测试，Unity 操作与运行时序未验收；尚非统一 OperationId 或分阶段耗时/资源关联追踪 |
| 资源预算占用明细 | 真实 Reservation 账本随加载/持有/释放状态更新，成功归还删除、失败保留；有界快照在同一锁内复制总量与资源键/类型/来源标签/估算/状态，不保留资源对象或任务；预算包装器两种模式接入 ownerLabel，同步示例增加共享后端明细入口 | Navigation 示例及依赖离线编译通过，0 警告/错误；成功归还、加载回滚、失败保留与截断路径源码检查；未新增测试，运行并发、失败注入和 Unity 资源归还未验收；尚未自动关联具体 View/Lifetime 或覆盖未接入预算的凭证 |
| UIHost 导航运行检查器 | UI Toolkit 保留宿主默认序列化编辑，显式采集有界导航快照；虚拟化实例筛选、状态/操作/清理/降级详情、覆盖来源/父拥有者/依赖/焦点/历史跳转；关系只查询同一快照，未收录句柄明确提示，失败清空旧结果，不初始化宿主或执行业务 | MUI.Editor 及依赖离线编译通过，0 警告/错误；程序集引用及快照边界源码检查；未新增测试，Unity 样式导入、虚拟列表交互和播放状态切换未运行验收；完整 Lease 归属、耗时追踪及自动化写控制仍待实现 |
| 显式同步射线诊断 | UIRaycastDiagnostics 使用独立指针数据调用实际 EventSystem.RaycastAll，复制排序、对象层级、点击处理目标及目标 View 层级命中位置；有保留条数与路径截断，拒绝重入；现有 View 检查器提供播放模式手动查询，不派发事件或修改焦点，无 Task 路径 | MUI.Editor 及依赖离线编译通过，0 警告/错误；与本地 uGUI 排序、首个有效对象及处理目标查找源码核对；未新增测试，Unity 检查器和实际射线未运行验收；查询执行项目射线过滤回调，不能证明完整点击资格 |
| 局部输入门控与 View 检查器快照 | InputGate 增加有界原因快照，区分已释放/仍阻挡；UGUI View 在不初始化、不创建门控、不写回属性的前提下采集宿主/局部/层级/保留画面及根 CanvasGroup 状态；现有检查器增加手动采集入口、时间、中文限制说明及截断提示；示例增加限制原因和释放后快照输出 | MUI.Editor 及导航示例离线编译；根 CanvasGroup 规则与本地 uGUI 源码核对；未新增测试，Unity 检查器展示和实际点击未运行验收；祖先组、控件与 EventSystem 的最终命中诊断仍待补齐 |
| 有界运行导航快照 | CaptureSnapshot/TryGetInstanceSnapshot 复制实例、历史、焦点、操作、共享拥有权及缓存/预加载/超时清理计数；表现重算记录最近隐藏/输入覆盖来源；绑定会话增加跨调用链在途命令计数；不持有项目对象或任务，不调用渲染器，同步无任务分配；示例增加快照输出和按真实句柄强制关闭准备中的可选依赖 | 导航示例编辑器及依赖离线编译；未新增测试，快照边界、命令计数与覆盖来源仅源码检查，Unity 未运行；渲染器局部输入锁、完整 Lease 追踪和运行检查器 UI 仍待补齐 |
| 静态依赖图编辑器浏览 | 独立 MUI.Navigation.Editor 程序集，UI Toolkit 窗口读取显式提供的路由根；复用静态校验，展示可搜索节点、父拥有者/直接依赖、必需/可选及层级顺序，支持关系跳转和复制报告；仅保存可序列化元数据，有节点/关系/问题展示上限；共享示例提供右键入口，新增编辑器离线编译项目 | 导航示例编辑器及依赖离线编译通过，0 警告/错误；XML 结构、控件名及 asmdef JSON 语法检查通过；未新增测试，Unity 模板导入、布局、交互及域重载未运行验收；当前为只读浏览，不包含路由资产创作或全项目自动发现 |
| 准备执行排空与依赖自身取消 | 导航候选增加准备执行标志，异步关闭取消后等待整次准备退出；每个异步候选令牌关联原请求及自身激活，保护迟到凭证、模型与视图；同步准备不创建任务并阻止重入释放；明确可选关系已撤销时等待既有退出，再复核父请求，间接必需父链不反向等待；新增异步共享依赖示例，支持延迟加载、迟到返回及延迟释放 | Navigation 示例及依赖离线编译通过，0 警告/错误；未新增测试，取消/强制退出/双重拥有链与清理顺序仅源码检查，Unity 未运行；非合作式准备、守卫等统一超时隔离仍待实现 |
| 可选依赖准备与运行期降级 | Optional 声明及静态元数据，禁止同目标混用必需/可选；逐项解析参数并区分新建实例与新取得关系，准备失败回滚后继续；同步不读取任务，异步等待实际清理；强制/故障退出撤销可选父关系，必需父链仍关闭；有界降级快照与事件、首次 Ready 降级、缺席依赖的参数更新保留；增加同步示例 | Navigation 示例及依赖离线编译通过，0 警告/错误；未新增测试，同步/异步回滚、重复声明、父链关闭及通知仅源码检查，Unity 运行未验收；自动恢复可选依赖未实现，需重新打开父页面 |
| 单条依赖释放基础 | 所有权账本增加幂等的单边撤销，先验证双向记录，再同步移除拥有关系及显示位置；整组释放复用此逻辑并保持逆序，只返回没有父拥有者和显式拥有者的目标；新候选创建前检查父容量 | Navigation 示例及依赖离线编译；未新增测试，单边/共享释放仅源码检查，Unity 未运行；可选依赖声明、降级通知及失败回滚接线仍未完成 |
| 保留依赖的父参数更新 | 同步/异步 UpdateArgs 在回调保护内解析新参数对应的依赖并比较现有参数，一致时继续原事务；变化返回 DependencyChangeRequired，失效返回 SourceUnavailable；示例增加标题更新及依赖变化拒绝，事务状态检查不读取关闭任务 | Navigation 示例及依赖离线编译；未新增测试，Unity 更新/回滚/依赖退出未运行验收；真正更换依赖参数的事务仍待实现 |
| 依赖父页面安全换绑 | 拆分实例资格与依赖变更约束，父页面 Rebind 同步/异步保留现有依赖；共享目标返回 InUse，换绑有效性再次校验拥有关系与依赖存活；参数更新的保护改为明确 InUse/DependencyChangeRequired；示例增加父换绑和共享拒绝 | Navigation 示例及依赖离线编译通过，0 警告/错误；未新增测试，Unity 显示、失败恢复和依赖退出未运行验收；依赖参数事务仍待实现 |
| 静态路由图校验 | Route.DependencyDescriptors 暴露只读元数据；RouteGraphValidator 检查键/单实例/位置/所有权环/显示环/最长深度，问题列表有界；首次 Register 先验证整图及宿主键冲突再登记，复用不可变定义；示例提供校验入口 | Navigation 示例及依赖离线编译通过，0 警告/错误；未新增测试，静态算法仅源码检查，Unity 菜单未运行，可视图编辑器未实现 |
| RequiredBefore/AttachedAfter 依赖顺序 | 声明增加位置，参数工厂按阶段解析；两种模式均完成隐藏准备后共同提交，分别安排父前/父后激活；账本记录位置与 Layer，同层拓扑显示排序、取得前检查显示环，冲突返回 DependencyOrderConflict；示例可配置位置 | Navigation 示例及依赖离线编译通过，0 警告/错误；未新增测试，Unity 层级、共享约束、替换和生命周期时序未运行验收 |
| 依赖准入拒绝与同步超限结果 | 参数冲突、资源准入、忙碌、清理容量及环/深度限制转为类型化 Rejected，Open/Replace/CloseOldest 保留原因和回滚状态；图校验先于参数比较，示例增加冲突入口；同步超限转换不再读取 SourceCleanup，ReplacedCleanup 按需物化 | Navigation 示例及依赖离线编译通过，0 警告/错误；未新增测试，Unity 冲突回滚、容量和超限任务分配未运行验收 |
| 必需依赖就绪合并 | 表现重算反向拓扑完成直接就绪，父 Ready 读取有效依赖的就绪结果并传播降级；通知仍在稳定快照阶段，无同步任务；已提交等待取消只收敛本页，依赖致命错误传给未关闭父链 | Navigation 示例及依赖离线编译；多层进入、共享动画取消、失败级联及 Unity 时序未运行验收；未新增测试 |
| 显式所有权撤销 | ReleaseExplicitOwnership 同步/异步入口及独立结果；父拥有者仍在则保留实例、撤销独立历史/焦点并通知；末拥有者走关闭守卫，拒绝保留关系，等待取消区分 Pending；显式重开恢复资格，共享示例增加持有/撤销菜单 | Navigation 示例及依赖离线编译；未新增测试，Unity 焦点、守卫、取消与生命周期回调尚未运行验收 |
| 必需共享依赖打开链路 | RouteDependency 强类型参数工厂及 Unit 结果限制，Route 复制声明；Open/Replace 同步异步递归准备、同参复用、共同提交、隐式历史隔离、显式打开增加拥有关系；关系快照 API 与纯同步双父示例，接入此前关闭账本 | Navigation 示例及依赖离线编译；共享/回滚/级联仅源码检查，Unity 未运行；细分拒绝码、显式释放、可选/AttachedAfter、依赖相关更新及 Ready 合并仍待补齐；未新增测试 |
| 所有权关闭接线 | 普通关闭增加 InUse，替换拒绝被持有源；父 EndActivation 后、内容缓存前逆序释放依赖并汇总错误；强制依赖关闭先拓扑收敛父链，占位避免父清理回等；宿主和显式清理等待覆盖父链阶段 | Navigation 示例及依赖离线编译通过，0 警告/错误；同步分支无任务；尚无依赖边创建入口，真实共享/强制级联路径仅源码检查，未新增测试，运行未验收 |
| 导航所有权账本与批次排序基础 | NavigationOwnership 区分显式拥有者和父页面集合，取得幂等、容量与环检测、逆序撤销和孤立依赖结果；节点接入 NewInstance/CompleteClose，批次按依赖约束稳定排序，无边时直接沿用原顺序 | Navigation 示例及依赖离线编译；目前只登记显式节点，尚无公开依赖声明/边接入，递归准备与联动释放未实现；图算法仅源码检查，未新增测试，运行未验收 |
| 不同类型候选共用准备契约 | ViewInstance 基类承载资源需求、准备有效性及回滚归属契约，Navigator 准备方法移除泛型依赖；Open/Replace 同步与异步四条路径均实际使用，路由参数和结果仍强类型 | Navigation 示例及依赖离线编译通过，0 警告/错误；仅完成依赖递归准备所需的内部契约，尚无共享依赖声明、图或所有权释放；未新增测试，Unity 运行未验收 |
| 局部返回登记及时撤销 | RegisterLocalBack 使用可撤销的令牌登记，不再向 Lifetime 累积已退订的释放记录；取消立即移除处理器及引用，补偿注册时同步取消，列表锁外撤销令牌防止互等；后台取消仅访问托管身份和列表 | Navigation 示例及依赖离线编译通过，0 警告/错误；撤销/派发快照源码检查；未新增测试，Unity 菜单循环及并发取消运行未验收 |
| 同步关闭移除主动任务分配 | 关闭准入使用 HasCloseStarted，纯同步关闭以 ViewCompletion 保存直接状态，不再创建两份 TaskCompletionSource；同步 Replace 携带延迟结果存储，只有显式读取 SourceCleanup 才创建兼容任务；异步关闭超时的逻辑/物理信号继续独立 | Navigation 示例及依赖离线编译通过，0 警告/错误；关闭/替换/参数更新状态读取源码检查；未新增测试，Unity 关闭重入及任务分配运行验收未完成 |
| 标准单按钮提示框 | AlertRequest/AlertViewModel/AlertPresenter 与 AlertDialog 契约及路由；同步已阅读命令返回 Unit，外部关闭保留 Dismissed；编辑器新增模板菜单并复用确认框正文/保存流程，独立契约校验 | UGUI.Dialogs.Editor 及依赖离线编译通过，0 警告/错误；未新增测试，Unity Prefab 创建、焦点、输入及运行时关闭未验收 |
| 纯同步导航通知示例 | 普通打开、替换目标与批次页面使用 ObserveReadiness/ObserveResult；同步 Lifetime 撤销、按激活句柄去重、最终结果移除登记，销毁先退订再关闭宿主；主动结果读取增加无效句柄检查 | Navigation 示例及依赖离线编译通过，0 警告/错误；未新增测试，Unity 回调时序及缓存重开未运行验收 |
| Handle 无任务就绪订阅 | ObserveReadiness 复用一次结果订阅及令牌撤销；表现重算退出后按稳定快照通知 Ready，关闭提交后的退役阶段通知提前关闭/失败，两种模式均不创建任务；业务回调使用导航上下文 | Navigation 示例及依赖离线编译；表现重算/关闭派发源码检查；Unity 动效、覆盖、就绪回调关闭和提前失败未运行验收 |
| 结果订阅及时解除与取消竞态 | ObserveResult 改用可撤销的 Lifetime 令牌登记，通知/取消/主动释放均解除；不向父 Lifetime 累积完成订阅；令牌登记竞态补偿、锁外撤销避免取消互等，登记检查创建 UI 线程 | Navigation 离线编译；通知/取消/挂接与解除状态路径检查；跨线程竞争及 Unity 实际回调未运行验收 |
| Handle 无任务结果订阅 | ObserveResult 由 Lifetime 托管、可提前撤销、已发布结果立即通知；导航在终态登记或超时结果提交后派发一次，回调隔离、撤销快照项有效，回调前释放业务引用；同步/异步共用且不创建任务 | UGUI.Dialogs.Editor 及依赖离线编译；结果发布/终态/超时接线源码检查；Unity 回调重入、取消与通知时序未运行验收 |
| 标准确认框完整同步路由 | ConfirmationViewModel 使用同步结果命令，ConfirmationDialog.CreateRoute 声明完整同步生命周期，两种宿主复用 VM/Presenter/绑定；同步通过 Open 与 Handle 读取用户结果，原异步串行服务保持独立 | UGUI.Dialogs.Editor 及依赖离线编译；生成器同步命令识别与绑定分流源码检查；Unity 按钮自身关闭、模态和结果读取未运行验收 |
| 导航候选提供方回滚归属 | Open/Replace 共用准备入口将同步回滚残留纳入候选 Lifetime；异步创建等待失败回滚并保留清理失败，成功回滚恢复取消语义；普通同步 Open 的异步回滚由候选关闭托管，纯同步宿主只记录违约 | Navigation 示例及依赖离线编译；Unity 无凭证残留、关闭结果及回滚等待未运行验收；共享依赖事务仍待实现 |
| 导航统一候选准备 | Open/Replace 共用类型化同步与异步准备入口，沿用原事务、不重新排队；同步 Replace 在加载前拒绝异步模型/Presenter；凭证先接管再检查取消和版本，提交/失败回收仍归外层事务 | Navigation 示例及依赖离线编译；共享依赖声明/持有权/整组提交仍未实现；Unity 缓存、版本失效与替换失败未运行验收 |
| 全程序集离线集成检查与菜单配置校验 | 根据 Tools~/Build 的 ProjectReference 计算 12 个入口，覆盖 32 个项目，Settings/Navigation/Tabs/DragDrop 示例、TMP、标准模块及 Editor 扩展全部编译通过；菜单返回目标祖先约束接入现有 Prefab 校验器 | 全部入口 0 警告/错误；校验器修改后额外编译 MUI.Editor 通过；未运行测试或 Unity，不证明 Prefab 扫描实际执行、输入或 IL2CPP 正确 |
| 同步契约异常与共享示例接入 | 同步 Lifetime.Load/ResourceSlot 对异步回滚异常只归档、不等待；SharedSynchronousResourceLoader 提供静态保留克隆安全能力的重载；同步导航示例连接预算/共享/Prefab 提供方并在宿主退出后停止共享层 | Navigation 示例及依赖离线编译，0 警告/错误；Unity 克隆共享、初始化失败与销毁次序未运行验收 |
| 异步共享加载期限与隔离 | 可选 loadTimeout，单次后端请求共用期限；超时发布携带实际回收任务的失败、取消令牌并拒绝同键新请求，迟到资源仍由原流程归还；IsolatedLoadCount 包含未成功回收条目，容量不提前归还 | Resources 离线编译与期限/取消/迟到状态路径检查；未运行计时或 Unity 验收；Lifetime/资源槽仍等待物理回收，不等同于导航准备响应超时 |
| 资源槽有界清理诊断 | 去除连续失败的无界异常列表，保留首错/饱和次数；先向所属 Lifetime 登记再发送 UIErrors，同步/异步最终释放统一汇总操作清理失败；目标清空失败不再掩盖已有归还错误 | Resources 离线编译与记录/释放路径源码检查；连续失败、回调退出及 Unity 清空异常未运行验收 |
| 资源加载失败回收归属 | Lifetime.LoadAsync/PreloadAsync 与 ResourceSlot 在原操作内等待后端 CleanupCompletion，成功回滚后恢复取消/取代语义；Lifetime.RecordCleanupFailure 有界保存操作回滚失败并纳入最终同步/异步释放，资源槽沿用释放错误归档 | Navigation 示例及依赖离线编译；同步回滚、异步失败及取代路径源码检查；UI 退出等待、并发和不合作后端运行未验收 |
| 异步共享加载退出等待 | SharedResourceLoader.DisposeAsync 停止新准入并快照全部回收任务、等待已接纳等待者退出；重复调用共享结果，全部收敛后汇总异常；加载/取消/释放调用链自等待在修改状态前拒绝 | Resources 离线编译；外部持有权不被强制夺取，不合作后端仍可阻塞退出；多线程及 Unity 退出时序未运行验收 |
| 异步资源共享加载 | SharedResourceLoader 按类型/键合并、独立等待取消与持有权；末等待者取消后拒绝加入旧请求、迟到回收、后端失败清理任务跟踪；条目/等待者双容量、首错诊断、调用链自等待拒绝；Dispose 停止新准入 | Resources 离线编译；取消/发布/末持有者释放状态路径源码检查，未新增测试；Unity 并发、上下文、取消回调及迟到释放未运行验收；不合作后端超时隔离仍待补齐 |
| 同步资源共享加载 | SharedSynchronousResourceLoader 按类型/完整键去重，独立凭证计数、最后归还后端；加载/释放重入拒绝、失败保留容量、有界首错诊断、关闭停止新借用；保留后端克隆安全能力，线程预检先于凭证失效 | Resources、Navigation 示例及依赖离线编译；与预算包装的源码路径检查；未新增测试，Unity 图标/Prefab 共享与失败回收未运行验收；异步共享加载仍待实现 |
| 返回输入跨入口消费 | BackInputConsumption 按 EventSystem 以弱引用记录本帧消费，域重载关闭时重置；UIHost、菜单 Cancel 与对话框 Cancel 在回调前共用占位，菜单补充选中对象/控件资格预检；同步调用，无任务 | Navigation 示例、UGUI.Dialogs 及依赖离线编译；Unity 输入模块顺序、同帧焦点迁移与无域重载会话未运行验收；跨帧重复及第三方输入需项目接线 |
| 菜单自动局部返回接线 | ContextMenuController 每次显示使用独立同步 Lifetime 登记到 View，隐藏/禁用/失去资格/打开失败撤销；支持指定祖先导航 View，重新显示重新登记，不累计到页面激活周期 | Navigation 示例及依赖离线编译；Unity 菜单焦点、输入模块重复派发与嵌套菜单未运行验收 |
| 导航局部返回处理 | Core ILocalBackView；UGUI View.RegisterLocalBack 由激活 Lifetime 托管，后登记优先、取消失效、快照派发与重入保护；Navigator 在页面策略前调用当前可交互候选的局部处理器，失败不穿透；两种模式共用 | Navigation 示例及依赖离线编译；子控件显式登记到所属导航 View，未实现原生编辑态或自动焦点链推导；Unity 输入、回调增删、缓存重开未运行验收 |
| 父 View 重新激活的直接清理状态 | ChildViewScope.IsDisposedSuccessfully 区分尚未释放、成功和失败；View 在恢复或重开前直接检查，同步路径不读取 CleanupCompletion，不因释放失败而允许复用 | Navigation 示例及依赖离线编译；Unity 缓存重开、子项清理失败和父重新激活未运行验收 |
| 宿主返回输入入口 | UIHost.RequestBack/TryRequestBack 供按钮及项目输入动作显式接入；同步直接派发、异步观察完成；同帧与在途去重、结果通知隔离、退出清除订阅及迟到通知抑制 | 源码检查与 Navigation 示例离线编译；不自动轮询平台键，输入法/局部取消优先级由项目输入适配接线，Unity 与平台运行未验收 |
| 异步 Prefab 回滚归属与退出诊断 | 驻留 Create 登记创建操作，失败回滚由独立 Lifetime 跟踪；退出先等待创建再等待回滚，staging 清理后才发布完成；创建准入计入在途回滚，保留首个错误/次数和回滚数；后端异步回滚结果在加载操作内收敛 | Navigation 示例及依赖离线编译；Unity 创建回调内退出、回滚失败、容量与迟到释放未运行验收；不合作后端的超时隔离仍未实现 |
| 同步 Prefab 提供方清理诊断 | 首个清理错误及饱和计数形成有界诊断，覆盖后端回滚、原生实例释放、驻留资源归还和 staging 销毁；Dispose 及重复 Dispose 报告已知错误，外部凭证迟到释放仍更新诊断，不夺取正常持有权 | Navigation 示例及依赖离线编译；Unity 失败退出、迟到释放和错误计数未运行验收；异步提供方最终诊断仍需补齐 |
| Prefab 正常释放的原生归属 | 工厂凭证独立持有 GameObject，View 组件提前销毁仍清理原生根；正常释放错误记录是否成功请求 Destroy；异步提供方等待完整 GameObject 销毁，未能确认销毁请求时报告错误并保留依赖 | Navigation 示例及依赖离线编译；Unity 组件单独销毁、正常关闭与 Destroy 异常未运行验收 |
| 原生 Prefab 回滚与销毁错误 | 工厂共用逐项清理，View.Dispose/隐藏/Destroy 独立尝试并保留全部错误；创建回滚失败抛同步资源残留异常并携带失败实例；异步提供方解包后仍等待原生销毁，Destroy 请求失败则拒绝提前归还依赖 | Navigation 示例及依赖离线编译；Unity 初始化失败、清理回调和原生销毁异常未运行验收 |
| 同步视图适配回滚失败传播 | 同步视图提供方契约明确回滚失败异常；Content 挂载失败后的凭证释放、同步加载 Prefab 创建失败后的驻留归还使用 SynchronousResourceLoadException；子 Scope 保留后端清理错误并转为子准备清理失败，使 Nested/Slot 禁止安全性未知的复用 | Navigation 示例及依赖离线编译；Unity 挂载失败、后端归还失败和父退出诊断未运行验收；原生 Prefab 工厂失败分支仍需审查 |
| 隐藏子界面请求维护 | IChildRequestHost 复用 Tick 活跃性注册、独立派发请求；Navigator.Pump 遍历注册快照，跳过执行命令/参数更新/换绑中的实例，不套用隐藏/覆盖暂停；UGUI View→Scope→Handle 递归派发，Navigator Pump 拒绝重入 | Navigation 示例及依赖离线编译；Unity 多层隐藏、覆盖、关闭及重入未运行验收；独立宿主须驱动 Pump |
| Lifetime 同步取消与释放状态 | 纯同步实例不创建取消/销毁完成信号，锁内直接记录取消完成与释放开始/完成；同步准入和重复释放不读取任务；操作计数包含同步 Run，IsEnded 在释放占位后立即生效；异步模式继续保留等待信号 | Navigation 示例及依赖离线编译；取消重入、跨线程竞争、错误聚合与 Unity 运行未验收 |
| 子界面故障关闭的同步报告 | 绑定提交、参数恢复、模型换绑、保留画面失败与 Scope 取消共用 BeginCloseAndReport；同步分支直接执行关闭、确认完成并报告原错误，不创建观察任务；Scope 异步观察器拒绝同步模式；Handle.DisposeAsync 同步兼容分支直接调用 Dispose | Navigation 示例及依赖离线编译；Unity 恢复失败、取消与清理错误传播未运行验收 |
| 同步子界面自身关闭请求 | RequestClose 在同步忙碌时登记 Scope 去重集合，空闲时直接关闭并报告错误；Pump 按入口快照同步派发，Tick 自动调用并由待请求唤醒 Tick 注册；取消和句柄移除清除请求 | Navigation 示例及依赖离线编译；Unity 自身命令、重入、隐藏层级暂停 Tick 与回调增删请求未运行验收；UIHost 已通过独立 Pump 维护隐藏层级，独立宿主需显式 Pump |
| Handle 同步关闭与过渡状态 | 子 Handle 以独立关闭开始/完成标志控制版本资格、参数更新及重复释放；直接 Dispose 不创建关闭任务，兼容清理信号按需提供；同步停用/恢复使用执行标志，不创建或读取过渡任务；移出 Scope 后才发布关闭完成 | Navigation 示例及依赖离线编译；Unity 清理回调、停用恢复及异常未运行验收；内部请求关闭观察路径与 Lifetime 仍需审查 |
| Scope/Slot 同步释放状态 | 同步 Dispose 使用独立开始/完成状态及原错误，准入与重复释放不读取任务；Scope 同步 IsActive/IsDisposed 读取直接状态，CleanupCompletion 在所属线程显式查询时按需生成；异步模式已完成后的同步重复释放兼容保留 | Navigation 示例及依赖离线编译；Unity 释放回调重入、错误聚合和兼容信号未运行验收 |
| 静态嵌套同步准备与清理错误 | NestedViewElement 的同步忙碌检查直接使用执行状态，兼容准备信号按需生成；Nested 和同步 Slot 不读取清理任务来推断复用安全性；ChildViewPreparationException 保存 SynchronousCleanupError，兼容清理失败任务按需创建 | Navigation 示例及依赖离线编译；Unity 嵌套恢复、清理失败及回调查询未验收；Scope/Slot 底层其他兼容信号仍需审查 |
| 动态子界面同步结果与按需任务 | DynamicViewElement 的同步配置、重复赋值和重试直接读取错误状态；同步替换与激活不分配结果任务，SynchronousResult 直接提供结果；回调中显式查询兼容接口才创建当前操作信号，并在收尾完成；整次同步替换拒绝重入 | Navigation 示例及依赖离线编译；Unity 回调查询、失败重试和资源清理运行未验收 |
| 同步子准备检查与列表按需信号 | IChildViewElement 提供直接同步准备检查，View 在同步作用域下检查 Nested/Dynamic/列表的执行状态和原始错误，不读取 Preparation 任务；同步列表仅在兼容查询时创建完成信号，普通刷新及几何失败不创建任务信号 | Navigation 示例及依赖离线编译；Unity 回调查询、父提交、异常与重新激活运行未验收；其他元素内部兼容任务仍待审查 |
| 批量条目更新的测量失效 | 完整变更路径按稳定键淘汰 Update/Replace/Add 的候选测量，Reset、版本断档与错误恢复全部重测；覆盖同批更新后移动、删除后重新加入，校验成功后提交；单条测量更新复用该路径 | Navigation 示例及依赖离线编译；Unity 批量文本更新、混合变更及同步重测运行未验收 |
| 测量缓存与布局提交 | 可见测量批次先验证总高度再发布缓存；条目更新使用候选缓存，宽度变化及手动失效先准备估算行索引；失败恢复重新测量，避免复用错误期间已变化模型的旧高度；同步/异步及 Grid 共用 | Navigation 示例及依赖离线编译；Unity 几何溢出、重试与宽度变化运行未验收 |
| 测量列表首次追加 | 启用自动高度测量时，空列表首次追加也走完整快照准备，创建行索引；同步列表及多列 Grid 共用，不启动异步工作 | Navigation 示例及依赖离线编译；Unity 首次追加和测量运行未验收 |
| 虚拟列表换源几何准入 | Items 在更换来源和事件订阅前验证模板及总高度，复用候选行索引；校验失败保留原来源、快照、选择和滚动位置；同步/异步及 Grid 共用 | Navigation 示例及依赖离线编译；非法总高度与换源运行场景未验收 |
| 同步所有权登记与资源槽残留诊断 | Lifetime 同步登记先校验 ISynchronousDisposable 能力，拒绝后所有权不转移；资源槽两种模式保存加载后端的同步回滚失败并在最终清理报告 | Navigation 示例及依赖离线编译；忙碌资源登记、页面退出与失败传播尚未运行验收 |
| 同步资源槽 | ResourceSlot.CreateSynchronous、Replace/Clear/Dispose、Lifetime 模式与同步销毁能力；共用赋值/冻结协议，独立同步加载和凭证，取消后候选释放，错误保留到销毁 | Resources、Navigation 示例及依赖离线编译；图标替换、取消、冻结、回调重入和失败释放尚未运行验收 |
| 同步加载回滚失败计费 | SynchronousResourceLoadException 区分普通失败与未释放资源；同步/异步预算包装保留失败额度；同步 Prefab 与导航预加载传播同一失败占用，退出保留诊断 | Navigation 示例及依赖离线编译；多层预算、后端回滚失败与运行行为尚未验收 |
| 同步克隆安全能力的预算透传 | BudgetedResourceLoader 的同步/双模式工厂按底层能力选择包装类型，强类型同步重载直接接入 Prefab；普通同步后端不会被授予更强能力；示例使用 Resources→预算→同步 Prefab | Navigation 示例及全部依赖离线编译；嵌套预算、失败释放与 Unity 运行尚未验收 |
| 同步按需 Prefab 加载 | SynchronousLoadedPrefabViewProvider；同步加载/创建/预加载、同代际驻留复用、独立引用、容量与失败额度、失效及关闭；UnityResourcesLoader 声明克隆安全归还能力；同步导航示例支持 Resources 路径 | Navigation 示例、UnityResources 及依赖离线编译；Unity 加载/销毁/容量/失效运行未验收；即时卸载克隆依赖的后端不支持此适配 |
| 导航准备与替换提交复核 | Open 的资源能力查询进入回调保护；ViewInstance 准备资格复核绑定注册代际；同步 Replace 在最终守卫属性访问后重新检查候选 | Navigation 示例及依赖离线编译；提供方/守卫回调重入和注册表重建运行场景尚未验收 |
| 同步底层释放审查 | SynchronousResourceLease 使用独立释放状态，Dispose 不创建或读取 Task，异步等待按需创建信号；纯同步 Lifetime.DisposeAsync 直接调用 Dispose，Run 以计数登记；执行中释放直接拒绝 | Resources 与 Navigation 示例依赖离线编译；并发、释放回调循环和异常传播尚未运行验收；其他内部兼容信号仍需逐项审查 |
| 同步预加载 | 独立 ISynchronousPreloadViewProvider/Lease、同步驻留凭证、Navigator.Preload/ClearPreloads、重复资源复用、容量与版本校验、低内存和退出释放；独立 IViewContentVersion；常驻 Prefab 适配及示例 | Navigation 示例及依赖离线编译；常驻目录只独立持有引用，不执行资源卸载；按需加载现有专用同步适配；失败/版本变化及 Unity 运行仍待验证 |
| 同步替换与超限淘汰 | INavigator.Replace；同步候选准备、共用守卫求值及版本复核、共享关闭/打开提交；Open CloseOldest 复用同一事务；SourceClose/ReplacedClose 直接返回旧实例清理；示例增加替换失败保留与第 4 页超限替换 | Navigation 示例及依赖离线编译；Unity 准备失败/守卫拒绝/回调重入/激活失败及缓存释放未运行验收 |
| 同步批量关闭 | INavigator.CloseLayer/CloseAll；共用快照及顺序、逐项同步守卫/清理、失败继续、自身命令整批拒绝；实例保留真实终态避免历史淘汰影响批次；示例增加多页面和分层关闭 | Navigation 示例及依赖离线编译；Unity 多页面/回调关闭/拒绝与清理异常尚未运行验收；共享依赖所有权及拓扑顺序仍未实现 |
| 顶层同步参数更新与换绑 | INavigator/ Navigator.UpdateArgs、Rebind；直接复用共用同步候选事务及模型换绑；队列忙时拒绝、自身命令重入拒绝、候选释放后撤销忙碌标志、恢复失败直接故障关闭、共用事件发布；示例新增更新/回滚/换绑菜单 | Navigation 示例及依赖离线编译；Unity 提交/回滚/故障关闭/所有权运行验收未完成 |
| 纯同步导航与 Unity 宿主 | Navigator.CreateSynchronous / UIHost.InitializeSynchronous；独立同步提供方、Open/Close/Complete/Back、缓存命中及直接淘汰、ClearCache/InvalidateCache/TrimMemory/Shutdown；同步低内存及 OnDestroy 接线；异步入口提前拒绝；新增 SynchronousNavigationDemo | Navigation/Tabs 示例及依赖离线编译；同步预加载导航入口及常驻 Prefab 适配已接入，同步 Resources 按需适配已接入，运行验收仍待完成；Unity 生命周期/缓存/输入/异常未运行验收，不能声明全部同步目标完成 |
| 导航同步关闭协议 | INavigator/ Navigator Close、ForceClose、Complete；同步守卫版本校验、关闭前能力检查、直接清理/终态登记；共用关闭提交及通知，异步实例返回 RequiresAsync | Navigation/Tabs 示例及依赖离线编译；公开纯同步实例创建、缓存和退出已接通；正常关闭链仍待 Unity 运行验收 |
| 导航同步守卫与结果读取 | ISynchronousCloseGuard 接入现有 Close/Complete/Replace 守卫流程与版本复核；ViewHandle.TryGetResult/Readiness 直接读共享结果，异步等待按需创建任务；可选示例 | Navigation/Tabs 示例及依赖离线编译；Unity 守卫/结果运行未验收；这些入口不等于 Navigator 全生命周期纯同步调度已完成 |
| 导航实例同步生命周期基础 | ViewInstance 激活模式、同步资源接管/释放、回调与清理能力预检、显式同步缓存交接、共用关闭结果/终态发布、重复释放结果保留 | Navigation/Tabs 示例及依赖离线编译；已从 Navigator.CreateSynchronous 创建纯同步实例；其余同步事务仍见首行限制；Unity 未验收 |
| 导航内容同步所有权基础 | Route 独立完整同步声明；ViewContent 可配置实例模式、同步凭证接管/预检/释放、自有模型与 Presenter 钩子能力检查、缓存同步释放原语 | Navigation/Tabs 示例及依赖离线编译；尚未接入 Navigator 的模式选择、实例激活、打开/关闭/缓存调度，不能作为纯同步顶层导航使用；Unity 未验收 |
| 子视图同步参数事务 | 独立同步 Presenter/候选契约、UpdateArgs、准备/提交/回滚/释放、输入屏障和错误汇总、故障同步关闭、历史清理错误保留；同步道具参数示例 | Navigation/Tabs 示例及依赖离线编译；Unity 提交/回滚/候选释放未验收；顶层导航同步参数更新尚未接入 |
| 子视图同步模型换绑 | ChildViewHandle.Rebind、共享 Presenter/模型所有权的同步换绑入口、输入屏障、原绑定恢复、子准备/提交、旧自有模型同步释放、不可恢复时同步关闭；道具示例菜单 | Navigation/Tabs 示例及依赖离线编译；Unity 业务钩子、失败恢复、资源释放未验收；子视图同步参数更新见上项；顶层导航同步换绑尚待接入 |
| 虚拟列表/Grid 同步路径 | 两种模式共用单元协调，同步嵌套绑定/回收、本地来源和 Columns 切换、Retry/ScrollToKey、有界测量/回调协调、异步分页准入拒绝、千项示例 | Navigation/Tabs 示例及依赖离线编译；Unity 滚动、测量、焦点、释放与性能未验收 |
| 同步统一内存回收 | 独立 ISynchronousMemoryTrimParticipant、同步登记/TrimMemory/Dispose、共享容量与顺序、逐项错误汇总及回调重入保护；同步 Tab 接入，示例增加统一回收菜单 | Tabs 示例及依赖离线编译；Unity 低内存回调、缓存释放运行未验收；Navigator/UIHost 尚未具备完整同步宿主模式 |
| Tab 同步生命周期 | 独立同步定义与守卫；Select/Retry、目录更新/可用性协调、停用缓存/恢复/淘汰/TTL 帧驱动、ClearCache/Dispose；TabBar/重试控件按模式分流、同步示例 | Tabs 示例及依赖离线编译；Unity 输入、失败恢复与缓存运行未验收；统一内存协调器已删除；由宿主显式调用自身缓存清理入口 |
| 动态子视图同步路径 | ChildViewSlot.Replace/Clear/Dispose；共享提交与回滚、同步旧资源释放、清理失败保护；DynamicViewElement 同步属性更新与完成信号；独立同步 Provider 挂载入口、动态道具示例 | Navigation/Tabs 示例及依赖离线编译；Unity 运行未验收；虚拟列表/grid 同步链见上项，顶层导航完整同步模式仍未接通 |
| 静态嵌套控件同步路径 | NestedViewElement 根据父模式同步移除/换模型/候选清理/旧显示恢复；清理失败禁止继续复用；当前子命令重入拒绝、父操作登记；同一失败请求允许重试；纯同步嵌套示例 | Navigation 示例、嵌套示例与生成器离线编译通过，Unity 绑定/恢复/清理未验收；动态子视图、Tab 与虚拟列表已接通，见上项 |
| 同步子视图停用与恢复 | Scope.Deactivate/PrepareReactivation；保留原实例、重建同步激活、旧激活清理、Prepared 显式提交、父操作登记、失败同步关闭；两种模式共享过渡与准入原语；同步道具菜单补充 | Navigation 示例及依赖离线编译通过；Unity 复用/嵌套/失败路径未验收，Tab 控制器同步切换和缓存已接通，见上项 |
| 纯同步子视图基本生命周期 | Scope 继承父同步模式、模板显式声明、独立同步 Provider 准入、同步准备/失败回滚/提交/句柄关闭/Scope 释放，共用状态与公共清理；ISynchronousDisposable 逐层预检；同步道具示例 | Tabs/Navigation 示例及依赖离线编译通过；未验收 Unity 运行；子视图参数更新与模型换绑已接通，见上项；顶层导航仍未全链路接通；停用恢复、动态组件和 Tab 见上项 |
| 同步操作登记与子视图准备 | Lifetime.Run 直接执行同步委托并跟踪退出；ChildViewScope.Prepare 移除 RunAsync/任务读取桥接，登记父 Lifetime 和 Scope 操作；Lifetime.Load 同步登记加载/失败回滚，阻止回调重入提前释放所有者 | Tabs 示例及依赖离线编译通过；纯同步父模式下的准备和子树关闭已接通；默认混合模式保留异步收尾；重入与线程运行验证未完成 |
| 共用生命周期同步收尾基础 | ViewModelOwnership 同步能力查询/Dispose/退役模型释放；ViewActivationCleanup.Run、ViewInstanceCleanup.Run；ViewPresenterLifecycle.EndActivation、同步钩子约束与激活模式传给绑定；保留换绑清理错误 | Navigation 示例及依赖离线编译通过；子视图已接入同步关闭、工厂能力准入与子树同步准备；顶层导航完整同步关闭仍待实现；Unity 运行未验收 |
| 同步命令与标准绑定 | IUICommandState/ISynchronousUICommand/SynchronousCommand；void 命令直接生成同步实现；绑定会话同步模式与异步命令准入拒绝；Unbind/Dispose 和直接同步 Rebind，共享失败恢复与清理终态 | Navigation/Settings 示例及生成器离线编译通过；不包含宿主/子视图属性设置器的同步能力传递，Unity 命令关闭/解绑重入/失败恢复未运行验收 |
| 同步视图资源契约 | 独立 ISynchronousViewProvider/ISynchronousViewLease、SynchronousViewLease；常驻 Prefab 与借用 View 同步归还；内容挂载同步准入/失败释放、共用挂载校验与回调后归属复核；借用 View 门控前占用防重入 | Tabs 示例及 Core/Resources/ChildViews/Navigation/UGUI/Tabs 依赖离线编译通过；不赋予 LoadedPrefabViewProvider 同步释放能力；完整宿主同步生命周期及 Unity 运行验收仍未完成 |
| 纯同步生命周期与资源基础 | LifetimeMode.Synchronous、同步 Dispose 与准入检查、独立 ISynchronousResourceLoader/ISynchronousResourceLease、Lifetime.Load、SynchronousResourceLease、Unity Resources 与预算包装器同步失败回滚 | Core/Resources、Unity Resources 适配与 Navigation 示例离线编译通过；完整 UI 同步模式尚未接入导航、绑定、子视图、Tab 与宿主退出，不能视为全框架已支持；运行验证未完成 |
| 导航关闭超时与清理隔离 | RoutePolicy.CloseTimeout 默认 30 秒、独立清理令牌、ClosedWithCleanupPending、业务结果先完成、物理 CleanupCompletion/WaitForCleanupAsync、隔离数量准入、迟到回收禁缓存、Shutdown/SourceCleanup 等待真实清理 | Navigation 示例及依赖离线编译；忽略取消的关闭示例已补充，Unity 计时/线程/取消回调/迟到释放未验收；Prepare、守卫和独立更新超时仍未完成，隔离字节预算未接入 |
| 参数更新清理错误保留 | Core 在完成信号前交付完整结果；父子宿主保留首个历史清理异常、关闭读取当前更新结果、禁止错误内容缓存或成功停用；恢复失败先撤销资格再等待候选释放 | Navigation 示例及依赖离线编译；清理失败后成功更新再关闭的示例已补充，Unity 运行与故障时序尚未验收 |
| 参数更新与换绑完成诊断 | LifecycleChanged 增加 ArgsUpdateFinished/ViewModelRebindFinished、类型化结果、沿用有界事件队列与观察者隔离；换绑版本回调后复核绑定代际；参数恢复失败先关闭再解除输入门控 | Navigation 示例及依赖离线编译；完成与关闭事件交错、观察者重入及原生门控时序未在 Unity 验收 |
| 父子视图显式 VM 换绑 | Navigator/ChildViewHandle.RebindAsync、共用生命周期、Presenter.OnViewModelChanged、命令排空与入队自等待保护、输入门控、失败恢复、旧工厂模型提交后释放、借用模型不入导航缓存、关闭等待和 Tick 暂停 | Navigation 示例及依赖离线编译通过；未新增测试或启动 Unity，失败恢复/关闭守卫/原生子树异步准备仍待运行验收，完成诊断事件已接通，超时隔离未实现 |
| 共用模型所有权 | ViewModelOwnership 在工厂调用前归实例所有；借用模型不释放，工厂模型异步优先且只释放一次；清理清除引用；子句柄直接读取生命周期模型 | Navigation 示例及依赖离线编译通过；宿主 RebindAsync 已接通；Unity 释放时序验收仍未完成 |
| 绑定换绑自等待保护 | 会话身份的异步命令执行链，换绑撤销订阅前拒绝当前或祖先会话命令自等待；退出清除会话引用；同模型无操作保留 | Navigation 示例及依赖离线编译；未新增测试，Unity 重入/异步路径未验收；宿主 RebindAsync 见下项 |
| 父子参数更新共用执行器 | Core ArgsUpdateOperation/Outcome/Host、子句柄 UpdateArgsAsync/Args/IsUpdatingArgs、单在途 Busy、停用/关闭等待、最新参数恢复、失败禁止复用、ThingItem 示例 | Navigation 示例及依赖离线编译；Unity 停用/关闭/缓存/版本变化与错误恢复未验收，超时隔离未完成 |
| 显式导航参数更新 | UpdateArgsAsync、可选候选协议、准备隔离/同步提交/补偿回滚、上下文参数同步、队列与输入阻挡、关闭取消/等待及迟到错误防缓存、可选演示 | Navigation 示例及依赖离线编译；Unity 运行/关闭守卫/取消异常未验收；VM Rebind 已接通，超时隔离未完成；子视图入口见上项 |
| 树形可观察来源 | TreeNode/TreeList，父关系与环校验、非递归先序/深度/子树索引、展开状态保留、祖先展开、原子发布、2120 节点示例 | Navigation 示例及依赖离线编译；Unity 视觉/锚点/选择/故障路径未验收，专用树输入与无障碍、异步节点加载未实现 |
| 分组可观察来源 | Core ListGroup/GroupedList，标题与正文展平、单组/全部折叠、按组键保留状态、全量容量与隐藏键校验、原子通知、100 组多模板演示 | Navigation 示例及依赖离线编译通过；Unity 锚点/选择/焦点/回收未验收；树形数据见上项；跨列与吸顶标题未实现 |
| 分页查询重置 | ResetAsync 单排空/最新候选、每页独立取消、QueryVersion 即时换代、集合原子清空、状态与错误隔离、销毁等待；Navigation 迟到页与连续查询演示 | Navigation 示例及依赖离线编译通过；Unity 运行、取消异常与并发时序未验收，分页超时隔离未完成 |
| 分页有界窗口 | PageOverflowPolicy 默认拒绝或相反端淘汰；页大小请求、原子删除与插入、窗口键目录、EvictedCount/Capacity、双方向演示 | Core/Navigation 示例及依赖离线编译与源码检查；Unity 锚点/选择/测量/释放未验收，查询重置见上项；双向游标未完成 |
| 导航身份与实例内容分离 | ViewContent 统一持有 View/VM/Presenter/执行器/实例 Lifetime/凭证，ViewInstance 保留 Handle/结果/本次激活；创建及最终释放已迁移，重复工厂创建拒绝 | Navigation 示例及依赖离线编译；仍为关闭销毁语义，顶层缓存目录/命中/新 Handle 恢复与 Unity 运行验收尚未实现 |
| 虚拟单元回调失效与创建回收 | 刷新捕获来源版本/激活身份，隐藏/尺寸/模板初始化/选择回调后复核；遍历快照避免关闭改集合；未交接新单元失败回收，创建与清理错误保留，整体清理逐项继续 | Navigation 示例及依赖离线编译；Unity 原生回调换源/关闭/清理异常运行验收尚未完成 |
| 自动测量手动预览 | NavigationDemo 可选独立预览，2000 条换行文本/两模板，VerticalLayoutGroup 首选高度、覆盖层忽略布局，每帧 2 项；定位/宽度/字号/文本更新流程及失败留场 | Navigation 示例及依赖离线编译；未启动 Unity，视觉/测量/定位/物化数量预期均待实际验收 |
| 动态测量定位就绪 | ScrollToKeyAsync 区分数据换代与几何修正，等待物化工作集测量，实际高度重新对齐后聚焦；LateUpdate 信号、校正次数与停帧期限、取消/父关闭复核 | Navigation 示例及依赖离线编译；Unity 分帧测量/目标可见性/焦点重入/停帧超时未验收 |
| 分页异步线程收敛 | 显式保存 UI 上下文，提交/终态通知/集合清理经所属线程执行；错误上下文拒绝或诊断，失败等待收敛且不在错误线程通知，提交回调拒绝重入 | Navigation 示例及依赖离线编译；上下文破坏、Post 失败、异步销毁的运行验收尚未完成 |
| 头部插入分页 | PageInsertion.Append/Prepend 共用分页协议；历史页原序原子插入头部；预取依据方向检查首尾边界；复用稳定键锚点；Navigation 历史页示例 | Navigation 示例及依赖离线编译；Unity 头部插入/网格/动态测量锚点未验收；双向独立游标与实时追尾尚未实现；页窗口淘汰见上项 |
| 虚拟列表页尾预取 | IPagedListSource 契约、Items 能力识别、按剩余条目预取、单帧单请求、错误暂停/显式重试、独立分页状态、激活跟踪与换源代际保护；Editor 阈值校验及示例 | Navigation 示例/Editor 依赖离线编译；Unity 帧驱动预取/错误表现/取消/换源未验收；反向分页及滚动速度预测未实现 |
| 列表显式分页来源 | Core PagedList/ListPage/PageLoadResult，可观察集合输入、单在途共享、等待取消隔离、页大小/总容量/跨页键/游标推进校验、失败重试、父级取消及销毁排空；Navigation 分页示例 | Navigation 示例及依赖离线编译；Unity 分页/取消/关闭未验收；自动预取、头部插入、窗口淘汰与查询重置已有实现，见对应行；双向游标未完成 |
| 虚拟列表多模板 | VirtualListTemplate/TemplateKey、代码及 Inspector 目录、模板身份复用、同键异模板先排空后替换、未知键拒绝、测量失效及选择身份保持；Editor 校验与示例混排 | Navigation 示例及 Editor 依赖离线编译；Unity 模板切换/选择/焦点/原生回收未验收；异步模板资源与分页仍未实现 |
| 动态高度增量行索引 | VirtualRowIndex 树状数组，单行修正/前缀位置/偏移定位 O(log n)；测量批次仅重算受影响行，预建估算索引，整批代际复核后发布缓存，几何总量校验 | Navigation 示例及依赖离线编译；未新增测试；Unity 测量/锚点/布局重入与真机性能尚未验收；结构和全局失效仍 O(n) 重建 |
| 虚拟列表可见高度测量 | 可选 uGUI 首选高度测量、每帧数量限制、条目身份缓存、宽度/集合 Update/显式失效、删除及父激活清理、显式高度优先、稳定位置修正及错误重试，Editor 检查预算 | Navigation 示例与 Editor 依赖离线编译；Unity 自动布局/宽度/字体/锚点未验收；高度修正仍 O(n) 重建索引，增量索引与测量就绪协议未完成 |
| 虚拟列表显式动态高度 | VirtualListItem 可选正高度，同键替换更新；单列/网格行最大高度位置索引、可见范围二分查询、单元尺寸、稳定键锚点、实际高度定位与列数切换；示例新增变高流程 | Navigation 示例及依赖离线编译；Unity 布局/锚点/复用未验收；自动测量缓存、分帧测量与增量高度索引尚未实现 |
| 缓存重新激活入口失败收尾 | 缓存项移出后明确接管失败清理；覆盖 Scope 入口校验失败及操作登记前取消，复用同一 BeginClose，保留原始失败并聚合独立清理错误 | Tabs 示例及依赖离线编译；入口取消/状态变化与释放异常的 Unity 运行验收尚未完成 |
| Tab 有界缓存过期扫描 | UI 同步上下文下单循环每秒扫描，与目录/主动清理共享维护队列，维护忙碌不追加任务，父取消停止；无上下文宿主可从所属线程 RefreshCache | Tabs 示例及依赖离线编译；Unity 定时回收/慢清理/销毁竞态未运行验收，导航缓存与全局预算仍未完成 |
| Tab 停用缓存 TTL | 可选正期限，单调时钟，实际入缓存起计时，命中过期先释放再创建，目录失效维护兼查过期；示例提供期限配置 | Tabs 示例及依赖离线编译；Unity 过期重建/释放失败未验收；有界扫描见上项，导航缓存 TTL 尚未实现 |
| Tab 策略手动示例与迟到清理 | Inspector 暴露缓存/期限/隔离/模拟延迟配置，启动时固定工厂设置，提供缓存清理及数量菜单；迟到候选已关闭时等待原 CleanupCompletion 才归还隔离名额 | Tabs 示例及依赖离线编译；手动步骤已补齐，Unity 超时/缓存/迟到清理仍未验收 |
| 子视图准备超时隔离 | 可选期限、取消未结束准备隔离、有界数量准入、迟到候选只清理、请求取消源延迟释放、最终销毁排空；Tab 接入配置与数量诊断 | Tabs 示例及依赖离线编译；Unity 超时/迟到/取消/销毁未验收；不覆盖清理与导航超时，字节预算及全局隔离未接入 |
| Tab 主动缓存清理 | ClearCacheAsync 复用串行失效队列，等待物理释放，重复共享，等待取消隔离，逐项失败继续并聚合；拒绝回调自等待，父关闭等待清理 | Tabs 示例及依赖离线编译；Unity 低内存/重复清理/取消/释放失败未验收；不关闭当前内容或永久禁用缓存 |
| Tab 缓存估算字节额度 | 定义提供可空 EstimatedRetainedBytes，缓存可配 MaxEstimatedBytes；未知/单项超额拒绝缓存，容量与估算额度共同淘汰，合计诊断及溢出保护 | Tabs 示例及依赖离线编译；只约束停用缓存的项目估算，不限制在途/实际进程内存；Unity 运行与全局资源预算未验收 |
| Tab 有界实例缓存 | TabCacheOptions 的 CacheRecent/KeepSelectedTabs、显式容量、Inactive 准入、同定义复用、强制重载、串行淘汰与目录失效、父关闭收尾；Slot 只将成功替换的旧稳定内容交接，失败继续关闭 | Tabs 示例及依赖离线编译；Unity 命中/淘汰/重入/失败清理未验收；停用缓存估算额度已接入；全局预算、超时隔离和父缓存重开未实现 |
| 子视图停用并排空 | Scope.DeactivateAsync 将稳定 Active/Retained 子项隐藏并排空旧激活，成功进入 Inactive；同实例新激活复用既有入口，关闭等待过渡退出；失败/取消关闭原实例，父级跟踪操作 | Tabs 示例及依赖离线编译；Unity 关闭/停用/恢复竞态未验收；Tab 有界缓存与淘汰已接入，超时隔离尚未实现 |
| Tab 候选有效性复核 | 普通准备前后复核请求/定义/可用性，校验失败只回滚本 Scope 的 Prepared 候选；Slot 绑定提交前/显示前验证，普通切换与恢复共用；示例新增可选保留/恢复配置 | Tabs 示例及依赖离线编译通过；Unity 加载中禁用/定义变化/候选显示与回滚未验收 |
| Tab RestorePrevious | 与三种 PendingDisplay 组合保留唯一旧实例；目标失败后经同一 Slot 恢复，校验版本/定义/可用性；成功同步恢复选中与显示，原请求仍失败并独立通知；失败回错误占位，取消/父关闭/新选择失效处理 | Tabs 示例及依赖离线编译；Unity 恢复/重入/取消/清理未验收，有界缓存已接入，超时隔离未实现 |
| Tab KeepPrevious | 一份稳定旧内容保留显示、冻结业务与输入；重新选择兼容旧定义排空旧激活后复用实例；新内容显示前撤下旧画面；失败/取消清空，目录失效回收，保留/退役回调前置拒绝重入 | Tabs 示例及依赖离线编译；Unity 视觉/切换/取消/资源释放未验收；RestorePrevious 已接入，有界缓存已接入，超时隔离未实现 |
| 父子视图公共创建协议 | ViewPresenterLifecycle.Create 共用模型/Presenter 工厂、外部模型借用、工厂模型拥有、异步优先释放登记、Tick 唯一性及间隔初始化；所有权登记后复核取消 | Tabs 示例及依赖离线编译通过，原有重复创建逻辑已删除；Unity 工厂失败/取消/借用释放运行未验收 |
| 父子视图共用 Presenter 执行器 | ViewPresenterLifecycle 统一创建回调/绑定/打开/异步打开/关闭/销毁与阶段标记，ViewInstanceCleanup 统一实例排空/逐项退订/视图资源归还；重新激活重用原 Presenter | Navigation 示例及依赖离线编译通过；模型工厂登记已收敛，见上项；完整缓存/隔离与 Unity 运行验收未完成 |
| 父子视图公共准备协议 | Core 内部 ViewPreparation 共用隐藏与子激活建立、绑定模型校验、异步打开和子准备顺序；导航准备新增线程/激活/状态复核，关闭后停止后续阶段 | Tabs 示例及依赖离线编译通过；尚非完整统一执行器，Unity 取消和回调重入未验收 |
| 父子视图公共激活收尾 | Core 内部 ViewActivationCleanup 被 Navigation/ChildViews 共用，统一关闭回调、解绑与 Lifetime 排空顺序，逐步异常隔离，保留各自回调重入保护和导航错误分类 | Tabs 示例及依赖离线编译通过；创建/准备/最终实例释放尚未统一，Unity 运行未验收 |
| 子视图命名统一 | Projection 系列改为 ChildView，程序集/命名空间/目录/构建引用/示例/文档同步，View.ChildViews 和 IChildViewElement，保留元数据 GUID | 7 个示例/编辑器构建入口及依赖离线编译通过，程序集与项目引用检查通过；公开 API 破坏性改名；公共生命周期现已由 Core 的 ViewPresenterLifecycle 统一，宿主调度仍各自负责；Unity 导入与运行未验收 |
| 子视图保留停用 | RetainAndDeactivate/Retained 单向撤销业务、保留显示与原资源所有权；失败关闭同一句柄，最终关闭先撤销保留；父子保留取得隔离；TabBar/AsyncContent 直接订阅延后渲染 | Tabs 示例及依赖离线编译；运行重入与资源持有未验收；重新激活入口见下项，Tab KeepPrevious 已接入，RestorePrevious 已接入，运行未验收 |
| 保留子视图重新激活 | 同 Scope 的 Retained 实例排空旧激活后复用 View/VM/Presenter/Lease，建立新 Lifetime 与绑定，只返回 Prepared；关闭等待恢复流程退出，旧 ActivationContext 拒绝跨激活操作，Slot 支持同句柄提交；准备回调后复核状态 | Tabs 示例及依赖离线编译；Unity 重入/取消/资源清理未验收；Tab KeepPrevious 已接入，RestorePrevious 已接入，运行验收与超时隔离仍未完成 |
| 拖放指针身份隔离 | 输入模块/指针 ID/按键共同匹配；悬停保存不可变身份并查询活动来源；兼容旧模块鼠标按键 ID、投放后释放登记、模块切换取消、代际复核；捕获释放不改写被复用到其他键的事件 | 根据本机 uGUI 1.0.0/Input System 1.14.2 源码核对，DragDrop 示例及依赖离线编译；真实多键/触摸/模块切换未验收，完整后端捕获与关闭手势消费仍待实现 |
| 帧驱动退出转场 | IExitTransitionView/uGUI 淡出、显示保留、Closing 保留渲染序列与覆盖/模态屏障、视觉退出后清理；独立帧预算、ReducedMotion、强制关闭/Shutdown 收敛；虚拟列表状态提示延后应用、编辑器时长校验与现有预览补充淡出 | Tabs/Navigation 示例与 Editor 依赖离线编译通过；Unity 视觉、焦点、重入和失败路径未验收；关闭手势持续消费仍未实现 |
| 显示门控保留协议 | Core 可选 IVisualRetentionView、uGUI 当前透明度/可见性保留与输入禁用、子 Scope 递归取得/失败回滚/逐项释放；阻止释放重入与保留期间新建子项，新激活前必须结束保留 | Tabs 示例及 Core/ChildViews/Navigation/UGUI 依赖离线编译；退出转场与虚拟列表状态已接入，见上项；手势屏障、Unity 视觉与重入未验收 |
| 关闭绑定退役 | 导航关闭与子视图停用先冻结已提交的标准绑定，再取消激活；允许源写入回调同步关闭时冻结正在提交的会话，停止剩余提交；退订错误收集后继续关闭 | Core/Navigation/ChildViews、Tabs 示例与渲染依赖离线编译通过，格式检查；关闭回调重入和异步运行场景未验收，完整退出画面保留仍未完成 |
| 父取消与子视图释放分离 | 父取消停止子项工作、冻结稳定的标准绑定并隐藏；稳定子项保留至显式关闭或父激活释放 Scope，避免 Scope 因父取消提前释放父关闭回调仍可能访问的子项；取消错误继续收集 | ChildViews 与 Tabs 示例及依赖离线编译、格式和调用链检查通过；多层子树、重入及失败路径未运行验收；已接入退出转场显示保留，见上项 |
| 对话框原生取消入口 | DialogCancelInput 将选中控件的 Cancel 转交同 View 取消按钮；复核原生交互状态和 View 输入门控、先消费事件；向导为两个按钮配置入口并显式设置默认取消焦点 | Dialogs 运行时/Editor 离线编译通过；原生键盘/手柄、焦点迁移与模态叠加尚未在 Unity 验收 |
| 页面向导源码可读性 | 模板按行组织输出，生成构造函数、关闭命令与路由方法展开代码块；页面、参数、结果、Presenter 与资源入口统一中文说明 | Editor 离线编译与模板格式检查通过；未逐一编译所有向导配置生成的源码，未运行 Unity 向导 |
| 渲染与编辑器可读性 | Rendering、Adapters、Editor 展开控制流和代码块，长签名按顶层参数换行；保留中文说明 | 9 个适配/Editor 构建入口通过，覆盖基础 UGUI、TMP 和模块依赖；格式与注释扫描通过；Unity 导入和实际交互仍未验收 |
| 导航层可读性 | 32 个源码文件展开控制流、调整长签名；INavigator 补充同步/异步打开、替换、关闭等待、批量关闭、预加载与关机的中文契约 | 大括号改写之外语法标记一致；Navigation 编译及格式检查；完整导航运行验收仍未完成 |
| 投影层可读性 | 展开控制流、调整长签名，补充提交、局部门控、Unit 结果、旧资源释放与 Tick 策略中文契约 | 非大括号语法标记一致；ChildViews 离线编译及格式检查；未替代异步生命周期运行验收 |
| Core 其余目录可读性 | Input/Observables/Attributes/Diagnostics/Infrastructure/Collections/Accessibility/Views/Presentation 展开控制流与代码块；补充输入门控与渲染最小契约中文说明 | 非大括号语法标记一致，Core 离线编译与本轮目录格式检查通过；不代表全框架格式和运行验收完成 |
| 命令通知线程归属 | 命令在创建线程求值和执行；后台状态通知 Post 回创建上下文并再次检查线程；无上下文或派发失败仅报告诊断，不运行绑定观察者 | Core 离线编译；Unity 上下文派发/销毁竞态仍待运行验收；不派发业务委托 |
| 排队命令线程身份 | 在等待队列前创建调用上下文，恢复后按原调用线程检查，阻止错误线程被重新认作合法来源 | Core 离线编译；通知派发保护见上项，运行验证仍待完善 |
| 命令上下文线程校验 | IsCurrent 在访问来源状态前校验创建线程；Apply/RequestClose/Complete 经 ThrowIfInvalid 共用检查，后台误用在业务写入前失败 | Core 离线编译和调用链检查；Unity 后台续接场景未运行验收；不提供线程派发 |
| 绑定与命令层可读性 | 17 个源码文件展开控制流，长方法签名按顶层参数换行；补充绑定会话与命令上下文中文契约 | 大括号改写前后其余语法标记一致；Core 编译及格式检查通过；命令竞态仍待运行验收 |
| 生命周期与资源层可读性 | Lifetime 与 Resources 展开代码块，锁与控制流补齐大括号；保留中文所有权契约，补充操作计数说明 | 排除新增大括号后的语法标记一致；Core/Resources 离线编译与格式检查；不替代运行竞态验收 |
| Tab 条目唯一性与初始化 | 运行时及 Editor 拒绝一个 Button 对应多个条目；初始化先校验全部条目和模板，再挂接点击监听 | Tabs 运行时/Editor 离线编译；Unity 配置错误场景未运行验收 |
| Tab 编辑器结构校验 | 独立 Tabs.Editor 注册 TabBar/AsyncContent 校验，检查键、按钮、模板、选择标记与内容挂载/提示分支；按运行时规则发现缺省引用 | Editor 扩展离线编译；Unity 导入、注册执行与 Prefab 诊断仍待运行验收 |
| Tab 按钮输入门控 | 预制与动态按钮共用选择入口，回调时复核元素、Button 和父 View 的有效输入状态 | Tabs 示例离线编译；原生事件与模态阻挡运行场景未验收 |
| 源码注释中文统一 | Runtime、Editor、Samples 中英文说明转换为中文，覆盖行内/块/XML 注释；页面向导生成的项目源码注释同步中文化；保留自动生成标记与代码标识符 | 全仓注释扫描及离线编译；不改变运行协议 |
| Dialogs 适配拆分 | 确认框工厂与向导独立程序集，共享 PrefabAuthoring 文本构建；基础 UGUI 不再引用标准模块 | 运行时/Editor 离线编译；依赖检查；Unity 导入、菜单与确认流程未运行验收 |
| Themes 可读性 | 展开代码块与控制流；补充主题预校验、发布失败边界、类型化值、目录回退、订阅所有权及字号缩放注释 | Themes 离线编译与格式检查；未新增行为，原生字体与资源主题能力仍待完善 |
| Localization 可读性 | 展开代码块与控制流；补充目录加载、回退语法与数字格式区别、订阅所有权、重入刷新和释放说明；Provider/文本结果独立文件 | Localization 离线编译与格式检查；不代表字体、RTL 排版与 CLDR 已实现 |
| DragDrop 可读性 | 展开控制流、调整长行；中文契约明确源/目标生命周期、一次提交、合作取消、业务成功不可回退和视觉收尾顺序 | DragDrop 离线编译与格式检查；未新增行为，未运行 Unity |
| Dialogs 可读性 | 展开控制流与命令回调；补充串行容量、取消与返回结果、清理顺序和自等待约束；ConfirmationPresenter 独立文件 | Dialogs 离线编译与格式检查；未新增行为，未运行 Unity |
| Notifications 可读性 | 展开代码块、补齐控制流大括号；中文注释明确非抢占优先级、同级 FIFO、去重不刷新、有效期与展示时长、ID 关闭和重入发布 | Notifications 离线编译与格式检查；未新增行为，未运行 Unity |
| Loading 可读性 | 展开代码块与控制流大括号；公开 API 中文注释补充提示令牌、任务所有权、线程、延迟与输入锁、进度汇总及重入发布约束 | Loading 离线编译和格式检查；未新增行为，未运行 Unity |
| Tab 适配与可读性 | 独立 UGUI.Tabs，示例引用同步；Tabs 模块及适配展开代码块、补齐大括号，状态与控制器补充契约注释；统一 editorconfig | 示例及依赖离线编译；格式检查；Unity 导入与运行未验收，其他模块可读性仍待整理 |
| 拖放适配拆分 | 独立 UGUI.DragDrop 与 Editor 扩展，源/目标校验通过注册接入，基础 UGUI/Editor 去除拖放引用，示例与元数据同步迁移 | 适配/编辑器/示例离线编译及依赖检查；Unity 导入、校验注册及真实拖放未运行验收 |
| 主题适配拆分 | UGUI.Themes/TMP.Themes 独立程序集，基础 UGUI/TMP 移除 Themes 引用，原类型名和元数据保留，TMP 安装条件同步 | 离线编译及依赖检查；Unity 导入和绑定行为未运行验收；现有模块适配已拆分，仍为同一 UPM 包 |
| Loading/Notifications 适配拆分 | 两个独立 UGUI 适配及 Editor 扩展程序集、基础 UGUI/Editor 无反向引用、元数据 GUID 保留、离线构建项目同步 | 两个扩展/基础 Editor/TMP 编译；Unity 导入/Prefab/校验注册未运行验收；其他模块适配未拆分 |
| Loading 策略替换边界 | ILoadingSource 只读状态契约、独立公开校验 Snapshot、默认 Scope 实现、UGUI 借用接口；任务/时钟/输入锁归服务所有者 | Loading/UGUI/Editor 编译；第三方来源运行未验收；适配程序集已拆分，见上项 |
| 通知策略替换边界 | INotificationSource 单显示契约、公开校验 Snapshot、默认队列实现、UGUI 借用接口 | Notifications/UGUI/Editor 编译；第三方来源运行未验收；多条显示协议未完成；适配程序集已拆分，见上项 |
| 资源槽冻结基础 | 保留当前 Lease、代际失效/取消在途、拒绝替换清空、最终销毁排空、Lease.Asset 外部访问后复核 | Resources/UGUI/Editor 编译；迟到/重入/释放运行未验收；尚未联动完整页面冻结 |
| 绑定冻结基础 | 可选接口、标准泛型上下文 Frozen、停止数据流/退订/取消命令、保留当前值、正式解绑再执行控件终结、重入解绑延后 | Core/UGUI/Editor 编译；冻结/异常/资源/恢复运行未验收；非完整画面冻结，退出转场/KeepPrevious 未完成 |
| 子视图取消异常隔离 | 自定义绑定取消失败后继续当前关闭与兄弟取消；错误归入 Scope 清理；Dispose 发布后仍启动收尾 | ChildViews/UGUI/Editor 编译；自定义绑定异常及兄弟释放运行未验收 |
| 进入转场配置与示例 | 结构/契约校验检查序列化时长及根 CanvasGroup；Navigation 可选独立淡入页、等待就绪与 finally 关闭 | 示例/Editor 编译；实际 Inspector/淡入/输入未运行验收 |
| 帧驱动进入转场 | 可选 Core 同步采样合约、UGUI 淡入、输入门控、异步等待就绪、提交后取消收敛、帧预算降级/失败补偿关闭、ReducedMotion | Navigation/UGUI/Editor 编译；Unity 动画/焦点/关闭/取消竞态未验收；退出转场见上项，跨手势屏障尚未实现 |
| 首次就绪基础协议 | Handle 查询/重复等待、Outcome 快照、表现收敛完成、就绪前关闭/失败、单等待取消隔离、Replace 共用 | Navigation/UGUI/Editor 编译；运行竞态未验收；进入转场及帧预算降级见上项，退出转场见上项 |
| 导航绑定激活门控 | 逻辑提交与激活完成分离、事件/绑定提交期间合并表现重算、未激活隐藏/无焦点/无 Tick、关闭撤销资格 | Navigation/UGUI/Editor 编译；事件重入与绑定失败运行未验收；完整转场 Readiness 未实现 |
| 按层/全部批量关闭 | 已提交快照、前到后串行守卫、逐项不可变结果、部分失败继续、等待取消/剩余项标记、原清理任务保留、有界并发与自等待预检 | Navigation/UGUI/Editor 编译；Unity 运行未验收；共享依赖 InUse/拓扑顺序尚未实现 |
| BeginOpen 请求控制 | OperationId/Cancel/可重复等待 Completion、直接复用 OpenAsync、完成自动释放取消源、取消回调内完成延迟释放 | Navigation/UGUI/Editor 编译；排队/取消/宿主联动未运行验收；完整转场仍未实现 |
| 准备取消的线程归属 | Core 内部取消注册、Navigation/ChildView 准备复用、同线程即时/跨线程 Post、退出排空与迟到失效、缺失/错误上下文诊断 | Core/Navigation/ChildViews/UGUI/Editor 编译；后台取消与 Unity 帧调度竞态未运行验收 |
| CloseOldest 超限事务 | RoutePolicy 显式策略、创建顺序选源、OpenAsync/PostOpen 复用 Replace、队列许可转交、旧身份/清理任务保留、同步 RequiresAsync | Navigation/UGUI/Editor 编译；容量/守卫/取消/清理竞态未运行验收 |
| 导航关闭排空 | Open/OpenAsync/PostOpen/Replace 统一请求计数；Shutdown 等待请求完整退出，覆盖确认时释放队列的情形；剩余实例清理独立等待 | Navigation/UGUI/Editor 编译；确认/取消/销毁竞态未运行验收；不合作任务超时隔离未实现 |
| 导航 Replace 基础事务 | 独立候选准备、同 Route 名额借用、关闭守卫与版本复核、确认阶段释放队列、双状态提交、旧清理独立结果、表现重算合并 | Navigation/UGUI/Editor 编译；取消/重入/失败/焦点未运行验收；退出转场/readiness 已有基础实现，运行未验收；CloseOldest 见上项 |
| 拖放示例 | 可导入 UPM 示例、Accept/Reject/Fail、异步提交、影子与悬停、禁用取消/销毁清理 | 示例编译通过；Unity 输入与运行未验收 |
| 浮层 Prefab 模板 | 菜单/Tooltip 创建菜单、Preview Scene 构造、标准控件/引用/Cancel/背景配置、结构及基本语义检查、拒绝覆盖 | Editor/TMP.Editor 编译；实际资产创建、导入、布局和运行未验收；无业务 VM/Route 自动生成 |
| 菜单原生输入适配 | Button Cancel 转发、同节点 Image 外部按下背景、实时射线门控、关闭/禁用后背景清理、容量/Cancel/背景编辑器检查 | Play Mode 的 EventSystem 接口派发验证 Down 跳过禁用按钮、Cancel 消费并关闭；设备输入、背景射线、按下释放与跨手势协议仍待验收 |
| 拖放条件与悬停反馈 | 可选 canAccept/CanDrop、提交前复核、条件重入拒绝、16 指针复用快照、IsDropAllowed/可选装饰高亮、异常暂停与 Editor 配置检查 | DragDrop/UGUI/Editor/TMP.Editor 编译；实际规则变化/射线/多指针/取消竞态未验收 |
| 拖拽图标影子 | 可选 Icon/Root、单个被动 Image、指针平面跟随、保持源布局、投放/取消/失败/换绑/销毁清理、代际隔离、Editor 引用检查 | UGUI/Editor/TMP.Editor 编译；实际显示/坐标/资源/销毁未验收；自定义效果与资源持有扩展未实现 |
| DragDrop 原生事件适配 | 源/目标 Element、泛型绑定、Begin/Drag/End/Drop、pointerId/单会话、EventSystem 归属释放、有界目标在途、失效/换绑取消、旧绑定结果隔离 | UGUI/DragDrop/TMP/Editor 编译；真实输入/取消/指针清理未验收；OS capture 尚未实现；标准影子清理见上项 |
| DragDrop 会话核心 | 泛型载荷、单次提交/重复共享、Source/Target Lifetime 取消、目标在途跟踪、捕获所有权与一次结果收尾、成功提交优先于迟到取消 | DragDrop/Core 编译；线程/取消/异常/销毁未运行验收；Unity 原生事件适配见上项；OS capture 未实现；标准影子清理见相关项 |
| 投影回滚与 Tab Retry 门控 | ChildViewSlot 回滚恢复旧 Handle 原有本地显示/交互；AsyncContentElement Retry 原生事件遵守 View/按钮门控 | ChildViews/Tabs/UGUI/TMP/Editor 编译；失败回滚与输入路径未运行验收；KeepPrevious 仍需冻结视觉与旧激活停用，不视为完成 |
| 菜单动态层级刷新 | RefreshItems 增删/重排、保留按钮导航快照和有效选择、回调内合并、容量失败关闭与恢复 | Play Mode 探针验证禁用 First 后刷新选择 Second；实际增删/重排、回调重入和失败清理仍待验收；数据到按钮生成仍由项目绑定/投影完成 |
| 单层 ContextMenu 控制 | 原生 Button 命令复用、有界项目收集、上下导航/跳过禁用/循环、当前菜单焦点约束、导航快照恢复、原焦点条件恢复、返回/外部点击转发 | Play Mode 探针验证显示选中 First、Down 跳过禁用项、Cancel 消费并关闭后恢复 Anchor；真实设备输入、异步命令、外部点击和故障清理仍待验收；自动数据模板与子菜单未实现 |
| 浮层编辑器校验 | View 结构/契约入口检查 Loading/通知引用、进度类型、浮层布局冲突、Tooltip 引用/延迟/射线配置，保留原有 View/Element 边界 | Editor/TMP.Editor 编译通过；Unity Inspector 执行、诊断准确性和真实射线未运行验收 |
| Tooltip 触发 | 悬停/选中延迟、独立 MonoBehaviour、多指针意图、点击/关闭抑制、共享 Anchor 归属、View 门控隐藏与禁用清理、自动/手动时钟 | Play Mode 临时对象探针验证延迟显示、离开关闭、选中显示、按下抑制和再次选中显示；真实设备输入、多指针、共享归属、门控与销毁仍待验收；动态内容与异步 Tooltip 服务未实现 |
| 锚点浮层基础 | 独立放置计算、同 Canvas 锚点跟随、首选/反向与边界夹取、目标失效隐藏、按 View 门控的返回/外部点击入口 | UGUI/TMP/Editor 离线编译；Unity 几何/布局/输入未验收；完整 Tooltip/ContextMenu 服务、首帧 readiness、焦点与全局输入路由未实现 |
| 通知队列 | 独立 Notifications、有界容量、key 去重、优先级/FIFO、排队与展示过期、ID 关闭、UGUI 借用显示与原生关闭按钮 | Notifications/UGUI/TMP/Editor 编译通过；实际计时/排序/显示/重入/门控未运行验收；多条可见、动作与动效模板未实现 |
| 通用 Loading | 独立 Loading 模块、Lifetime 兜底令牌、容量/线程约束、加权进度、不定进度、延迟提示、显式 InputGate、UGUI 借用显示与清理 | Loading/UGUI/TMP/Editor 编译检查；实际显示、重入、取消和门控未运行验收；无内置自动时钟或全局遮罩 |
| TMP 基础控件 | 独立可选 MUI.TMP：文本 Content、输入框 Value/ReadOnly/EditingEnded/Submitted、下拉 Value/Options/占位状态；Editor 按原生类型扫描挂载 | TMP/Editor 独立 .NET 编译通过；Unity 有/无 TMP 导入、渲染与交互未验收；TMP 基础结构校验已接入（见编辑器项），IME/软键盘协议未完成 |
| 生命周期 | Lifetime 所有权登记、取消、在途操作跟踪、逆序异步清理、错误聚合 | .NET 编译、Unity 编译；尚未完成故障运行验收 |
| Prefab 预加载 | IPreloadViewProvider/IPreloadLease、Lifetime 托管预加载、LoadedPrefabViewProvider、共享实例化工厂 | Unity：独立持有、销毁后释放、迟到取消回收通过；Navigator 目录已接入；具体资源库未完成 |
| 资源基础 | IResourceLoader、可选 ISynchronousResourceLoader、IResourceLease、幂等释放、异步托管及迟到清理 | 原有 .NET/Unity 编译；新增同步能力仅 .NET 编译，具体内置适配见下项 |
| 主题值与切换 | 独立 Themes、类型化 Token/目录/fallback、IThemeProvider/常驻提供者、切换前订阅校验、Lifetime 订阅；uGUI 颜色/间距/Selectable 状态配色 | Themes/UGUI 编译通过；切换/取消/实际状态及清理未运行验收；字体资产、编辑器与页面转场适配未实现；用户偏好适配见下项 |
| 控件无障碍语义 | Core IAccessibleElement/角色/状态，Element 可绑定名称/说明/值/顺序/隐藏，标准控件默认角色/部分原生状态，可见子树快照，Inspector 基本检查，确认页动态按钮名称 | Core/UGUI/TMP/Editor/TMP.Editor 编译通过；语义过滤/绑定/序列化/诊断未运行验收；平台桥接、语义事件与键盘可达性验收未完成 |
| 用户表现偏好 | Core UIUserPreferences、主题基准×用户缩放、Text/TMP 字号、ColorTint 减少动画与解绑恢复作者时长、Lifetime 订阅 | Core/Themes/UGUI/TMP 编译通过；字号/布局/动画与池化恢复未运行验收；持久化、页面动效和完整无障碍未实现 |
| 本地化文本 | 独立 Localization、语义 key/参数、显式复数、Culture 格式、回退目录/方向信息、ResourceSlot 切换、Lifetime 文本订阅、JSON TextAsset 提供者 | Localization/UnityResources 编译通过；JSON/切换/取消/目标刷新未运行验收；完整 CLDR、RTL shaping、字体/列表联动未完成，主题值模块见上项 |
| Unity Resources 适配 | 独立 MUI.UnityResources；原生同步/异步加载、独立 Lease、路径/类型/线程检查、在途容量、原生完成后取消收敛 | .NET 编译通过；Unity 运行未验收；Lease 释放托管持有，原生内存由项目统一 UnloadUnusedAssets，不提供逐项原生引用计数或远程加载 |
| 动态资源槽 | ResourceSlot：代际校验、淘汰请求主动取消、先赋新值再释放旧值、失败保留、Clear、销毁排空、释放错误汇总 | .NET 与 Unity 原有编译通过；新增淘汰取消仅 .NET 编译检查，并发与失败路径运行验收待完成 |
| 列表基础单选 | SelectedKey/SelectedIndex、生成 TwoWay、根按钮选择、可选高亮、键保留/缺失清空与监听释放 | .NET 编译及生成代码检查通过；Unity 场景已补充但启动被自动审批权限限制拦截，运行未验收 |
| 屏外条目定位/焦点 | ScrollToKeyAsync、键索引、物化后选择、请求/源版本与激活复核、取消等待 | Unity：Item 9000 实际选中、旧请求淘汰、源更新淘汰、预取消不滚动、InputBlocked/NotFound 通过；基础单选已编码但未运行验收，完整方向导航未完成 |
| 列表状态与重试 | Status/Error、可选加载/空/错误节点、RetryAsync、失败暂停自动刷新、父取消回 UI 线程 | Unity：容量错误、暂停、显式恢复 Ready、空态展示通过；异步条目失败/重复激活等故障路径待完成 |
| 固定行高虚拟列表/网格 | VirtualListElement、FixedGridLayout、生成属性绑定、单列/等宽网格视口/overscan 物化、嵌套投影复用、稳定键锚点、池收缩与父生命周期 | Unity：100/10000 数据均 8 个原生条目、滚动复用 8 个旧节点、插入保持锚点、清空下一帧原生 0；高级布局/选择/性能验收未完成 |
| 集合数据 | ObservableList、只读集合契约、Add/Remove/Replace/Update/Move/Reset、事务批次、版本与可选稳定键校验 | 编译通过；运行验证见下；固定行高虚拟列表已消费通知；单项 Add 原地追加与增量键校验，Move 局部移位、NotifyUpdated 无整表复制，仅编译检查；虚拟列表尾部追加及单条同键 Update/Replace 已增量消费、仅编译检查；其他事务结构复制及混合变更全量处理仍待性能优化 |
| 属性状态 | ObservableObject、ViewModel、ObservableProperty 生成 | 设置页的生成结果在 .NET 与 Unity 编译通过 |
| 导航生命周期事件 | 类型化提交/关闭事件、Route/Handle/顺序/提交版本、原因与关闭结果、有界分发和异常隔离 | Navigation 编译通过，日志示例已补充；嵌套关闭/异常/溢出运行验收待完成 |
| 安全区布局 | SafeAreaFitter、屏幕安全区/Canvas 视口交集、持续变化检测、轴开关、预览覆盖与通知 | 编译通过，示例已补充；Unity/真机旋转与刘海屏未验收；软键盘、多显示器和复杂区域未实现 |
| 子视图 Tick | IChildTickHost、活动子树事件注册、Scope 快照、父子逐级驱动、模板隐藏暂停、共用计时/所有权 | Navigation 编译通过；ThingItem 子驱动示例已补充，Unity 运行未验收；多层树/低频/复用故障场景待完成 |
| 顶层页面 Tick | IViewTick/ILowFrequencyViewTick、非缩放时钟、按需登记、覆盖/隐藏暂停、低频余数与补帧上限、回调隔离 | 编译通过；控制时钟的示例已补充，Unity 运行未验收；子视图 Tick 已编码；完整故障/性能验收待完成 |
| 属性通知批处理 | DeferNotifications、嵌套作用域、同名合并、显式 Flush、重入批次上限与异常汇总 | Settings 编译通过；运行验收及自动帧末调度未完成 |
| 属性绑定源重入 | 最新源值补发、回声跳过、TwoWay 读取最终规范值、32 次不收敛拒绝 | Navigation/Settings 编译通过；生成绑定 VM 重入示例已补充，Unity 运行仍待权限恢复后验收 |
| 绑定 | 名称/类型索引，属性与命令绑定，准备阶段门控，同步解绑与异步任务清理，换绑恢复、外部解绑打断换绑、构建中关闭延迟清理；初始提交中解绑停止剩余写入；运行时拒绝属性多写入冲突 | 设置页双向绑定与命令 Play mode 演示通过；打开事件/初始写入触发关闭修复仅编译检查；换绑中关闭协调已编码；复杂故障分支待运行验证 |
| Presenter/实例 | 类型化参数/结果、可选异步钩子、两级 Lifetime 驱动、打开回滚与最终释放 | 导航示例已运行同步/异步打开和结果关闭；扩展失败路径待完整验收 |
| 标准确认对话框 | 无 Unity 的 Dialogs 模块、容量限制/串行服务、取消后强制关闭与排空、标准 uGUI 状态/绑定/Manifest/Route、Prefab 创建菜单；可用于导航/Tab 守卫 | Dialogs/UGUI/Editor 编译通过；Prefab 创建、布局、键盘、取消/宿主关闭/Tab 联动未运行验收；Alert/多按钮/TMP 模板/超时隔离未实现 |
| 关闭守卫/结果许可 | Presenter ICloseGuard、Allow/Deny/NeedsConfirmation、可注入确认服务、版本复核、重复关闭共享、CompleteAsync 候选提交、ForceCloseAsync、宿主绕过与激活跟踪 | Navigation/UGUI/现有 Navigation 示例编译通过；守卫/确认/强制竞态/结果重试未运行验收；标准确认页已编码见下项，超时隔离待实现 |
| 基础子视图 | 独立 ChildViews 程序集，Template/Handle/Scope、同步/异步准备、显式提交、父子门控、关闭取消与借用节点复用 | .NET/Unity 编译；ThingItem 的隐藏准备、提交、局部隐藏、借用重开和父关闭 Play mode 演示通过 |
| 静态子视图绑定 | NestedViewElement、生成工厂解析、借用 VM、换绑排空/版本校验/失败恢复、父首帧准备检查、父提交驱动子反向写入 | .NET/Unity 编译；初始绑定、更换 VM、null 清空与再绑定 Play mode 演示通过；故障与首帧异步拒绝分支待运行验收 |
| 动态子视图 | ChildViewSlot 有界替换、候选提交/失败保留/迟到回收；DynamicViewElement Source/VM 分离、生成绑定与挂载 | .NET/Unity 编译；淘汰慢加载、保留无模型实例、重绑及清空 Source 的 Play mode 演示通过 |
| 异步 Tab 基础 | MUI.Tabs 控制器/快照/结果，ReleaseOnLeave、重复等待隔离、错误重试；TabBar/AsyncContent 适配器 | Unity Play mode：等待取消、禁用拒绝、加载可交互、错误重试、快速切换和父清理通过；缓存尚未实现；离开守卫见下方验证记录 |
| 覆盖与基础模态 | None/BlockInput/Hide、独立焦点策略、OnCovered/OnRevealed、IModalView 与同级 uGUI 屏障 | Unity：门控与关闭恢复通过；下一帧 Canvas 更新后窗口外射线命中屏障；同帧 readiness 与完整输入协议未完成 |
| 基础焦点协调 | IInputView 状态通知、有效焦点重算、IFocusView、原生选择记忆/回退与帧级模态约束 | Unity EventSystem API：越界恢复、禁用回退、令牌失焦/恢复、关闭恢复原选择、局部显隐通过；真实键盘/手柄和事件级约束待完成 |
| 输入令牌活动记录 | 激活只登记一个管理器、提前释放即时移除、Context.InputBlockerCount、关闭重入回收 | 编译通过；1000 次短令牌示例已补充，Unity 运行与内存验收待完成 |
| 输入令牌 | Core InputGate、IInputView、Context.BlockInput 激活托管、父子门控、Button/绑定命令事件入口拦截 | Unity：2→1→0 独立释放、重复释放、模态 Back Blocked、关闭清理通过；Slider/Toggle/InputField 原生事件门控已编码、仅编译检查；输入事件级焦点陷阱待完成 |
| 返回策略 | Close/Ignore/Block/HandleByPresenter、非历史模态优先、可交互层级顺序、处理器重入拒绝 | Unity 示例：Blocked、层级优先、局部 Handled 后 Close、递归 Reentrant 通过；平台输入适配未完成 |
| 基本导航 | Route、Handle、顺序队列、来源关闭、结果等待、Back/焦点/排序、有界终态账本、Shutdown | Unity Play mode 基本链路通过；不等于完整窗口策略 |
| uGUI 宿主/Prefab | UIHost、常驻 PrefabViewProvider、隐藏创建、Lease 释放、逐帧 PostOpen 调度 | 导航示例真实实例化与关闭；通用异步加载/预加载已实现；具体远程适配未完成 |
| 命令 | AsyncCommand 四种并发策略、容量限制、来源上下文、CanExecute/Error、绑定级取消 | Save/Reset 与中断清理演示；完整并发/多投影运行验收待完成 |
| 生成器 | Roslyn 4.3 增量生成，属性、命令、BindingContext、Manifest、工厂、程序集注册入口；MUI001 诊断、TwoWay/OneWayToSource 多反向写入拒绝 | 生成器与实际 Unity 编译链通过；不代表所有诊断分支已验证 |
| uGUI | View 门控与边界索引；Text、Button、Slider、Toggle、InputField、Image、Dropdown Element | 原有控件对应 Unity 2022.3 程序集编译及实际导入通过；新增 Dropdown、InputField.EditingEnded、Image.FillAmount、ScrollRect.NormalizedPosition 仅编译验证，弹出列表及编辑结束交互待运行验收 |
| Element 命名 | 命名扫描、前缀建议、唯一名称预览、逐项编辑、最终冲突检查、过期预览拒绝、Undo 分组/异常回滚、嵌套 Prefab 名称保留 | Editor 编译通过；原生交互/Undo/Prefab 保存未验收；不自动迁移绑定或层级路径引用，可扩展规则与完整页面关联流程未完成 |
| 页面创建向导 | 全屏/弹窗 Prefab、Title/Close 绑定骨架、显式 Route 工厂、可选 Presenter/类型化 Args/Result、路径预览、防覆盖与失败清理 | Editor 及六组模板产出联合生成器编译通过；Unity 资产创建/回滚/打开未验收；现有 View 关联、程序集配置发现和 TMP 模板未完成 |
| 控件批量挂载 | Element Authoring 扫描预览、逐项选择、按原生类型挂载、重复跳过、应用前重扫、Undo 分组、嵌套边界排除 | Editor 编译通过；Undo/Prefab Mode/场景保存交互待验收；TMP 文本/输入框/下拉的可选适配器发现已接入，命名预览见对应项，页面向导见下项 |
| 编辑器 | 选中根批量结构校验、去重/取消/Console 定位；View Inspector，选择生成 Manifest 校验缺失/歧义/重复、正向和反向多写入冲突、ignoreParentGroups 与 Dropdown 模板结构；可选校验注册入口，独立 TMP.Editor 输入引用/层级检查，共享 Dropdown 有效 Toggle/RectTransform 模板检查 | Editor/TMP.Editor 编译通过；扩展自动注册、诊断交互和有/无 TMP 导入待 Unity 验收 |
| 设置页示例 | 程序化 UI、滑条双向绑定、异步 Save、同步 Reset、编辑器启动入口和自动演示 | Unity Play mode：50% → 80% → Saved 80% → Reset 50%；这是自动演示，未进行人工鼠标/键盘验收 |

## 待收尾与待验收

2026-09-29 模态输入静态复核：`BringToFront` 现在仅阻止越过同层模态页，高层模态下方的低层页面可在本层重排。透明关闭屏障在读取按键状态前，先核对输入模块、指针 ID 和按钮，避免复用事件对象后误把下一次输入当成原关闭手势；`StandaloneInputModule` 读取其实际 `BaseInput` 鼠标/触摸状态，包括项目 `inputOverride`，其他不支持的输入保留 Pointer 状态回退。可选 Input System 适配器在场景加载前登记。当前 `MUI.UGUI`、`MUI.UGUI.InputSystem` 和 `MUI.BasicExample` 使用 Unity 2022.3.62f3 与现有 Input System 1.14.2 程序集完成 Release 离线编译，均零警告、零错误；610 个 C# 文件格式检查及 `git diff --check` 通过。没有启动 Unity，真实指针行为与可选程序集导入仍未运行验收。

2026-09-29 模态置前约束：`Navigator.BringToFront` 在修改 Order 或历史前检查当前渲染序列，拒绝越过同层 Open/退出中的模态页，包括暂被更高层隐藏的模态页，避免同层下层页重新获得输入。基础示例及依赖的离线编译通过；模态叠加与关闭交错下的原生焦点仍待 Unity 运行验收。

2026-09-29 模态关闭手势：uGUI 模态屏障在视觉退出后对正在结束的关闭 Pointer 转为透明，保持在同一宿主区域的活动页面之上；旧输入模块按原生 Pointer 状态、可选 `MUI.UGUI.InputSystem` 适配程序集按 Click Action 的按下状态，在手势结束、模块切换或失焦后释放。基础 UGUI 不引用 Input System；ButtonElement 点击和 OverlayDismissArea 按下自动登记，项目自定义 Pointer 回调在请求关闭前调用 `View.CaptureModalPointer(eventData)`。基础示例与可选适配器分别离线编译、全仓格式门禁通过；真实鼠标、触摸、拖放、追踪设备和 IL2CPP 的逐模块运行行为仍待验收，不能将源码检查记为完整输入协议通过。

2026-09-29 返回输入收尾修复：异步 UIHost 的返回输入锁现在随目标视觉退出解除；守卫拒绝或无目标时随请求完成解除，完整关闭结果仍在清理结束后通过 BackInputCompleted 发布。Unity 2022.3 对应程序集离线编译、全仓格式门禁与 `git diff --check` 通过；没有启动编辑器，慢速 OnCloseAsync 下的连续物理返回输入仍待运行验收。模态 Pointer 关闭手势持续消费是独立缺口，本次未据此宣称完成。

2026-09-28 复核：下面按能力区分代码缺口与验收缺口，避免把历史记录里的“尚未实现”继续当作当前状态。此表不是完整设计逐条验收结论，也不用于计算精确完成百分比；未列出的设计要求不因此视为完成。

| 能力 | 当前代码证据 | 仍需验收或明确的限制 |
|---|---|---|
| 绑定与生成 | 属性/命令/绑定工厂、ViewModule 和 ViewRoute 已实现；支持泛型模型及非泛型 partial class 中的嵌套模型，页面向导使用生成路由 | 泛型外层及泛型 Presenter 自动推导不支持；复杂约束、跨程序集继承、转换器诊断和 AOT 待验收；开放工厂由项目显式闭合注册 |
| 实例与导航 | 同步/异步打开关闭、结果、历史、覆盖/焦点、Rebind/UpdateArgs、缓存与共享依赖已有代码；依赖资格检查已统一 | 同步/异步换绑、参数更新、缓存、超时隔离、共享依赖末拥有者释放、Replace 准备失败和慢速模态收尾已有运行证据；完整退出与物理指针/回调重入组合仍需验收；改变共享依赖参数返回 DependencyChangeRequired，直接改写被持有依赖返回 InUse，跨拥有者业务决策不属于框架缺口 |
| UI 缓存与资源边界 | 通用资源预算和全局回收已删除；具体加载后端、LoadedPrefabViewProvider、Lifetime.Load 接线移至 Samples；ResourceSlot 是 uGUI 内部实现 | 页面实例清理、制作回滚、同步列表释放、忽略取消后的异步迟到凭证归还及示例后端归还异常已有 Unity 运行证据；任意后端失败与平台实际卸载仍待验收，实际加载/卸载、共享计数和资源预算由项目负责 |
| 控件资源绑定 | 图像、纹理、材质及 uGUI 字体可通过项目加载器进行 Source 绑定；TMP 字体使用借用对象绑定 | 同步换键/清理、异步快速换键和在途关闭已有示例运行证据；Sprite 候选赋值故障及 Sprite/Texture/Font 确认清空后的归还已有 Unity 故障注入证据；内置 Image/RawImage/Text 的 Material 清空异常已加入确认路径，运行复验待完成；自定义图形、其他原生故障、失败重试及目标平台引用清理仍待验收；字体/图集管理不属于框架交付项 |
| Tab | 保留旧页、失败恢复、缓存、停用/恢复及独立同步分支已有代码 | KeepPrevious/RestorePrevious、缓存、标准 Dialog 联动及停用/恢复完整运行验收 |
| 列表与 Grid | 动态高度、多模板、选择、增量更新、分页来源接线及网格行高计算已实现；普通列表与 ThingItem 示例已有；具体分页请求/缓存移至项目示例；虚拟列表复用键索引，同步刷新减少临时集合，子视图共用回调上下文槽 | 普通回收列表的同步增删与异步条目准备、快切、在途关闭已有运行证据；虚拟列表稳定视口滚动无 GC.Alloc 样本、三列标签、定位和模板/高度切换已有专项证据；完整动态高度/换列锚点、多模板复用、跨行分配、增量性能与长期释放仍待验收 |
| 输入与标准表现 | UIHost.RequestBack、同帧消费、主题、本地化、安全区、焦点及标准组件已有代码；主题/字号偏好、语言与复数回退、字体切换、横竖屏原生布局、退订及公开安全区/键盘 override 已有运行与渲染证据 | 平台返回/物理输入映射、完整语言/字体/布局矩阵、Tick、转场及设备交互仍需验收；Android 键盘区域由项目平台适配提供，不由 UI 框架管理键盘 |
| 编辑器与生产流程 | 页面关联、程序集检查、可扩展命名规则、目录预览、防覆盖、批量挂载及清单校验已有代码；向导生成 Scripts/Prefabs 目录 | uGUI/TMP 资产创建、已有 View 关联、普通场景及 Prefab Mode 的挂载/Undo/Redo/保存已有运行证据；当前真实 Prefab 目录、域重载登记、无效契约阻止构建与有效目录 IL2CPP 构建已复验；中途制作故障回滚、源码定位及完整诊断交互仍待验收；目录自定义模板未提供 |
| 交付验收 | 当前工作树 Unity 导入、既有示例运行、真实 Prefab 制作/目录校验及 macOS IL2CPP 首场景画面与键盘结果已验证；同步 Prefab 缓存/非缓存循环计数收敛；主题/本地化和横竖屏布局已有专项渲染证据 | 完整设计第 19/24 章故障组合、物理鼠标/手柄、完整语言/尺寸矩阵、分阶段性能及长时设备内存仍待验收；已有窄路径证据不代表完整设计通过 |

后续围绕已有页面链路验证，不再将项目基础设施补入 UI 框架。表单草稿、字段校验、业务提交、跨拥有者参数事务、资源后端及全局资源管理均不属于待实现项。历史记录中的旧缺口以本节和当前源码为准。

## 已执行的验证

- 屏外聚焦 Navigation 示例退出码 0：Ready/selected=Item 9000/cells=9；连续请求 old=Superseded/latest=Ready；数据更新 Superseded；Cancelled/keptOffset=True；父门控 InputBlocked；缺失键 NotFound；原有导航 shutdown 完成。

- 列表观察者重入 Unity 示例退出码 0：waitedCurrent=True/pendingAtLoading=True；Ready 观察者换源后 readyReplacement=True/final=Empty/cells=0；导航 shutdown 完成。补充在途错误检查与几何失败归档；错误竞态尚未完整运行验收。

- 列表状态 Navigation 示例最终退出码 0：Error/errorVisible=True；尺寸恢复后一帧 sameOperation=True/status=Error；Retry 后 Ready/errorCleared=True/cells=3；清空 Empty/placeholder=True；导航 shutdown 完成。首轮定时取消触发后台线程修改状态节点，已改为 UI 上下文投递并验证不再出现该异常。

- 固定网格 Unity 示例退出码 0：三列 small=24/large=24，10001 项内容高度 133360；单元格实际宽 66.66667、列锚点不同；切回单列 cells/native=8/8，视口缩至 80 后 cells/native=3/3，随后导航 shutdown 完成。

- VirtualListElement Navigation 示例退出码 0：small=8/large=8/items=10000；原生条目数 8；跳转 first=500/cells=9 且复用原生条目 8；头部插入 kept=True/first=501；清空 cells=0/first=-1，下一帧原生条目 0；完整导航 shutdown。这是固定行高视口物化证据，不是所有列表特性的验收。

- ObservableList Unity 示例退出码 0：Add/Move/Replace/Update/Remove 一次提交为 version=1、单次通知、silver/iron/wood；重复键和回调失败后 version/count 保持 1/3；失效编辑器与通知重入拒绝；Reset 10000 条后 version=2、总通知数 2；后续导航 shutdown 完成。没有据此声称万条列表渲染性能已验收。

- 通用 View 输入状态通知与焦点协调改动后，现有 Tabs Play-mode 示例复核退出码 0，完成 cleanup；该复核覆盖现有异步切换/父子门控演示，不代表所有新焦点场景已在 Tab 中验收。

- 焦点 Navigation 示例退出码 0：初始 Confirm/modalFocused=True；越界维护后 inside=True；禁用 Confirm 后回退 Close；输入阻塞 none=True/selectionCleared=True，释放后恢复 Close；关闭后下层原选择恢复；局部隐藏撤销焦点，显示重新恢复；导航 shutdown 完成。

- InputGate Navigation 示例退出码 0：count=2/enabled=False 时手动触发 Confirm 后页面仍 Open，模态 Back=Blocked；同一令牌重复释放后 count=1/enabled=False；最后释放后 count=0/enabled=True；激活结束时 count=0/gateOpen=False；原有导航流程完成 shutdown。

- BackBehavior 导航示例退出码 0：非历史模态 Closed/Destroyed 且下层 Open；Block 返回 Blocked；跳过 Ignore 后关闭较高 Layer，较新但低层页面保持 Open；Presenter 首次 Handled/仍 Open，第二次 Closed，嵌套 Back 均 Reentrant；随后原有导航场景正常 shutdown。

- 覆盖/模态 Navigation 示例最终退出码 0：None 保留下层焦点，BlockInput 保持 alpha=1/input=False，Hide 为 alpha=0/input=False，关闭均恢复 alpha=1/input=True/focus=True；OnCovered/OnRevealed 已触发。下一帧 Canvas 更新后窗口外 RaycastAll 首项为 MUI Modal Barrier，随后完成导航 shutdown。新建屏障同帧立即查询未命中，不能将这次运行当作首帧 readiness 或跨手势输入协议验收。

- Navigator 托管预加载已接入有界目录、资源身份去重、ClearPreloadsAsync 与 Shutdown；Unity 示例验证 WaitCancelled/CapacityExceeded/Ready、清理前迟到 Superseded、退役占额度，以及退出 loads=5/releases=5。

- Tab 离开守卫已实现可选异步确认、拒绝保留、重复等待隔离、串行最新意图与父关闭取消。Unity 示例验证 Rejected/Denied、保持版本、WaitCancelled 后原请求 Ready、确认期间改选旧 Superseded/最新 Ready、父关闭 ParentInactive；全局 Dialog/不合作守卫超时隔离未实现。

- TabBar 动态模板 Unity 示例退出码 0：新增 mail/Label=Mail/active=True；移除后 destroyed=True、焦点 inventory；父关闭 ParentInactive/Inactive 并完成清理。真实键盘/手柄输入与按钮池仍待验收/实现。

- Tab 动态目录 Unity 示例退出码 0：相同定义 keptVersion=True；重复键 Failed/count=3 且显示 quests；移除当前项后 Ready/inventory；空目录 Empty；恢复 quests 按钮可见。UpdateDefinitionsAsync 与 ReconcileAvailabilityAsync 已实现；显式模板的动态按钮生成已实现。

- Tabs Unity Play mode 示例退出码 0：WaitCancelled；Rejected/Disabled；Loading indicator=True/barEnabled=True；Failed/Error → Retry/Ready；Superseded → Ready；ParentInactive/Inactive；cleanup complete。

- 动态子视图 Unity Play mode 示例退出码 0：Iron × 3；慢请求 Superseded、最新请求 Ready；null 模型时 instance=True/unbound=True；重绑 Iron × 7；清空 Source 后 instance=False。

- NestedViewElement 的 Unity Play mode 示例退出码 0：Stone × 5 → 替换 VM 为 Stone × 8 → null 清空 → 再绑定 Stone × 12；同次原有导航与显式投影演示正常结束。
- 新增 ChildViews 后 Navigation/ThingItem 的 Unity Play mode 示例退出码 0：Prepared alpha=0 → Active alpha=1；父隐藏/恢复后子局部隐藏仍 alpha=0；借用释放后 nodeAlive=True；复用节点异步显示 Wood × 20；父关闭后子状态 Closed。
- 上述示例未覆盖投影准备超时、失败清理、跨线程取消、动态池回收或完整多层子树故障路径，不把成功链路当作这些能力的验收。

- 新增 ResourceSlot 后再次完成 Unity 编译与现有 Navigation Play mode 演示，进程退出码 0；这不代表资源槽自身的运行路径已验收。
- Runtime 程序集依赖无环，Core/Resources/Navigation 禁用引擎引用；源码和程序集定义的 Unity metadata 齐全。
- Navigation 在 Unity 2022.3 Play mode 跑通：同步成功、AsyncOnly 同步拒绝、异步 OnOpen、重入拒绝、Completed(42)、重复关闭 AlreadyClosed、Back 后 history=0、宿主清理。
- Navigation 准备阶段取消返回 CancelledBeforeCommit，history=0；关闭等待取消返回 WaitCancelled/Closing，随后实际结果 Dismissed、cleanup=Complete。

- Settings 的 RunPreviewBatch 在 Unity 2022.3 实际进入 Play mode，通过原生 Slider 和 Button 事件执行自动演示；日志确认 VM 与显示状态变化、异步 Save 完成、Reset 恢复。
- 演示再次启动 Save 并销毁示例：销毁前 executing=True，最终日志为 binding state=Unbound、saveExecuting=False，过程无脚本异常。

- `dotnet build Tools~/Build/MUI.Resources.csproj -c Release`：基础层编译通过。
- 使用本机 Unity 2022.3.62f3 的模块和匹配的 UnityEngine.UI.dll 编译 `MUI.UGUI`、`MUI.Editor`、`MUI.Samples.Settings`。
- `bash Tools~/publish-generator.sh`：构建生成器并更新带 RoslynAnalyzer 标签的发布 DLL。
- 临时 Unity 项目 `/tmp/mui-unity-validation` 直接引用本地包与本机缓存 uGUI 包，导入设置页示例；Unity batchmode 返回成功，输出 Core/Resources/UGUI/Editor、Settings 及其 Editor 程序集。
- 检查 Unity 生成的 `MUI.Samples.Settings.dll`，确认存在生成的 `Volume`、`Status` 和 Manifest 属性。

初次受限环境下 Unity 未连接授权服务，未进入脚本编译；允许访问现有授权服务后成功。授权日志中的远程令牌提示未阻断后续实际编译。

新增 Settings 示例自动演示及批量 Play mode 启动入口；未新增测试工程或单元测试，没有自动提交。编译证据不等同于运行时正确性、零泄漏或完整设计验收。


## 顶层实例缓存增量（2026-09-18）

已接入 RoutePolicy.CacheMode、IReusableViewPresenter、有界缓存目录、Open/OpenAsync/ReplaceAsync 命中、实例内容与新导航激活分离、ClearCacheAsync 和 ShutdownAsync 最终释放。外部借用 VM、异常或非正常关闭不复用；缓存清理期间拒绝入缓存，释放回调受重入保护，历史清理错误记录有界且关闭不重复聚合相同失败。新增可选 Navigation 缓存复开示例，展示模型/视图复用、新参数、新句柄、旧句柄隔离及最终销毁。

验证为 Navigation 示例及依赖的离线编译、源码调用链检查；未新增测试，未启动 Unity。TTL、LRU、字节预算、版本失效、子实例保留和完整运行验收仍未完成；本节补充历史“尚未接入顶层缓存”的记录，不代表完整设计目标已完成。


## 顶层缓存过期与淘汰增量（2026-09-18）

缓存策略从临时布尔开关调整为 ViewCacheMode.None/KeepAlive/Timed，Timed 强制正 CacheDuration。命中预检与取出都检查单调时钟 TTL；Tick 每秒维护，无帧宿主可调用 RefreshCache。按最近使用后归还顺序淘汰最旧条目，一次仅有一批自动释放，阻塞清理不会形成无界淘汰队列。新增 RetiringCachedViewCount；ClearCacheAsync/ShutdownAsync 等待在途淘汰和目录收尾，失败记录有界。UIHost.Initialize 暴露 cacheCapacity。可选缓存示例使用两项容量，并增加 TTL 重新创建及容量淘汰演示。

Navigation 示例及依赖离线编译通过；未新增测试、未启动 Unity，计时/淘汰与释放竞态仍需运行验收。资源与绑定契约版本失效、字节预算、父子整组实例缓存等仍未完成；本节更新此前 TTL/LRU 尚未接入的历史描述。


## 顶层缓存估算预算增量（2026-09-18）

新增 Route.EstimatedRetainedBytes、Navigator/UIHost.Initialize 的 maxCachedEstimatedBytes，以及 ReservedCacheEstimatedBytes/FailedCacheEstimatedBytes 诊断。启用字节上限后拒绝大小未知和超大实例；数量、字节同时满足才准入。淘汰以最旧内容为先，目录移出不提前归还额度；释放失败保持占用，避免反复透支。命中转为活动内容时移出缓存预算，重入缓存重新计费。预算实现独立为 Navigator.Cache.Budget，新增元数据文件。

Navigation 可选示例增加 2 MiB 预算、1 MiB/1.5 MiB 条目、未知与过大估算拒绝流程。估算数字用于演示，不是实测。示例及依赖离线编译通过；未运行 Unity、未新增测试。全局资源预算、动态大小估计、资源/绑定契约版本失效仍未完成。


## 顶层缓存代际失效增量（2026-09-18）

新增 InvalidateCacheAsync：先同步切换宿主缓存代际，再清目录并等待在途淘汰。ViewContent 工厂运行前捕获代际，旧活动/准备/关闭中内容后续不能重新入缓存；命中与准入都复核，淘汰回调后再次检查。BindingRegistry.Reset 自动更换绑定代际，旧缓存无法命中，并由维护移出；不自动重注册或停止已有绑定。新增 Navigation 失效演示，展示活动页保持活动、旧页关闭不缓存、新代际重新创建并可缓存。

Navigation 示例及依赖离线编译通过，未运行 Unity、未新增测试。Route 与 ViewResource 已有不可变身份，跨定义不共享缓存；资源提供方内容变化的自动通知、路由热更新、账号切换的队列/活动业务协调、全局资源预算仍未完成。本入口不是完整账号切换协议。


## 资源提供方代际衔接增量（2026-09-18）

新增 IVersionedViewProvider.ContentVersion。导航缓存记录提供方代际，在命中、准入、维护以及页面准备/最终提交前复核；Replace 等待确认后的候选也重新校验。预加载按代际撤销旧批次，迟到凭证清理后不发布旧 Ready，容量仍计入在途释放。LoadedPrefabViewProvider 支持显式 Invalidate，旧驻留项和迟到加载不再发布，既有资源凭证保留到所有权结束；实例化回调后再次检查并清理失败实例。ContentViewProvider 透传底层代际。示例补充预加载换代和旧版本迟到加载日志。

Navigation 示例及依赖离线编译通过，未运行 Unity、未新增测试。自动衔接限于提供方主动更新代际，未实现具体热更新后端事件适配、按资源精细失效、Tab/子实例缓存端到端版本传播或全局预算。


## 子视图与 Tab 资源版本传播增量（2026-09-18）

ChildViewHandle 在创建工厂前捕获资源提供方和绑定注册表代际，公开 IsContentCurrent；准备、停用恢复、子提交及父绑定提交均复核，失效仍沿用原关闭/回收协议。最终释放清除提供方引用。Tab 缓存准入、排空后、淘汰等待后和命中都检查代际；启用缓存即建立单条维护循环（需要 UI 同步上下文），不再仅在配置 TTL 时启动，也不再只检查最老缓存项。新增 Tabs 示例资源失效菜单与创建数量日志。

Tabs 示例及依赖离线编译通过，未启动 Unity、未新增测试。当前稳定活动内容不会自动撤下；账号/业务上下文切换、按单资源精细失效、全局预算及故障/视觉运行验收仍未完成。


## 共享资源加载预算增量（2026-09-18）

新增 ResourceBudget、Snapshot、ResourceBudgetExceededException 及 BudgetedResourceLoader。多个加载器共享数量/估算字节上限；加载前申请，加载中/驻留/释放中/失败持续计费。释放失败保留额度，未知估算在启用字节预算时拒绝。包装器保留底层原生同步能力，同步失败需要异步清理时通过 ResourceLoadException.CleanupCompletion 暴露责任，嵌套包装也等待内部清理。无静态单例、无无界申请队列，预算锁内无外部回调。

Navigation 示例接入两个加载包装器共享额度演示，并将同一预算用于 LoadedPrefabViewProvider 预加载。示例及依赖离线编译通过；未新增测试、未运行 Unity。低内存优先级回收、物理资源去重、动态预算与实例池/远程缓存统一计费仍未完成，不能宣称完整全局内存预算已完成。


## 导航低内存回收增量（2026-09-18）

新增 Navigator.TrimMemoryAsync/IsTrimmingMemory：先取消预加载，随后启动非活动顶层缓存清理，等待包括此前移出批次在内的相关释放；重复请求共用任务。回收期间 PreloadAsync 返回新增 MemoryPressure 状态、正常关闭不入缓存。ShutdownAsync 等待正在进行的回收。预加载清理先登记再执行取消回调，并跟踪在途任务。

UIHost.Memory 接入 Application.lowMemory，以单个标记合并通知，在主线程 Update 开始回收；最多一轮在途与一轮待处理，退出开始即退订。增加显式等待入口、示例菜单和迟到预加载回收流程。Navigation 示例及依赖离线编译通过，未运行 Unity、未新增测试。活动 Tab 缓存/独立池/后端缓存的统一回收、优先级细分、设备内存下降与异常竞态验收仍未完成。


## 回收参与者协调与 Tab 接入增量（2026-09-18）

新增 Core/Memory 有界协调器与参与者契约。按 Preloads/InactiveCaches/Pools/DerivedResources 和同类登记顺序启动所有请求，重复回收共用任务、异常不跳过其他参与者，回调拒绝等待自身。登记借用资源，撤销不终止已开始回收；协调器销毁等待在途任务但不销毁参与者。

Navigation 将自身预加载和顶层停用缓存统一登记为参与者，默认拥有协调器，也可借用项目共享协调器。UIHost 暴露同一服务，退出撤销本宿主登记。Tab 可显式 RegisterMemoryTrimming，失活自动退订，回收期间不接纳新停用缓存。Tabs 示例增加独立协调器与人工菜单。

Tabs 示例及依赖离线编译通过，未运行 Unity、未新增测试。具体项目池/后端缓存需实现参与者并登记，框架不会发现任意外部资源；实际低内存、跨宿主、失败与取消竞态验收尚未完成。
