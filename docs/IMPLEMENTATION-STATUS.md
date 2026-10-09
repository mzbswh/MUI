# 设计目标实现记录

验收依据为 [DESIGN-GOALS.md](DESIGN-GOALS.md)。本文件记录实现差距与验证范围，不替代设计目标，也不把编译成功视为运行验收。

## 最终交付验收清单（2026-10-09）

第 1–8 节设计目标及三个收尾批次已完成。以下对应设计第 8 节的九类验收场景，后附 20 个确定性条件的逐项证据。后文按日期保留历史记录，历史的“待实现”与“尚无证据”只描述当时状态。进度按下表已验收条件与剩余条件报告，不由断言数或源码文件数推算百分比。

| 设计验收场景 | 当前证据 | 验收结论 |
| --- | --- | --- |
| UPM 包与示例 | Unity Hub 官方 3D Built-In 模板新建 2022.3.62f3 工程，本地包安装及内置 Shader 检查通过；用户完成 Add package from disk，随后核对窗口、manifest 与 lock 的本地引用；八项 Sample 已经 Package Manager 窗口逐项导入，最新 254/254 文件与仓库一致，按依赖先导入 Resource Integration；Basic、Settings、Tabs、Navigation、RecyclingList、CommonPatterns 和 Dialogs/DragDrop 接入场景画面正常；Analyzer 与当前 Release 构建同步，八项 Sample 的 24 份生成源码前后一致，最终 11 个示例/可选程序集 Unity 编译加载有既有记录 | 已完成；最终示例操作与清理见最后批次记录，254/254 文件一致 |
| 基础页面 | 无 Presenter 的生成绑定、Done(42)、返回与重开有 Player 键盘记录；最终新工程实际 Return 提交得到 42，Return 重开后 Escape 返回，再次 Return 重开，画面正常；Basic README 已逐项列出手写模型、Prefab、Route/注册、宿主配置和生成产物，页面不要求 Presenter | 已完成；用户实际 Done(42)/Reopen，原生 Return/Escape 有日志 |
| 数据与控件 | 最终新工程的合并 Play/IL2CPP 批次验证公共 Legacy/TMP 控件及转换、资源槽和自定义 Element；最终嵌套路径与线程批次每后端 46 项通过；最新合并批次证明 InputGate 关门仍允许有效命令提交，换绑/关闭后的旧结果被拒绝，路径退订故障保留回调及旧拥有者且解绑失败 | 已完成；Settings 原生滑块/按钮及文本提交回调通过，换绑与线程证据已对应 |
| 导航与模态 | 双后端记录覆盖关闭意图、许可失效、同 Navigator 确认框、队列满时关闭及 Shutdown、历史显示顺序；最终剩余条件批次通过单实例额度并发及迟到取消、同 Route Replace、Done/Back 两种结果竞争、提交后转场故障、隐藏/恢复绑定与 Tick，及真实 timeScale=0 的 UIHost 自动转场驱动；最终新工程官方输入模块通过共享/独立 EventSystem、焦点、同帧返回、模态进入/退出与关闭 Pointer、叠加屏障及排序冲突的 29 项检查 | 已完成；批量关闭、模态失败及退出期间四类输入补验通过 |
| 子视图与列表 | 静态嵌套、普通/虚拟列表换绑和固定槽位已有受控矩阵；原生组合面板的停用/恢复、活动与停用兄弟的父关闭、迟到准备及叶责任恢复在同一双后端批次通过 4/100；最终测量示例通过项目侧语言/主题服务同步刷新文本、字号、尺寸和输入资格，Editor 实际运行各阶段 Ready | 已完成；Tabs 选择/重试、奖励增删及字体恢复、最终复杂容器清理通过 |
| 核心虚拟列表 | 焦点 11/22、合并容器 47/637、布局 14/102 均有 Play/IL2CPP 记录；环境批次验证实际坐标；当前 Runtime 的固定/测量双模板 100/1,000/10,000 项 IL2CPP 基线已刷新，共 3,600 帧、展示实例最多 11、单帧测量最多 4 | 已完成；官方输入模块横纵持续按住拖动的尺寸补偿通过；目标设备性能仅交付基线 |
| 资源与退出 | 缓存 100 次复用、预加载 25/101、宿主 16/67、加载器 17/70 有双后端记录；环境 9/82 取得真实资源与场景证据；原生复杂组合退出及迟到/可重试故障恢复在 4/100 批次通过；高频 View Provider 已记录独立 .NET 与最终 Unity Editor Mono 获取/归还分配 | 已完成；最终奖励资源 15/15/0、DragDrop Shutdown、临时批次账本归零；外部后端遵守公开契约 |
| 诊断与 Editor | 三类向导生成与公开 API 运行、保存合并校验、实际编译/域重载补跑、构建错误阻断及 Manifest 源码/资产路径已通过同一 Editor 批次 41 项断言；最终资产移动/删除、Play 中保存延后与退出后单批补跑通过 20 项检查；错误上下文、清理账本及 Inspector 有已有记录 | 已完成；实际 UIHost 快照、声明/控件/源码定位及 Contract 校验通过 |
| 可选适配 | 最终新工程 TMP 公共控件有合并 Play/IL2CPP 记录；官方 Input System 1.19.0 的实际 Dialogs 原生 Esc 已关闭页面，当前 Runtime 的官方输入模块观察再次通过返回、退出屏障、关闭 Pointer 持续消费及释放后恢复 | 已完成；TMP 双后端与官方 Input System 返回/模态手势通过；Basic 另有用户实体鼠标结果 |

性能要求按设计保留：目标设备与预算未指定时记录可复现基线，不声明目标设备性能达标。设计必需条件已关闭，停止扩展验收矩阵；后续仅对具体回归或新增需求开展工作。

收尾执行与报告规则：

1. 固定以上九类场景及设计第 8 节的确定性条件，不在进度询问时重新估算或扩大分母。此前百分比仅为主观估算，后续以具体已验收项、证据和剩余项报告进度，不再给出漂移的百分比。
2. 先归因同一批的全部已知失败，再集中修改相关实现；观察驱动前提错误先校正，不能把它当作包缺陷。每批静态编译及规范检查集中执行，互不依赖的检查并行。
3. 相关运行场景合并为同一 Editor 运行与同一 IL2CPP Player；Editor 通过后只构建一次最终源码。新失败只补充能判定该失败的诊断，不重跑无关已通过模块。
4. 已完成项保留对应源码版本与证据。后续发现回归单独记录受影响项并修复，不把原先的局部通过改写成从未完成，也不把旧证据冒充最新源码验收。完整目标仍须满足原设计全部条件。

收尾按以下三个固定批次执行，现均已关闭。批次不改变设计要求；一个场景可关闭多个条件，不为每个条件另建工程、驱动或 Player。

| 批次 | 固定范围 | 停止条件 |
| --- | --- | --- |
| S1 示例与编辑交付 | 最终八项 Sample 的 Package Manager 导入及公开入口；Basic、Legacy/TMP、Input System、模态和列表的真实输入与画面；Inspector 定位；资产移动/删除及 Play 后补跑 | 最终版本导入、编译、操作与画面有对应记录；发现的实际缺陷集中修复后，只补验受影响入口。Editor 专属变化不重建无关 Player |
| S2 剩余契约核对 | 对照第 8 节逐条复用既有证据；补齐通知清理、换绑交错、命令结果、导航确定性条件和复杂容器退出中确实缺失的条件 | 每条要求有匹配的通过证据；缺失条件集中补验，实际失败修复后只回归受影响条件。全部通过即关闭，不继续扩展任意故障组合 |
| S3 最终交付核对 | 第 1–8 节、注释/命名/结构、UPM 与生成器一致性；固定/动态/多模板的可复现性能基线；最终源码必要的 IL2CPP 运行及交付记录 | 没有未关闭的设计必需条件和已知阻断缺陷；构建与运行证据匹配交付源码。未指定目标设备/预算仍按设计记录基线，不等待假定的外部环境 |

每批开始列出具体缺口，结束只记录关闭项、实际失败和下一批剩余项。新增检查必须能对应原设计条件或已复现缺陷，不能仅因“可能还有问题”延长收尾。相同代码及配置的通过结果不重复验证；最终集中校验只覆盖交付所需门槛。仓库不新增未被要求的测试，不自动提交；全部必需条件关闭后才宣布完整目标完成。

## 最终交付边界

- S1 示例与编辑交付、S2 确定性契约、S3 源码及交付一致性均已完成；没有未关闭的设计必需条件。历史待验收表述保留当时范围，以本节及最终审计为准。
- 最终工程为桌面 `MUI-Clean-20261009`，Unity Hub 官方 3D Built-In / 2022.3.62f3 Silicon；MUI 0.1.0 以本地 Package 安装，八项 Sample 按窗口逐项导入。TMP 3.0.7、Input System 1.19.0；Assets 没有包 Runtime 副本。
- Basic 的实体鼠标与键盘已验收。其他原生输入观察使用官方输入模块的模拟设备；Settings 文本编辑明确使用原生结束编辑回调。持续拖动补偿验证真实输入模块保持同一捕获，覆盖横/纵尺寸修正及下一帧位移，不声称操作者使用实体鼠标。设计只在 Basic 条件中明确要求实体鼠标/键盘，其他条件按声明的原生执行路径验收。
- 当前 Runtime 的必要生成绑定、导航、列表复用/定位及资源归还已有匹配源码的 IL2CPP 运行证据；Editor 专属操作在 Editor 验收。最终补验没有 Runtime 修改，不重复构建未变化的 Player。
- 目标设备和预算未指定，性能按设计交付可复现基线；不声明目标设备达标。历史 Player 退出有 13 条 allocator 提示及 Unity Immediate MemoryLeaks 输出，原因未确认，不宣称完整内存证明。这些边界不改变已逐项通过的受控凭证及责任账本结果。
- 未新增仓库测试，未自动提交或推送。临时观察脚本、场景及 meta 已退役至 `/private/tmp`，工程恢复 Basic 公开示例场景。

## 最终确定性条件审计（20/20）

下表逐行对应设计第 8 节的 20 条确定性条件。日志均保留实际后端与注入故障范围；预期错误报告不作正常路径失败处理，也不把模拟设备事件写为实体操作。源码对应复核见 `/private/tmp/mui-final-handoff-audit-20261009.json`。

| 序号 / 原条件 | 结果与证据 |
| --- | --- |
| 1 导航 | 通过。慢准备、同 Navigator 确认、满队列 Busy/关闭/Shutdown、Done/Back 竞争、提交后转场失败：`mui-contract-closing-final-il2cpp-player-20261009.log`、`mui-navigation-remainder-final-il2cpp-player-2-20261009.log`。 |
| 2 命令与输入 | 通过。门控后已接纳结果有效，换绑/关闭旧结果失效；共享 EventSystem 唯一仲裁：上述 closing 日志及 `mui-shared-input-final-play-20261009.log`（29 项、reports=1 为排序冲突；19 条 Unity 多 EventSystem 警告另记）。 |
| 3 清理 | 通过。百次复用、缓存单列/清空、失败责任、显式安全重试、取消 Shutdown 观察：`mui-cache-budget-final-il2cpp-player-20261009.log`、`mui-environment-final-il2cpp-player-20261009.log`、`mui-final-gaps-core-20261009.log`。 |
| 4 列表 | 通过。100/1,000/10,000 项横纵/Grid、容量/并发、多模板、远定位/对齐、增量锚点、历史阅读：`mui-list-layout-batch-final-il2cpp-player-20261009.log`、`mui-list-closing-batch-final-il2cpp-player-20261009.log`。 |
| 5 列表性能 | 通过设计规定的基线交付。固定/动态双模板六组共 3,600 帧，CPU P95/P99、GC 和测量次数区分记录：`docs/VIRTUAL-LIST-BASELINE.md`、`mui-composite-baseline-final-il2cpp-player-20261009.log`。目标预算未指定。 |
| 6 接入成本 | 通过。Basic README 列出 ViewModel、Prefab、Route、宿主与生成产物，不要求 Presenter；最终用户 Done(42)/重开、原生键盘日志 `mui-basic-final-keyboard-20261009.log`。 |
| 7 绑定 | 通过。双后端初始化/编辑/转换、嵌套/null、换绑准备/故障提交、自定义 Element 与线程入口：`mui-final-contract-batch-il2cpp-player-20261009.log`、`mui-final-path-thread-il2cpp-player-20261009.log`、`mui-args-final-operation-il2cpp-player-20261009.log`。 |
| 8 缓存与退出 | 通过。新身份/结果、订阅基线、在途不复用、清空归还、Shutdown 与共享服务：cache-budget、preload-consumer、environment 的最终 IL2CPP 日志。 |
| 9 焦点与手势 | 通过。原控件/逻辑控件、失效回退、不抢新焦点、模态限制；拖拽 gate/cover/close 各一次取消且无业务 Drop：`mui-list-closing-batch-final-il2cpp-player-20261009.log`、`mui-shared-input-final-play-20261009.log`、`mui-final-gaps-native-play-20261009.log`。 |
| 10 列表异常 | 通过。占位/显式重试、尺寸/布局诊断、修正上限、图片后续尺寸不重启定位、焦点失败取消：list-closing、list-layout 最终 IL2CPP 日志及 `mui-image-resize-play-20261009.log`（26 项）。 |
| 11 结果与扩展 | 通过。拒绝/Busy 无异常、多等待者只报一次、抛错观察者隔离：`mui-final-gaps-core-20261009.log`。语言/主题资源、通知/加载、动态尺寸/门控：`mui-measured-services-play-20261009.log`、`mui-final-samples-first-play-20261009.log`。 |
| 12 并发与守卫 | 通过。实例额度/迟到取消、同 Route Replace、关闭意图/许可代际、直接自等待：navigation-remainder、contract-closing、environment 最终 IL2CPP 日志（ProviderDirectSelfAwait）。 |
| 13 历史与生命周期 | 通过。覆盖保持句柄/绑定且暂停 Tick、恢复不重开、B 置顶后 C 前台；子停用/恢复/父关闭：`mui-navigation-remainder-final-il2cpp-player-2-20261009.log`、`mui-composite-baseline-final-il2cpp-player-20261009.log`。 |
| 14 模态退出 | 通过。退出提交后新点击/滚轮/提交/Back 阻断，结束恢复；零时长、ForceClose、叠加与迟释放 Pointer：`mui-final-gaps-exit-inputs-play-20261009.log`、`mui-final-inputsystem-play-20261009.log`、`mui-shared-input-final-play-20261009.log`。原生退出采样失败移除画面/屏障并恢复下层命中：`mui-final-gaps-native-play-20261009.log`。 |
| 15 加载器寿命 | 通过。父停止后凭证安全归还、不能延寿不缓存、当前注入、迟到/淘汰/Shutdown 不用失效后端：`mui-loader-lifetime-final-il2cpp-player-20261009.log`（17/70）。 |
| 16 完成点与环境 | 通过。关闭提交/视觉/清理分离、批量拒绝逐项继续：navigation-remainder 及 `mui-final-gaps-core-20261009.log`；timeScale=0、场景/持久宿主/外部销毁：list-layout、environment 最终 IL2CPP 日志。 |
| 17 Provider | 通过。本地/延迟获取、取消/异常/迟到归还、多观察者共享完成、分配测量：`mui-final-contract-batch-il2cpp-player-20261009.log`、`mui-environment-final-il2cpp-player-20261009.log`、`mui-final-gaps-core-20261009.log`、`mui-provider-allocation-unity-20261009.json`。保留 Task。 |
| 18 控件与编辑接入 | 通过。三向导/公开运行、Legacy/TMP 公共控件、Element 初始化失败、容器不重复持有、固定槽位、增量及当前构建规则：`mui-editor-closing-final-play-2-20261009.log`（41 项）、`mui-editor-incremental-final-20261009.log`（20 项）、final-contract/list-closing 最终 IL2CPP 日志；用户确认 Inspector 三项操作正常。 |
| 19 资源槽 | 通过。失败保留 A、迟到归还、B 接管后 A 失败独立记录、赋值故障不引用已释放对象：`mui-final-contract-batch-il2cpp-player-20261009.log`、`mui-loader-lifetime-final-il2cpp-player-20261009.log`。奖励示例字体失败保留显示、同键重试及 15/15/0：`mui-final-samples-remaining-play-20261009.log`。 |
| 20 契约边界 | 通过。参数/换绑准备隔离与故障提交、合并观察者取消及批量逐项取消、共享预加载消费者/旧版、跨来源位置与回退、Overlay/Camera 一致坐标：args-final-operation、contract-closing、preload-consumer、list-layout、environment 最终 IL2CPP 日志及 `mui-final-gaps-core-20261009.log`。 |

表中简写的最终日志全名均可在各历史批次记录查询，原文件位于 `/private/tmp`。本轮重新计算三份完整 452 文件快照、223 文件导航快照及 10 文件 closing 快照，均无差异。补验前后全部 452 份 Runtime 也未变。

## 当前基础

### 2026-10-09 奖励列表示例退出与最终路径线程验收

- 在最终新工程实际停止 RecyclingList Play 时复现 View 原生销毁先于示例拥有者清理：Console 报 `View is retained until its activation cleanup is confirmed`，随后资源计数为创建 6/归还 6/仍持有 0。原始记录 `/private/tmp/mui-recycling-stop-reproduction-20261009.log`。示例增加 OnApplicationQuit 提前启动拥有者清理，退出、手动关闭和销毁共享同一 Task，资源计数只输出一次；不改变 View 对未确认责任的诊断。同步导入后实际运行并停止 Play，计数仍为 6/6/0，Console 0 警告、0 错误；记录 `/private/tmp/mui-recycling-stop-fixed-play-20261009.log`。该修复仅涉及示例；不由本地立即完成路径承诺任意退出期间的外部任务都能清理完成。
- 复用已有嵌套路径观察入口，对最终源码执行一次 Play 和一次 IL2CPP Player，每个后端共 46 项通过：原 Legacy/TMP 各 19 项路径条件，加各 4 项线程条件。覆盖叶及中间模型替换、null 回退与反向写入禁止、旧订阅解除、转换/读取重入、只读预览和候选失效；后台生成属性及集合写入在修改前拒绝、自定义违规通知不更新原生 UI、Dispatcher 在所属主线程更新成功。自定义违规通知只在临时观察模型中注入；包实现和仓库测试没有新增。
- Play 与 Player 报告均为 completed=true/failed=false，Player 退出码 0；IL2CPP Development/High stripping 构建 Succeeded/errors=0/warnings=0，耗时 81.02 秒，Unity 2022.3.62f3 / Apple M4 Pro / Metal。首次受限启动在 macOS 应用注册阶段 SIGABRT，尚未进入观察代码；在授权环境重启同一产物后通过，没有重新构建。最终退出仍有 13 条 allocator 提示及 Unity Immediate MemoryLeaks 输出，未归因，不宣称完整内存验收。
- 原始证据 `/private/tmp/mui-final-path-thread-clean-editor-20261009.log`、`mui-final-path-thread-play-20261009.json`、`mui-final-path-thread-il2cpp-build-20261009.json`、`mui-final-path-thread-il2cpp-player-20261009.log` 和同名 Player JSON；产物 `/private/tmp/mui-final-path-thread-il2cpp-20261009.app`。452 个 Runtime 源码校验值在本批次前后未变，产物旁 Core/UGUI/TMP Portable PDB 的 190 个 Runtime 文档全部匹配当前源码，报告 `mui-final-path-thread-source-20261009.json`、`mui-final-path-thread-pdb-audit-20261009.json`。这关闭路径及线程条件的最终运行对应，不代替实体输入/IME或其他换绑条件。
- 唯一改动示例源码的成员布局、大括号及空白检查通过；八项导入 Sample 的 252 个文件与当前仓库全部一致，记录 `/private/tmp/mui-recycling-stop-style-20261009.log`、`mui-final-sample-content-after-stop-fix-20261009.json`。临时入口、asmdef、场景与 meta 已移出工程 Assets，保留在 `/private/tmp/mui-final-path-thread-20261009/imported`；恢复原 RecyclingList 场景。状态文档同时纠正上一合并批次的模型描述及“每后端 16 项”计数，未自动提交或推送。
- 随后按 README 在新工程创建带默认相机的 CommonPatterns 与 DragDrop 接入场景，未复制或改写包 Runtime。临时 Editor 接线脚本 `/private/tmp/MuiRemainingSampleSetup.cs` 同步在工程 `Assets/Editor/`，场景在 `Assets/MUI Acceptance Samples/`；工程菜单 Tools/MUI Acceptance 可切换这两项，当前 CommonPatterns 已实际进入 Play，初始文字、图标和四个按钮可见，Console 0 警告、0 错误。GameView 聚焦后的自动点击仍未触发 Toast，不能记录为真实交互通过；已请求用户确认通知、语言、主题和加载门控操作。此前奖励列表的人工增删/字体结果仍未收到，Dialogs 接入及 DragDrop 运行继续保留在 S1。

### 2026-10-09 最终控件、资源槽与原生清理合并验收

- 对旧 PDB 已定位的源码变更范围复用四个已有观察入口，不扩展故障矩阵。在最终新工程 `MUI-Clean-20261009` 中执行一次合并 Play Mode，再构建并运行同一 IL2CPP Development/High stripping Player；包为本地 MUI 0.1.0，Unity 2022.3.62f3，Apple M4 Pro / Metal。观察所用模型由包内 Analyzer 生成绑定；TMP 3.0.7 已安装，官方 TMP Essential Resources 已导入并保留。
- 公共控件观察原先仅输出值，本次为相同八个条件添加断言，每个后端的 16 项均通过：模型初始化无原生输入，结束编辑的草稿/提交、逐次编辑、模型回写不产生输入事件，原生下拉提交一次、模型写入不派发命令，以及关门后输入不回写。转换与资源槽批次通过 `reports=2`（两项预期故障），包括 Legacy/TMP 无效草稿保留、规范化、校验通知重入、OneWayToSource、解绑、迟到资源与直接赋值、赋值异常冻结和实际归还。Element 批次 `cases=9/assertions=38/reports=0`；View/Provider 批次 `cases=13/assertions=60/reports=2`，包含初始化回滚、未知部分清理、明确安全重试、边界扫描、常驻及加载 Prefab 的原生根归还。故意未知清理责任按原范围保留至 Play/进程结束，不重复回调。
- Play 与 Player JSON 均为 `completed=true/failed=false`；构建 Succeeded/errors=0/warnings=0，耗时 130.51 秒，Player 退出码 0。原始证据 `/private/tmp/mui-final-contract-batch-clean-editor-20261009.log`、`mui-final-contract-batch-play-20261009.json`、`mui-final-contract-batch-il2cpp-build-20261009.json`、`mui-final-contract-batch-il2cpp-player-20261009.log` 和同名 Player JSON；产物 `/private/tmp/mui-final-contract-batch-il2cpp-20261009.app`。Player 退出仍有 13 条 allocator 提示及 Unity Immediate MemoryLeaks 输出，未归因，不宣称干净退出或完整内存验收。
- 批次开始保存的 452 个 Runtime 源码 SHA-256 到结束均未变化；此次产物旁五个框架模块 Portable PDB 的 280 个 Runtime 文档校验值全部匹配当前文件，报告 `/private/tmp/mui-final-contract-batch-source-20261009.json`、`mui-final-contract-batch-pdb-audit-20261009.json`。这关闭上述四项旧记录的最终源码对应缺口；不由公共原生事件调用替代真实鼠标、IME、Input System 手势或其他尚未审计条件。
- 临时观察器首次缺少显式 Resource Integration 引用，构建报告计数字段也需按 Unity 2022.3 使用 int；两项修正均只在临时工程入口，包源码没有变化。观察、构建和 Player 通过后恢复 RecyclingList 场景，清理本次脚本、asmdef、场景和 meta 并刷新。未新增仓库测试，未自动提交或推送。

### 2026-10-09 交付布局及剩余运行证据核对

- 当前包的必需根文件齐全，Runtime/Editor/Analyzers 共 600 个 meta 无缺失、非法或重复 GUID；29 个 asmdef 无 Runtime → Editor/Samples 引用，所有 Editor 程序集仅包含 Editor 平台。Core 声明 noEngineReferences，Core/Resources/Navigation 源码及 asmdef 没有 UnityEngine/UnityEditor/Samples 引用。TMP 与 Input System 的 versionDefines 和 defineConstraints 与按需依赖一致，未加入基础必需依赖。生成器 DLL SHA-256 仍为 `d5dc8324970e10b2960413cc0382b5fef447f8dc80e311893da06eb5f38b44c7`。
- 三份示例接入说明修正后，重新逐文件核对导入内容，八项 252 个文件全部一致；报告 `/private/tmp/mui-final-delivery-layout-20261009.json` 同时保存清单、程序集边界和内容核对。实际执行源码规范检查，614 个 C# 文件的成员布局、人工冲突和控制流大括号违规均为 0，空白检查通过，进程退出 0；日志 `/private/tmp/mui-final-delivery-style-20261009.log`。这关闭本轮布局/格式核对，不代替实际 Inspector、输入或全部设计验收。
- 直接读取下列 IL2CPP 原始日志，核对确定性条件对应的结果，而非只引用历史汇总。较早控件与初始化记录保留原版本范围，最终源码对应仍属于 S3；不因日志存在而宣布整类完成。

  | 原设计条件 | 原始结果及范围 |
  | --- | --- |
  | 测量多模板、锚点与位置恢复 | `mui-list-layout-batch-final-il2cpp-player-20261009.log` 的横/纵 100/1,000/10,000、模板/内容版本、前项尺寸变化、锚点删除、Grid/视口、历史阅读、timeScale=0 和有界无效尺寸均 PASS，14/102、failures=0；真实持续拖动仍属 S1 |
  | 逻辑焦点与取消清理 | `mui-list-closing-batch-final-il2cpp-player-20261009.log` 的垂直/水平/Grid、Slider、在途用户新选择、取消/新请求/目标删除/父关闭、选择回调重入及页面恢复全部 PASS，11/22、failures=0；同文件保留普通/虚拟列表和固定槽位 47/637 记录 |
  | 父加载器寿命与资源槽责任 | `mui-loader-lifetime-final-il2cpp-player-20261009.log` 的父停止、迟到加载、各类回滚、缓存注入当前加载器、不可延长寿命不入缓存均 PASS，17/70、failures=0/reports=9；getter/null 失败路径 state=Displayed 且旧释放只尝试一次 |
  | 共享预加载消费者与退出 | `mui-preload-consumer-final-il2cpp-player-20261009.log` 的原调用/观察者分别取消、最后消费者取消、旧版本、重复快照、迟到归还、后台慢取消和宿主退出后共享后端可用均 PASS，25/101、failures=0/reports=0 |
  | 百次缓存复用与实际归还 | `mui-cache-budget-final-il2cpp-player-20261009.log` 为 cycles=100/assertions=1085/configurations=107/textureReturns=106/reports=2，恢复后 reserved=0/failed=0；预期故障与有意缓存持有按原批次记录区分 |
  | 公共 Legacy/TMP 提交与转换 | `mui-current-contracts-il2cpp-player-final-20261008.log` 两后端均记录初始化 nativeChanges=0、结束提交 Draft/events=1、逐次编辑 Live、模型回写不增加原生事件及关门不回写；`mui-completed-contracts-il2cpp-player-20261008.log` 两后端的 invalidPreserved/validNormalized/sourceOnly/modelReset/detached 均为 true。原生事件观察不代替 IME/物理输入 |
  | Element 初始化及视图最终归还 | `mui-element-property-final-il2cpp-player-20261008.log` 为 9/38/reports=0，另记录两项故意初始化异常；`mui-view-provider-final-fixed-il2cpp-player-20261008.log` 为 13/60/reports=2，未知失败责任保留原范围，不称为全部干净退出 |

- 较早控件批次另核对各自 IL2CPP 产物旁的 `BackUpThisFolder_ButDontShipItWithYourGame/Managed` Portable PDB 文档校验值，不使用可能被后续构建覆盖的共享 Bee 目录。current-contracts 的 171 个 Runtime 文档有 122 个与当前源码一致，completed-contracts 的 173 个有 132 个一致；两批的六个 TMP 文档，以及 Legacy 输入/下拉源码均一致，但 Core 绑定、生命周期与 UGUI 持有权相关文档存在后续变更。因此这些历史日志仍保留原范围，不能据局部一致关闭最终整链验收。差异报告 `/private/tmp/mui-contracts-pdb-source-audit-20261009.json` 记录文档路径、算法及旧/当前校验值；仅通过读取产物定位需补验范围，没有重跑未变化的矩阵。

上述具体原始日志均位于 `/private/tmp`。本轮未重跑未变化的 Player、未扩展故障矩阵、未新增仓库测试或自动提交；S1 的实体交互、S3 的最终源码对应继续保留；Unity 获取分配由下节完成。

### 2026-10-09 高频 View Provider 分配观察与接入说明

- 对当前 Core/Resources 源码运行独立 .NET 10.0.3 Release 分配观察，macOS 26.6.2 / Arm64；每条路径预热 2,048 次，再采样五轮、每轮 20,000 次。通过 `GC.GetAllocatedBytesForCurrentThread` 分开记录获取和归还，不在循环中生成日志或扩容报告。临时源码 `/private/tmp/mui-provider-allocation-20261009/Program.cs`，原始数据 `/private/tmp/mui-provider-allocation-dotnet-20261009.json`。最初项目引用还原等待被主动终止，最终使用本地源码和空包源完成，退出 0；没有新增仓库测试。

  | 路径 | 获取分配/次 | 归还分配/次 | 五轮结果 |
  | --- | --- | --- | --- |
  | DelegateViewProvider 完成 Task 的派发隔离 | 72 B | 0 B | 每轮 20,000 次获取全部内联完成，两次等待同一 Task 取得同一对象 |
  | DelegateViewProvider 每次创建 AcquiredView | 440 B | 656 B | 同上，归还完成后继续下一次获取 |
  | BorrowedViewProvider 获取/归还 | 504 B | 656 B | 同上，借用资格每次归还后恢复 |

- 第一项复用只读的临时占位凭证，仅隔离 Task 派发分配，不作为可重复交付同一生产凭证的示例。上述数字是独立 .NET 的受控路径，不包含 Unity Prefab、布局、业务绑定和真实后端，也不替代 Unity Mono、IL2CPP 或目标设备测量。当前 View Provider 继续使用可重复观察的 Task；没有仅凭这份数据引入 ValueTask 或修改 Runtime。Unity Editor Mono 的最终获取数据见下方；不由独立 .NET 数据代替 Unity 测量。
- 在最终新工程 Unity 2022.3.62f3 / Editor Mono / Apple M4 Pro / macOS 26.6.2 中完成同三条路径的分配观察。每条路径预热 2,048 次，再采样五轮、每轮 20,000 次；每帧执行 1,000 对获取/归还，使用 `RawFrameDataView` 读取获取及归还标记内的 `GC.Alloc` 大小元数据，排除标记外的 Editor 与观察器工作。所有 300,000 对调用都内联完成，两次观察同一获取 Task 返回同一对象；每轮获取/归还标记各 20,000 个，无缺失。原始数据 `/private/tmp/mui-provider-allocation-unity-20261009.json`，临时入口 `/private/tmp/MuiProviderAllocationObservation.cs`。

  | Unity Editor Mono 路径 | 获取分配/次 | 归还分配/次 | 五轮结果 |
  | --- | --- | --- | --- |
  | 完成 Task 的派发隔离 | 80 B | 0 B | 每轮相同 |
  | 每次创建 AcquiredView | 624 B | 2,144 B | 每轮相同 |
  | BorrowedViewProvider | 624 B | 2,144 B | 每轮相同 |

- 已知分配校验为 16 个 1,024 B 数组，原始 Profiler 精确记录 16 次、16,896 B（含数组对象开销），空标记为零。`GC.GetAllocatedBytesForCurrentThread` 在同工程对已知分配仍返回零，最初全零报告已被有效结果替换；`ProfilerRecorder` 的 GC.Alloc 值是耗时，未当作分配字节使用。读取原始样本的首轮因 Unity 无名样本而失败，修正只在临时观察器中。最终校验和完整采样通过，录制设置已恢复，临时工程脚本、meta 与空目录已清理并刷新；重新进入 RecyclingList Play 后实际 Console 为 0 warnings/0 errors。这关闭第 8 节高频获取分配记录条件；不包含 Prefab、业务绑定或真实加载后端，不宣称 IL2CPP 或目标设备分配相同。保持可重复观察的 Task API，本轮没有 Runtime 修改或仓库测试。

- 修正 Settings 清单说明，指向实际公开菜单；Reset 使用统一异步命令和立即完成的业务委托，不再描述为第二套同步命令。Settings、Common Patterns 和 DragDrop 的手动场景步骤明确保留活动 Camera，避免无相机提示遮挡 UI；Settings 的已验证画面与未验证真实输入分别记录。三个 README 同步到新工程导入目录，未改动运行中的奖励列表。

### 2026-10-09 Add package from disk 与示例相机修复

- 用户确认已在 Package Manager 执行“+ → Add package from disk”，选择本仓库的 `package.json`。随后核对窗口显示 MUI 0.1.0 / Local，manifest 引用 `file:/Users/mzbswh/GitHubRepository/MUI`，packages-lock 的 source=local、depth=0 且依赖与包声明一致。此项关闭第 8 节指定的窗口安装操作；此前 Client.Add 安装记录保留其原范围。
- Settings 公开入口生成的空场景没有 Camera，Game 视图的“No cameras rendering”提示覆盖 Overlay UI。集中修改 Settings、Tabs 和 Navigation 的三个 Editor 启动器、四处场景创建，使用 `NewSceneSetup.DefaultGameObjects` 保留模板相机，并说明用途。修改后的三个文件同步到新工程已导入的 Sample 后执行 Assets → Refresh；此步骤是修复同步，不称为 Package Manager Reimport。
- 通过公开菜单重新生成并进入 Play：Settings 显示音量、输入、Lock/Unlock、Save/Reset；Tabs 显示 Inventory content 与三个页签；Navigation 异步显示 Asynchronous page (ready)、Confirm/Close、嵌套内容和列表；RecyclingList 显示三项奖励与 Remove、Add reward、字体操作。四个场景均有 Main Camera、没有无相机覆盖提示，实际 Console 为 0 warnings/0 errors。Navigation 从初始天空盒到就绪页面及日志变化证明异步打开实际推进；截图不替代真实点击、IME或持续拖动验证。
- 按 Package Manager 的 Sample 显示名称定位导入目录，再次核对八项共 252 个文件，无缺失或内容差异。报告 `/private/tmp/mui-clean-final-sample-content-20261009.json` 保存本地引用、各 Sample 比对结果及三个启动器 SHA-256；本轮 Editor 日志保存为 `/private/tmp/mui-clean-camera-scenes-20261009.log`。再次计算 452 个 Runtime 文件的 SHA-256，与最终组合/性能批次快照完全一致；沿用已匹配源码的协议/AOT/性能记录，git diff --check 通过。本轮不新增仓库测试，不自动提交。

### 2026-10-09 S2 证据与当前源码对应核对

本轮直接读取观察入口及 Editor/IL2CPP 原始日志，并重新计算源码 SHA-256。`mui-contract-closing-source-20261009.json` 的 10 个文件、`mui-navigation-remainder-source-20261009.json` 的 223 个文件和 `mui-composite-baseline-source-20261009.json` 的全部 452 个 Runtime 文件均与当前仓库一致，无变化或缺失。因此下列条件沿用真实运行结果，不为证据整理重新构建。

| 对应设计第 8 节条件 | 已核对的实际结果 | 原始证据 |
| --- | --- | --- |
| 冲突关闭意图与重复确认 | Replace 期间 Close 为 Busy；Done 期间 Close/Back 为 Busy；各自 guardCalls=1；普通关闭期间 Done/Replace 为 Busy | `/private/tmp/mui-contract-closing-final-play-2-20261009.log` 与 `/private/tmp/mui-contract-closing-final-il2cpp-player-20261009.log` 的 replace-close、done-dismiss、duplicate-dismiss |
| 许可代际与取消观察者 | 参数/换绑 Applied 后旧 Close/Replace 为 Superseded，旧页面保持 Open；取消一个观察者得到 WaitCancelled，共享关闭仍等待且最终 Closed | 同上日志的 dismiss-args、replace-args、dismiss-rebind、replace-rebind、duplicate-dismiss |
| 队列满、迟到归还与同 Navigator 确认 | 满队列重复打开 Busy，其他页 Closed、Shutdown 推进，候选 CancelledBeforeCommit，取得/归还 2/2；确认页 Succeeded、原页 Closed | 同上日志的 capacity、confirmation |
| 命令准入与结果附着 | InputGate 关闭时已接纳命令 starts=1/writes=1；换绑后 oldWrites=0/newWrites=0；关闭后 oldWrites=0；最终 running=False 且凭证归还 | 同上日志的 command-gate、command-rebind、command-close |
| 未知退订失败不误报完成 | unbindFailed=True、oldSubscribers=1、oldRemoveCalls=1、retainedFailures=1；未知回调未重放 | 同上日志的 path-detach；观察源码 `/private/tmp/mui-navigation-closing-20261009/MuiClosingTrace.cs` |
| 历史与显示顺序分别维护 | A/B/C 历史保持 1,2,3，B 前台后关闭，剩余历史 1,3、焦点归 C，A 仍 Open | 同上日志的 history；更细的覆盖/Tick 与额度条件在 `/private/tmp/mui-navigation-remainder-final-il2cpp-player-2-20261009.log`，10 组/87 项、failures=0 |
| 子容器停用、父关闭与迟到准备 | deactivate-reactivate、parent-closes-active-and-inactive、parent-close-during-reactivation、safe-leaf-recovery-through-composite 均 PASS；4 组/100 项、failures=0，预期注入 reports=4 | `/private/tmp/mui-composite-baseline-final-play-20261009.log` 与 `/private/tmp/mui-composite-baseline-final-il2cpp-player-20261009.log`；观察源码 `/private/tmp/mui-composite-closing-20261009/MuiCompositeClosing.cs` |

以上关闭对应协议条件的证据核对，不关闭整个 S2，也不替代真实 Pointer、多宿主或输入法验收。Legacy/TMP 的旧 Player 日志保留原范围；当前源码的公共控件、转换与资源槽、Element 及 View/Provider 清理已由上方最终合并批次补齐 Play/IL2CPP 对应。本轮没有 Runtime 修改、仓库测试或自动提交。

### 2026-10-09 新工程八项 Sample 窗口导入

- 在 Unity Hub 新建的 `MUI-Clean-20261009` 中，通过 Package Manager 的 Import 按钮逐项导入 Basic Example、Resource Integration、Settings、Navigation、Dialogs、Common Patterns、Tabs 和 DragDrop；Resource Integration 先于依赖它的 Navigation 和 Common Patterns。逐文件核对共 252 个文件，无缺失或内容差异，报告 `/private/tmp/mui-clean-samples-window-import-20261009.json`。此记录关闭窗口导入条件，不代表八项运行交互全部通过。
- Basic 从公开菜单打开并进入 Play，Editor、Game 页面和退出 Play 后的天空盒均实际绘制正常，没有旧工程的粉色界面；此前键盘 Space 产生 Completed/42 日志。只读观察记录 frameCount 持续递增，同时鼠标尝试期间 Application.isFocused=false、focusedWindow=none，点击未产生完成结果；焦点与真实鼠标交互仍未确认，不据此修改框架或宣布鼠标验收通过。观察日志 `/private/tmp/mui-clean-interaction-observation-20261009.log`。
- 已退出 Play 并清理临时只读观察脚本及 meta，刷新后实际 Console 显示 0 warnings/0 errors，保留导入的包、八项 Sample 和打开的 Basic 场景；未修改框架源码，未新增仓库测试或自动提交。新工程重建及导入已完成，S1 其余实体交互条件继续保留。

### 2026-10-09 Unity Hub 重建最终交互验收工程

- 旧 `MUI-Acceptance-20261008` 实体窗口重现 Editor 粉色界面与 Package Manager 黑色内容；日志 `/private/tmp/mui-final-samples-ui-20261009.log` 包含内置 GUI Shader 找不到 HLSLSupport.cginc 的错误，不能沿用历史缓存重建记录认定本轮画面正常。按用户要求关闭旧 Editor，保留旧工程与协议验收证据，通过 Unity Hub 的 New project 创建桌面 `MUI-Clean-20261009`，选择 2022.3.62f3 Silicon 与官方 3D Built-In 模板，没有复制旧 Library、设置或观察脚本。
- 导入包前已实际观察默认天空盒、Editor 文字/面板正常、Console 0 warnings/0 errors。通过一次性 UPM Client.Add 安装 `file:/Users/mzbswh/GitHubRepository/MUI`；安装后六个基础程序集加载，四个内置 Shader（GUIRoundedRect、GUITextureBlit、UI/Default、Skybox/Procedural）均 supported 且无 Shader 编译错误。报告 `/private/tmp/mui-clean-project-install-20261009.json`、Editor 日志 `/private/tmp/mui-clean-project-editor-20261009.log`。临时安装入口最初发生 PackageInfo 类型歧义，修正为完整类型名后安装通过；不将这个已修复的安装脚本错误记为 MUI 编译缺陷。
- 安装完成后实际 Package Manager 画面显示 MUI 0.1.0、Local 及本仓库路径，绘制正常。一次性入口已由 AssetDatabase 删除，空目录已清理；新工程 Assets 只有模板场景，没有 C# 观察脚本、Runtime 副本或 Samples。窗口点击添加菜单未响应，因此本轮使用官方 Client API 安装，不声称已经完成 Add package from disk 的窗口操作或八个 Sample 导入/交互；后续实体验收使用此新工程。仓库实现未变，旧双后端协议与性能证据保留原范围。

### 2026-10-09 示例导入依赖与文档核对

- 对照 package.json 的八项 Sample、各示例 asmdef 引用及 README，跨示例依赖只有 Navigation → Resource Integration 和 Common Patterns → Resource Integration；两项均在清单说明及文档写明先后导入顺序，其他六项没有遗漏的跨示例程序集依赖。八项均包含 README 和接入源码。此为静态包交付核对，Package Manager 窗口导入及最终示例运行仍待验收。
- DragDrop README 仍描述已经移除的纯同步模式，与当前 DragSession 构造时要求 UI SynchronizationContext、后台派发和 Pump 回退不符；按当前实现修正，并保留最终实体交互的证据边界。删除 Tabs README 的空标题。只修改文档，不重建未变化的 Runtime/Player。

### 2026-10-09 Editor 增量检查剩余路径

- 复用独立验收工程的 FullScreen 向导产物，只复制并操作 `Assets/MuiEditorIncrementalClosing` 下的临时 Prefab，不修改原页面。通过真实 AssetDatabase.MoveAsset/DeleteAsset 验证移动后重新采集当前路径，缺失 Element 的报告包含新路径；删除后登记目录拒绝缺失 Prefab，报告目录采集失败，没有保留旧有效结果。
- 实际进入 Play Mode 后保存无效 Prefab，持续观察超过 0.2 秒合并窗口：目录采集次数不变，没有增量报告，SessionState 待检查标记仍为 true。实际退出 Play 后，采集次数只增加一次、标记清除，报告包含当前 Prefab 的 Missing Element。此批为验证无域重载的纯 Play 延后路径，临时关闭进入 Play 的域/场景重载并在结束恢复原配置；真实编译/域重载的标记保留继续使用此前 41 项记录，不以本批替代。
- 同一 Editor 进程完成 20 项检查，PASS、退出码 0；日志 `/private/tmp/mui-editor-incremental-final-20261009.log`，临时观察源码 `/private/tmp/MuiEditorIncrementalClosing.cs`。结束取消登记并删除自己的 Prefab 目录。删除故障信息中的反射调用外层异常来自临时目录适配，属于主动构造的缺失资产结果。本批没有修改框架实现，不重建 Player；顶部 Editor 行只保留 Inspector 实体画面与跳转缺口。

### 2026-10-09 生成器包产物一致性核对

- 当前生成器 Release 构建与包内 DLL 的 SHA-256 不同；未提交源码差异仅为 UIGenerator.Commands.cs 删除一处空行。先在独立临时进程中加载旧包及新 Release 的增量生成器，对八项 Sample 的实际声明分别运行，24 份生成源码逐字相同、生成器无错误诊断；日志 `/private/tmp/mui-final-generator-comparison-20261009.log`。这项比较证明本次包同步不改写上述生成接线，不代表任意项目声明的编译证明。
- 同步 `Analyzers/MUI.Generators.dll` 后，包文件与 Release 文件 SHA-256 均为 `d5dc8324970e10b2960413cc0382b5fef447f8dc80e311893da06eb5f38b44c7`；原 RoslynAnalyzer 标签及导入配置保持有效。Release 构建 0 warnings/0 errors，日志 `/private/tmp/mui-final-generator-build-20261009.log`。独立 Unity 工程重新编译并核对八项示例及 TMP/TMP.Themes/InputSystem 共 11 个程序集，日志 `/private/tmp/mui-final-generator-unity-compile-20261009.log`，退出 0。Runtime 未变，临时 Player 的绑定接线亦未使用本次格式差异，因此不为同一生成输出重建或重采性能。
- 静态包布局核对 Runtime/Editor/Analyzers 的 600 个 .meta 记录，无缺失、无非法或重复 GUID；八项 Sample 路径及根级必需交付文件均存在，git diff --check 通过。关闭源码/Analyzer 产物一致性的本项缺口；最终 UPM/文档及第 1–8 节整体核对仍保留。无仓库测试或自动提交。

### 2026-10-09 原生组合容器与最终性能基线

- 本批固定补齐四条组合路径，不展开任意容器排列。通过真实 PrefabViewProvider 在同一父页面挂载面板，每个面板同时包含静态 NestedViewElement、3 项普通列表、100 项虚拟列表及 2 个借用固定槽位；各行含原生文本、纹理资源槽和初始化/最终清理观察控件。所有投影模型均借用。首轮一项失败来自观察脚本将未物化模板也计入已初始化控件，修正计数前提后没有复现框架缺陷。
- 最终 Editor 与 macOS IL2CPP Development/High stripping 同批通过 4 组、100 项检查、failures=0/reports=4。停用结束旧嵌套作用域、通知订阅及纹理凭证，保留同一原生面板；恢复建立新激活作用域、保持原句柄与模型、不重复 OnCreate/Element 初始化，显示停用期间更新的数据。活动与停用兄弟同时被父关闭后，均完成凭证归还与原生销毁，停用 Presenter 不重复 OnClose。父关闭遇到忽略取消的资源准备时保持节点与归还责任，迟到完成不能附着，排空后才归还。虚拟条目的明确可重试部分归还失败阻止整个父资源返回；显式叶重试再逐层确认后原生面板确实销毁、账本归零、后端只实际归还一次，首次失败结果不改写。四项报告分别为注入叶失败及各层未确认依赖，没有把未知清理猜测为成功；未知通知/最终监听继续复用此前嵌套与 47/637 记录的范围。
- 同一 Player 接入已有六组性能驱动，复用固定/测量双模板、100/1,000/10,000 项与每组 600 帧的配置，不新增性能场景矩阵。3,600 帧的维护 CPU 全部有效、业务采样未超出同帧维护片段，测量帧都有原生耗时；展示实例最多 11、可见最多 7、全部模板节点总数最多 11，小于容量 24；单帧测量最多 4。框架维护 P95 为 2.707–3.612 ms、P99 为 4.050–4.499 ms，整帧 GC 均值约 174–184 KiB。保留高于旧源码记录的成本，不作预算达标或独立归因结论。当前逐帧 CSV、业务/原生测量分位数、设备与限制见 [性能基线](VIRTUAL-LIST-BASELINE.md)。Editor CPU 的 3,599 个零样本未作为基线。
- 日志 `/private/tmp/mui-composite-baseline-final-play-20261009.log`、`/private/tmp/mui-composite-baseline-final-il2cpp-build-20261009.log`、`/private/tmp/mui-composite-baseline-final-il2cpp-player-20261009.log`；构建 Succeeded/errors=0/warnings=0，Editor、构建、Player 均退出 0。产物 `/private/tmp/mui-composite-closing-il2cpp-20261009.app`。452 个 Runtime 源码 SHA-256 清单 `/private/tmp/mui-composite-baseline-source-20261009.json` 在运行后复核未变；Player 仍有 13 条 allocator 退出提示，未归因，不宣称完整内存验收。
- 本批关闭顶部复杂组合退出和最终桌面性能基线缺口，Basic 接入成本文档也已按原设计核对。没有新增仓库测试或自动提交；S1 真实示例/Editor 交互、资产移动/删除及 Play 后补跑，和 S3 全设计证据对应核对仍保留。

### 2026-10-09 剩余导航条件合并收尾

- 使用临时公开 API 观察核对本轮开始列出的导航缺口，复现普通实例满额返回 InstanceLimit 而设计要求 Busy 的差距。Navigator 接纳只将 InstanceLimit 用作显式 CloseOldest 的内部信号；默认满额、准备中无可替换旧页及溢出替换的额度拒绝向普通 Open 调用方统一返回 Busy。不新增 Provider 获取，不改变显式溢出替换的守卫流程。修改 Navigator.cs、Navigator.Open.cs、Navigator.Overflow.cs，保留现有枚举值及 Navigator 名称。
- 最终协议观察通过 10 组、87 项检查、failures=0/reports=1，唯一报告为主动注入的退出采样异常。队列容量 8 下，Reject/ReturnReady 两种单实例策略只接纳一份准备，其他页面可打开；取消后迟到凭证归还，实例额度恢复，已有就绪实例按声明复用或 Busy。相同 Route 的 Replace 借用旧页名额、并发替换 Busy、取消后重试及关闭后重开均成功，候选各有独立 Presenter；Done/Back 两种先后顺序各发布一次不可变结果；提交后的退出故障结束视觉保留且不能复活旧句柄。隐藏页继续模型通知和绑定，默认暂停 Tick；返回恢复不重复 OnOpen，InputGate 关闭时可见 Tick 继续。CloseOldest 保留正常守卫，只有准备中实例时不启动第二份获取。
- 同一 Unity 批次另验证实际 Time.timeScale=0、Time.deltaTime=0 时，UIHost 的自动帧驱动完成非零时长进入/退出，不手动调用 Navigator.Tick；Editor 经过 1519 帧、Player 经过 1577 帧，取得/归还各 1，进入/退出均 Completed。协议 View 并非原生控件，此证据只证明导航、绑定和引擎帧时钟契约，不代替多宿主或 Pointer 的实体交互。
- 最终 Editor 日志 `/private/tmp/mui-navigation-remainder-final-play-2-20261009.log`；macOS IL2CPP Development/High stripping 构建 Succeeded/errors=0/warnings=0，日志 `/private/tmp/mui-navigation-remainder-final-il2cpp-build-2-20261009.log`；Player 同批通过，日志 `/private/tmp/mui-navigation-remainder-final-il2cpp-player-2-20261009.log`，产物 `/private/tmp/mui-navigation-remainder-il2cpp-20261009.app`。三个进程均退出 0。223 个相关源码 SHA-256 记录在 `/private/tmp/mui-navigation-remainder-source-20261009.json`，运行后复核无变化。Player 退出日志含 allocator 统计与 MemoryLeaks 遥测，未据此作完整内存验收结论。
- 最终 614 个 C# 源码的成员布局、控制流与空白检查通过，日志 `/private/tmp/mui-navigation-remainder-final-style-2-20261009.log`；git diff --check 通过。本批关闭顶部导航行原列出的剩余协议条件；实体示例/Editor 操作、复杂组合容器退出和最终性能/交付核对仍保留。没有新增仓库测试或自动提交；已通过条件不继续扩展任意故障矩阵。

### 2026-10-09 关闭意图、许可代际与通知清理合并批次

- 临时公开 API 观察复现三项真实缺口：Replace 守卫等待期间普通 Close 再次调用同页守卫（calls=2）；Done 决议中的 Close/Back 合并观察不同意图；路径替换退订抛错后旧拥有者被清空，最终解绑成功而旧订阅仍存在（retainedFailures=0）。源码修复共用 ViewInstance 上的关闭意图记录，冲突返回 Busy；CloseApproval 同时比较业务守卫版本与页面提交版本，确认期间的参数更新/换绑可成功提交并使旧许可 Superseded。路径保留失败拥有者和原异常，失效监听不再派发；BindingSession 将失败订阅/最终回调接入同一 CleanupRegistry，保留实际回调与所属身份，不隐式执行未知 remove/finalizer。
- 最终合并观察包含 15 组协议路径的实际输出，正常路径凭证取得/归还相等、宿主未确认责任归零：Replace/Close 与 Done/Close/Back 的冲突立即 Busy、反向普通关闭中的 Done/Replace Busy；参数和绑定提交使 Close/Replace 许可失效且旧页面仍 Open；普通关闭共享一次守卫、取消一个观察者不取消其他等待；ForceClose 后迟到守卫不能重新提交；队列上限 1 时其他页关闭和 Shutdown 可推进，迟到候选归还；已就绪单实例复用不增加获取；A/B/C 的历史不因 B 置顶而改写，关闭 B 后 C 获得焦点；同 Navigator 打开确认框正常完成；关门期间有效保存结果仍提交，换绑/关闭后的旧结果没有写入。满队列的重复打开记录不能代替非满队列的实例额度并发验收，历史焦点观察不证明覆盖后的 Tick/激活次数。
- 路径故障观察明确得到 unbindFailed=True、oldSubscribers=1、oldRemoveCalls=1、retainedFailures=1：未知退订继续持有责任，不通过重复解绑重放回调，不把未确认订阅报告为已归还。该组及导航组使用实际框架和协议 View，不能作为物理输入或原生页面画面证据。另在同一运行中复用既有原生父子换绑 7 组/预期 reports=2，及嵌套清理 126 项、外部/通知/最终回调清理 54 项断言，均通过；未知故障按原契约持续保留，不要求整批账本清零。
- 最终 Editor 日志 `/private/tmp/mui-contract-closing-final-play-2-20261009.log`；同一批 macOS IL2CPP Development/High stripping 构建 Succeeded/errors=0/warnings=0，Player 取得同样记录及原生/清理回归通过。构建与运行日志 `/private/tmp/mui-contract-closing-final-il2cpp-build-20261009.log`、`/private/tmp/mui-contract-closing-final-il2cpp-player-20261009.log`，产物 `/private/tmp/mui-contract-closing-il2cpp-20261009.app`。三个进程退出码均为 0；Player 仍有 13 条 allocator 退出提示，不宣称干净退出。614 个源码规范检查通过，日志 `/private/tmp/mui-contract-closing-final-style-20261009.log`；本批 10 个源码的 SHA-256 记录 `/private/tmp/mui-contract-closing-source-20261009.json`。临时观察仅在 `/private/tmp` 和独立工程，没有仓库测试或自动提交。
- 关闭本批已匹配的意图、许可、命令结果和退订故障条件；S2 的其余确定性条件仍按顶部清单核对，S1 的真实示例/Editor 输入与 S3 的最终交付仍未完成。Navigator 名称保留，其职责是页面导航，UIHost 负责组装与驱动；不因命名询问新增全仓库重命名。

### 2026-10-09 收尾批次固定与最终 Sample 刷新

- 剩余交付集中为 S1 示例与编辑交付、S2 剩余契约核对、S3 最终交付核对，停止条件见顶部表格；没有修改设计完成标准或将局部通过改成整体完成。本轮只修改状态记录，不为文档变化重复运行源码规范或 IL2CPP。
- 独立工程通过 Package Manager 的 Sample.Import(OverridePreviousImports) 刷新八项 Sample，Resource Integration 在 Navigation/Common Patterns 前导入。逐文件核对共 252 个文件，无缺失、无内容差异；随后同一工程集中编译，八项示例及 MUI.TMP、MUI.TMP.Themes、MUI.UGUI.InputSystem 共 11 个程序集加载核对通过。两次 Editor 进程退出码均为 0，日志 `/private/tmp/mui-samples-final-import-20261009.log`、`/private/tmp/mui-samples-final-compile-20261009.log`。这证明最终导入内容和编译，不证明 Sample 已全部运行或真实输入已通过。
- UI 工具按 Unity 应用路径取得的窗口属于无关工程，自动审批拒绝继续读取该窗口，未向其发送输入。Package Manager 窗口操作、真实交互与画面仍待验收；已使用独立工程 CLI 完成不受该限制的导入与编译。尝试关闭本轮 Editor 时审批要求先确认 PID，随后只读核对和原工具会话确认该进程已经以 0 退出，没有执行进程终止。S1 尚未关闭，完整目标保持未完成。

### 2026-10-09 场景、真实资源后端与屏幕布局合并收尾批次

- 原生观察复现活动 View 的 GameObject/组件被外部销毁后，最终责任中的输入通知同步触发导航关闭，归还路径再次等待同一最终责任，导致凭证 Failed 和宿主提供方滞留。View 现在先标记原生身份失效并独立通知拥有者，再观察同一次异步最终清理；原生销毁有慢归还时，只等待拥有者已经启动的激活清理，不提前释放作用域或隐式重试。LifetimeScope 提供框架内部只读 DisposalCompletion；正常显式 Dispose 及历史失败语义保持。缓存重置和最终释放清除激活引用。
- 首轮 8 组的另外两项失败来自观察前提：缓存维护间隔为 1 秒，不能要求四帧内淘汰；Unity 在没有 worldCamera 时实际报告 ScreenSpaceOverlay，不能按仍处于 Camera 模式断言配置异常。最终等待真实淘汰并记录原生有效模式。SafeAreaFitter 按有效 Camera 模式要求明确相机；本批观察证明 Unity 的 Overlay 回退与有效 Camera 的坐标，不能声称已经触发缺相机拒绝分支。临时程序集最初漏 ResourceIntegration 引用，修正只在独立工程。
- 同一 Editor 两场景通过环境 9 组/74 项断言及既有宿主责任 16 组/67 项断言，合计 25 组/141 项。覆盖 Overlay/Camera 与原生 CanvasScaler、测量双模板列表的 Start/Center/End 定位和阅读锚点、安全区及项目键盘像素区域、Camera 视口变化、真实 UnityResourcesLoader + LoadedPrefabViewProvider 的 Prefab/纹理取得和延迟归还、等待 Shutdown 后卸载场景、显式 DontDestroyOnLoad 宿主的活动与缓存 View 跨场景、活动 GameObject/组件销毁、慢归还时输入撤销和等待，以及缓存 View 销毁后的淘汰/重新创建。正常资源路径取得/归还相等，各宿主未确认责任归零；注入未知归还失败的既有宿主回归仍保留责任。
- 同一 macOS IL2CPP Development/High stripping 双场景 Player 通过环境 9/82/0/3、宿主 16/67/0/12，合计 25 组/149 项。Player 使用真实窗口，启动参数请求 1200×800；日志直接确认切换后的实际尺寸为 1000×700、700×1000，两种 Canvas 模式都记录实际像素矩形和局部 Canvas 尺寸。安全区/键盘投影误差与定位/锚点误差均不超过 1 个对应坐标单位。额外 8 项断言来自 Player 实际分辨率变化；Editor 的 640×480 记录不冒充该验证。环境 reports=3 是三次故意销毁活动 View 的预期异常，宿主 reports=12 为既有故障注入。
- 日志 `/private/tmp/mui-environment-final-play-20261009.log`、`/private/tmp/mui-environment-final-il2cpp-build-20261009.log`、`/private/tmp/mui-environment-final-il2cpp-player-20261009.log`；产物 `/private/tmp/mui-environment-il2cpp-20261009.app`。BuildReport Succeeded/errors=0/warnings=0，Editor、构建、Player 退出码均为 0；Player 仍有 13 条 allocator 退出提示，未归因，不宣称干净退出。离线 ResourceIntegration/依赖编译 0 warnings/0 errors，614 个源码规范检查通过，日志 `/private/tmp/mui-environment-fixed-build-20261009.log`、`/private/tmp/mui-environment-fixed-style-20261009.log`；git diff --check 通过。无仓库测试、无自动提交。关闭本批场景/资源后端/坐标条件，继续固定清单中的复杂容器、真实示例输入、Editor 画面与性能条件，不扩展本批故障矩阵。

### 2026-10-09 Editor 与三类页面向导合并收尾批次

- 源码审查确认两项真实缺口：PagePreset 只有 FullScreen/Popup，且导入/保存增量调度未接入。向导增加 Notice，使用现有 RoutePolicy 的 layer=200、coverage=None、takesFocus=false、enterHistory=false、backBehavior=Ignore；生成纯文本无射线目标 Prefab，不生成关闭按钮/命令，业务通过句柄显式关闭。已有提示 Prefab 必须无 Selectable、无射线目标，缓存校验键加入预设，避免切换预设后沿用旧结果；默认两份业务源码且无需 Presenter，不覆盖原目录。
- 新增 UIIncrementalValidation 与 UIValidationAssetPostprocessor。导入回调只收集合并路径，Editor 空闲后重新采集目录，按当前 Prefab 依赖筛选受影响目录，再复用完整目录校验规则。脚本/程序集变化、域重载及移动/删除使用完整重采集；编译、导入和 Play Mode 暂停执行，SessionState 保留域重载后的待检查标记。最多 2,048 个待检查路径，达到上限转完整检查；不写资产、不保留路由/模型/Prefab 缓存，也不重复输出未变化报告。手动与构建检查不依赖该队列或旧结果。
- 独立工程同一 Editor 会话通过 41 项断言：向导实际创建三类 Prefab/源码、拒绝覆盖、不要求 Presenter；生成 Manifest 与 Prefab 契约及源码行号匹配；两次 Prefab 保存只触发一次增量采集；错误报告定位 Prefab，构建前检查重新读取当前错误并抛 BuildFailedException；修复保存后检查及构建前校验恢复；真正修改脚本触发编译/域重载，待检查标记保留并完成补跑。随后同一会话进入 Play Mode，通过公开 Navigator/PrefabViewProvider 运行三类生成页面，验证标题绑定、弹窗模态焦点、提示不抢焦点/返回/射线、生成关闭命令、提示显式关闭及最终原生 View 全归还（其中运行观察 17 项断言）。
- 最终日志 `/private/tmp/mui-editor-closing-final-play-2-20261009.log`，Editor 退出码 0。首轮临时入口试图在第一次向导创建触发编译后立刻创建第二类页面，被已有忙碌保护拒绝；修正仅在临时入口内，通过同一 Editor 会话等待实际编译后继续创建，未削弱向导保护。离线 MUI.Editor/依赖编译 0 warnings/0 errors，日志 `/private/tmp/mui-editor-closing-build-20261009.log`；614 个 C# 文件规范检查通过，日志 `/private/tmp/mui-editor-closing-style-20261009.log`。本批修改属于 Editor，不重复构建无关 Player；布局 Player 的运行记录见下节。临时脚本仅在独立工程及 `/private/tmp`，未新增仓库测试，未自动提交。
- 本批关闭上述两项实现缺口及已观察条件；Inspector 实际画面/跳转、移动/删除和 Play Mode 队列补跑尚未取得最终运行证据，保留在固定清单，不用此批代表全部 Editor 交付。实体输入、示例交互、场景/持久宿主和最终性能等其他剩余条件继续保留。

### 2026-10-09 多模板测量与位置恢复收尾批次

- 同一临时观察驱动在 Editor Play Mode 和 macOS IL2CPP Development/High stripping Player 均通过 14 组、102 项断言，failures=0/reports=1（预期无效尺寸诊断）。横向/纵向分别覆盖 100、1,000、10,000 条双模板测量列表及 Start/Center/End 远距离定位，误差不超过 1 Canvas 单位；12 次快速跨模板定位核对模型身份和模板池。配置为 Overlay Canvas、600×240 视口、估算尺寸 80、overscan=1、capacity=24、每帧测量上限 2，无资源加载；物化不超过可见数加 2，未为定位测量全部前置项。
- 同批覆盖同键/显式内容版本/模板的测量迁移、无版本的新实例失效、NotifyUpdated 与单项尺寸失效、Reset/换源、显式兼容来源恢复、前项与可见项尺寸变化、头部插入、锚点删除后的后继/前驱/空表回退、单列与三列 Grid 切换、视口变化、末尾跟随与历史阅读、timeScale=0 平滑定位、原生 Scroll/BeginDrag 事件取消、无效尺寸有界回退及更新恢复。此处原生事件模拟不替代真实鼠标持续拖动时的指针补偿验收。
- 首轮两项失败来自临时驱动把 Extent 写成普通字段，NotifyUpdated 并未改变原生 LayoutElement 尺寸。最终改为通知属性经真实 Property 绑定更新原生尺寸后通过，包布局源码无需修改；不把观察前提错误记录为包缺陷，不继续扩展已通过的本批矩阵。
- 日志 `/private/tmp/mui-list-layout-batch-final-play-20261009.log`、`/private/tmp/mui-list-layout-batch-final-il2cpp-build-20261009.log`、`/private/tmp/mui-list-layout-batch-final-il2cpp-player-20261009.log`；BuildReport Succeeded/errors=0/warnings=0，Editor、构建、Player 退出码均为 0。产物 `/private/tmp/mui-list-layout-il2cpp-20261009.app`。Player 退出仍有 13 条 allocator 提示，未归因，不宣称干净退出或目标设备性能达标。临时驱动仅在独立工程与 `/private/tmp`，未新增仓库测试或自动提交。

### 2026-10-09 焦点与容器清理合并收尾批次

- 实际复现虚拟列表父关闭时的两条预期取消被聚合并报告为清理失败；日志 `/private/tmp/mui-list-focus-required-resource-play-20261009.log`。`PrepareItemAsync` 清空条目后继续观察到的旧准备取消，现在只在任务确实取消且原刷新已失效时忽略该重复观察；setter、未取消的准备故障及真实节点归还失败仍保留。不全局屏蔽 OperationCanceledException，也不丢弃未知清理责任。普通列表已有父激活取消分支，本批未另行修改。
- 同轮两个断言问题来自临时观察条件：对仍被选中的同一 Outside 对象再次 SetSelected 不构成可观察的新选择；BorrowedViewProvider 隐藏借用 View，不销毁其可复用原生节点。最终驱动先聚焦旧条目，再在准备等待期间选择 Outside；父关闭检查业务清理 Complete、View 不可见/不可输入、全部资源凭证已归还和原生选择已撤销，而节点由最终 View 清理处理。这两项不作为包焦点缺陷记录。前两轮还误用换绑准备及渐进资源加载制造等待；最终明确 `View.WaitForResourceSources=true`，并断言焦点任务确实仍在途。
- 临时驱动 `/private/tmp/mui-list-focus-20261009/MuiListFocusObservation.cs` 最终通过 11 组、22 项断言、failures=0/reports=0：原生垂直/水平/Grid 方向、Slider 调值、回收后逻辑控件路径恢复、显式在途焦点保留用户的新选择、取消/新请求/目标删除、父关闭、选择回调取消/来源重置，以及页面异步恢复竞争。相关控件路径和焦点实现无需改写；本批实现修改为预期取消诊断。
- 与既有普通/虚拟列表及固定槽位矩阵合并为同一 Editor 运行和同一双场景 IL2CPP Player，后者通过 47 组、637 项断言、reports=46（观察阶段预期注入错误）。整个批次为 58 组、659 项断言，两个后端汇总一致。Editor、构建和 Player 退出码均为 0；BuildReport Succeeded/errors=0/warnings=0。日志 `/private/tmp/mui-list-closing-batch-play-20261009.log`、`/private/tmp/mui-list-closing-batch-final-il2cpp-build-20261009.log`、`/private/tmp/mui-list-closing-batch-final-il2cpp-player-20261009.log`；产物 `/private/tmp/mui-list-closing-batch-il2cpp-20261009.app`，Development/High stripping。每轮保留未知失败根至进程结束，退出另有 4 条相关清理诊断；Player 另有 13 条未归因 allocator 提示，不报告干净退出。
- 集中的 Navigation/依赖编译 0 warnings/0 errors，612 个源码规范检查及 git diff --check 通过；日志 `/private/tmp/mui-list-closing-batch-build-20261009.log`、`/private/tmp/mui-list-closing-batch-style-20261009.log`。本批临时合并入口 `MuiListClosingBatchMenu` 只在独立工程中，未新增仓库测试或自动提交。上述条件验收后关闭本批，不继续扩展同一故障矩阵；多模板内容版本测量、真实示例输入、场景/持久宿主、Editor 增量校验及原设计其他剩余条件按固定清单继续完成。

### 2026-10-09 资源槽、迟到结果与加载器寿命

- 原生 Texture 观察复现迟到加载/预加载叶凭证恢复后所属作用域仍无法确认，以及旧资源责任 getter 抛错使新显示失去独立清理诊断；日志 `/private/tmp/mui-loader-ownership-runtime-reproduction-play-20261009.log`，7 组中 5 组失败。候选归还路径现在复用公开三参数 `CleanupRegistry.ReleaseAsync(resource, owner, lifetime)`，捕获一次稳定责任和真实确认条件，调用必须在所属跟踪操作退出前完成。迟到资源和预加载不重复登记无确认入口的未知错误；项目属性异常仍尝试一次真实归还，新显示不因旧清理诊断被撤销。
- `ResourceLoadException.CleanupCompletion` 等待归原加载操作；失败回滚保留独立稳定不可安全重试责任，不猜测后端已归还。资源槽的清理诊断保留首次失败及次数。`ElementResourceOwner` 在本次尝试开始前判断子责任是否已确认，首次错误必须传播，只有后续显式恢复才完成剩余解除持有；第一版在释放后判断确认而吞掉首次错误，日志 `/private/tmp/mui-loader-ownership-fixed-play-20261009.log` 的 7 组中 2 组失败，最终基础矩阵 `/private/tmp/mui-loader-slot-first-result-fixed-play-20261009.log` 通过 7/30/0/6。
- 最终临时驱动 `/private/tmp/mui-loader-ownership-20261009/MuiLoaderOwnershipObservation.cs` 通过 17 组、70 项断言、failures=0/reports=9（预期注入错误）。覆盖迟到加载/预加载的安全叶恢复，责任属性抛错/null 与成功/失败真实归还，父加载器停止后子凭证及迟到凭证归还，资源/预加载/资源槽回滚成功与失败，真实 Prefab 缓存命中重新注入当前加载器，不支持缓存的 View 关闭实际归还，以及最近父 View 的资源继承边界。未知失败责任保留，不强制销毁或重放 callback。Play Mode 日志 `/private/tmp/mui-loader-lifetime-expanded-matrix-fixed-play-20261009.log`；前一扩展日志仅因临时观察程序集缺 Navigation 引用未能运行，修复限于独立工程，不改变包程序集或 Sample 配置。
- 最终 macOS IL2CPP Development/High stripping 同样通过 17/70/0/9，BuildReport Succeeded/errors=0/warnings=0，Editor、构建及 Player 退出码均为 0。日志 `/private/tmp/mui-loader-lifetime-final-il2cpp-build-20261009.log`、`/private/tmp/mui-loader-lifetime-final-il2cpp-player-20261009.log`，产物 `/private/tmp/mui-loader-ownership-il2cpp-20261009.app`。退出仍有 13 条 allocator 提示，未归因，不宣称干净退出或完整内存验收。
- 既有资源槽、原生赋值故障及 Legacy/TMP 转换 Play Mode 回归通过，reports=2（预期注入错误），7 个获取凭证全部归还；日志 `/private/tmp/mui-loader-lifetime-final-contract-regression-play-20261009.log`，Editor 退出码 0。Navigation 及依赖离线编译 0 warnings/0 errors，日志 `/private/tmp/mui-loader-lifetime-final-build-20261009.log`；612 个源码成员布局、大括号和空白检查通过，日志 `/private/tmp/mui-loader-lifetime-final-style-20261009.log`，git diff --check 通过。本项使用可保证独立归还的受控加载器，不证明任意项目后端均满足依赖寿命；复杂容器、完整异步焦点、多模板布局、实体输入/IME、八项示例、场景和目标设备性能继续验收。未新增仓库测试，未自动提交，完整目标未完成。

### 2026-10-09 宿主提供方责任与原生意外销毁

- 公开 UIHost + 真实 Texture 驱动复现四项冲突：提供方部分释放后抛错无宿主责任记录；恢复后的预加载查询为 0 而导航快照仍为 1；历史清理错误永久阻止提供方释放；宿主销毁后既无延后提供方释放入口，也看不到在途异步提供方清理。日志 `/private/tmp/mui-host-ownership-runtime-reproduction-play-20261009.log`，首轮 6 组有 4 组失败。临时驱动此前将责任值快照与 null 比较的编译错误单独修正，不能作为包源码故障证据。
- UIHost.ProviderCleanup 将宿主拥有的提供方交给独立于原生组件的持有对象，复用 CleanupResponsibility/Registry。正常和兜底退出共享首次任务，清理按实际导航依赖确认，排除自身责任而不依赖 HasCleanupFailure 历史标记。未确认资源继续持有提供方；允许的叶责任显式恢复后，外层只执行尚未开始的最终释放或检查提供方已确认结果。未知同步/异步提供方只尝试一次并保存不可重试回调，提供方责任属性抛错/null 仍执行一次真实清理；内部叶确认不能代替未知外层 callback。预加载数量快照同步使用稳定确认值，不从诊断启动维护。
- 提供方源属性与释放阶段的首次补充矩阵暴露阶段上下文未保留；现从明确 UIHost.Shutdown/ProviderRelease 宿主值上下文启动责任，属性只读一次，后台恢复被线程约束拒绝。Core 的既有直接清理自等待判断提取为内部只读资格，UIHost 在提供方 callback 内直接等待自身 Shutdown 时立即返回失败；不是通用 Task 环检测。源码兼容 C# 8，不使用新的条件表达式转换语法。
- 原生 View 观察覆盖只销毁宿主组件、销毁整个宿主层级、宿主销毁时 View 真实归还等待。退出撤销输入并确定 HostShutdown 业务结果，原生 View 实际归还后才释放提供方，重复观察不再次归还。共享提供方仍可由另一 Navigator 使用。随后补充迟到凭证尚未交付阶段，复现独立账本为零，日志 `/private/tmp/mui-host-pending-ownership-reproduction-play-20261009.log`；现退出接纳时启动提供方责任并等待导航任务，整个等待阶段持续在账本中，历史导航失败仍保留于首次宿主结果。
- 最终临时驱动 `/private/tmp/mui-host-ownership-20261009/MuiHostOwnershipObservation.cs` 的 Play Mode 通过 16 组、67 项断言，failures=0/reports=12（预期注入错误）；日志 `/private/tmp/mui-host-pending-owner-fixed-play-20261009.log`。覆盖未知同步/异步部分释放、清理历史已恢复、组件销毁后叶及外层恢复、迟到预加载、预取消 Shutdown 等待、共享后端、安全提供方叶恢复、责任属性抛错/null 的成功/失败真实归还、线程拒绝、直接自等待及三类原生 View 销毁。未知失败责任保留至进程结束，不强制销毁或重放 callback。
- 最终 macOS IL2CPP Development/High stripping 同样通过 16/67/0/12，构建 Succeeded/errors=0/warnings=0，Editor、构建及 Player 退出码均为 0。日志 `/private/tmp/mui-host-pending-owner-final-il2cpp-build-20261009.log`、`/private/tmp/mui-host-pending-owner-final-il2cpp-player-20261009.log`；产物 `/private/tmp/mui-host-ownership-il2cpp-20261009.app`。退出仍有 13 条 allocator 提示，未归因，不报告干净退出或完整内存验收。首轮 16/66 Player 早于等待阶段登记修复，不替代最终源码证据。
- 最终预加载 Play Mode 回归通过 25 组、101 项断言、reports=0，日志 `/private/tmp/mui-host-final-preload-regression-play-20261009.log`，Editor 退出码 0。Navigation 及依赖构建 0 warnings/0 errors，日志 `/private/tmp/mui-host-pending-owner-final-build-20261009.log`；612 个源码成员布局/大括号/空白检查通过，日志 `/private/tmp/mui-host-pending-owner-style-20261009.log`。新增 UIHost.ProviderCleanup meta GUID 唯一，git diff --check 通过。未新增仓库测试，未自动提交。证据仍以受控提供方与独立桌面 Unity 工程为范围；完整场景切换/持久宿主、共享后端借用期与父加载器寿命、复杂子容器及八项示例真实交互等继续保留，完整设计目标未完成。

### 2026-10-09 预加载持有权、共享消费者与快照清除

- 原生 Texture 驱动复现叶凭证显式恢复后仍占一个预留，账本还留框架的未知释放回调；日志 `/private/tmp/mui-preload-recovery-reproduction-play-20261009.log`。批次现在直接 Own 持有凭证的 PreloadReservation，公开同一叶责任，不在其外另包未知 OnDisposeAsync 回调。额度只观察已捕获的责任，确认后在准入或维护移出记录；首次清理结果和历史错误保持原值，未知部分归还不根据原生对象消失确认。
- 凭证责任属性抛错或返回 null 时，回退责任保留对象和一次真实归还入口；实际归还成功恢复容量，失败不以内部叶恢复替代未知外层确认。迟到归还失败登记到原批次，ResourceLoadException 回滚任务保留稳定未知责任。Clear 捕获接纳时已在途批次，再发起当前批次失效，避免取消回调内联完成使旧任务漏出快照；后来接纳的请求继续属于新批次。
- 共享原请求取消导致其他消费者获取失效已通过公开 API 复现；第一版 `/private/tmp/mui-preload-ownership-matrix-play-20261009.log` 仅该组失败。PreloadEntry 独立登记每个消费者，最后一个消费者取消才停止底层准备；Ready 提交与最后取消在短临界区仲裁，锁内不执行项目回调。已放弃请求的新合并观察者仍能取消自己的等待，Ready 后取消不撤销批次驻留。
- 最新 Unregister 调整在 .NET Standard 2.1 编译被拒绝；改为 DisposeAsync 后仍复现后台项目回调等待五秒时 UI 同步阻塞，日志 `/private/tmp/mui-preload-background-cancellation-reproduction-play-20261009.log`。本机 Unity unityjit-macos/mscorlib.dll 的 IL 显示 CancellationTokenRegistration.DisposeAsync 直接调用同步 Dispose，摘录保存在 `/private/tmp/mui-unity-mscorlib-20261009.il`。最终由同一消费者登记先撤销资格、发布取消信号，再执行底层取消；回调接管状态与观察者释放用原子操作仲裁，登记释放等待回调收尾，不使用 Task.Run 包装框架工作。取消回调异常保留 Navigator.PreloadCancellation 未知责任及容量，独立凭证仍归还，Clear 不误报成功。
- 临时驱动 `/private/tmp/mui-preload-ownership-20261009/MuiPreloadOwnershipObservation.cs` 的最终 Play Mode 通过 25 组、101 项断言，failures=0/reports=0；日志 `/private/tmp/mui-preload-consumer-final-play-20261009.log`。覆盖安全额度恢复、多叶分别恢复、慢归还、重复快照清除、版本切换、迟到失败、身份和责任属性故障、未知部分归还、回滚成功/失败、共享原调用及观察者分别取消、最后消费者取消、后台慢取消、有/无交付凭证的取消异常、Ready/预取消请求、普通加载失败重新获取及共享后端在宿主退出后继续使用。后台项目回调门控仍等待时，UI 约 11 ms 发布取消；迟到凭证与清除仍等待原取消收尾，未确认责任保留，不强制销毁或重放未知 callback。
- 最终 macOS IL2CPP Development/High stripping Player 同样通过 25/101/0/0，后台取消约 10 ms 发布；构建 Succeeded/errors=0/warnings=0，Editor、构建与 Player 退出码均为 0。日志 `/private/tmp/mui-preload-consumer-final-il2cpp-build-20261009.log`、`/private/tmp/mui-preload-consumer-final-il2cpp-player-20261009.log`；产物 `/private/tmp/mui-preload-ownership-il2cpp-20261009.app`。Player 退出仍有 16 条 allocator 提示，未归因，不报告干净退出或完整内存验收。此前 24 组 Player 早于已放弃请求观察者修复，不作为最终源码验收。
- TickCache 接入预加载归还维护后，既有原生缓存驱动 Play Mode 回归通过 1,085 项断言、100 次复用、configurations=107/textureReturns=106/reports=2；日志 `/private/tmp/mui-preload-final-cache-regression-play-20261009.log`，Editor 退出码 0。Navigation 及依赖构建 0 warnings/0 errors，日志 `/private/tmp/mui-preload-final-navigation-build-20261009.log`；611 个源码成员布局、大括号与空白检查通过，日志 `/private/tmp/mui-preload-final-consumer-style-20261009.log`。两个新增 meta GUID 各唯一。未新增仓库测试，未自动提交；这批受控预加载凭证证据不替代真实加载后端、宿主意外销毁、完整复杂容器交错或目标设备性能验收。

### 2026-10-09 缓存估算额度恢复与原生失效

- 真实 UGUI 驱动复现缓存归还失败后，明确支持安全重试的凭证已确认原生 View 销毁，而 ReservedCacheEstimatedBytes/FailedCacheEstimatedBytes 仍为 100/100；日志 `/private/tmp/mui-cache-budget-reproduction-play-20261009.log`。缓存内容现捕获稳定归还责任，失败估算记录仅观察该责任；确认后查询反映当前额度，下一次准入或维护移除已确认记录，重复维护不重复扣减。原始清理任务及历史错误不改写，未知部分归还不以节点消失推断成功。
- 责任属性抛错或返回空值时，缓存仍执行真实归还一次；不可安全重试的回退责任保存凭证和回调。真实归还成功即释放相应估算额度，属性错误保留于首次结果；真实归还失败保留额度，内部叶责任恢复不能代替未知外层适配器的确认。读取预算和诊断不重读项目属性、不启动工作。
- 临时驱动 `/private/tmp/mui-cache-budget-observation-20261009/MuiViewCacheObservation.cs` 在 Play Mode 通过 1,085 项断言、100 次原生 View 复用、configurations=107/textureReturns=106/reports=2；日志 `/private/tmp/mui-cache-budget-final-matrix-play-20261009.log`，Editor 退出码 0。扩展场景覆盖单项及两项失败额度分别恢复、重复诊断与维护、恢复后再接纳、未知/超额估算拒绝、在途原生淘汰不借用额度、未知后端部分归还、责任属性异常/空值及成功/失败收尾、Provider 版本变化、活动缓存代际失效、Timed 过期和缓存 View 外部销毁后剩余凭证归还。无合作声明的外层失败责任保留至 Play/进程结束，不通过额外强制销毁或重放清理消除责任。
- 最终 macOS IL2CPP Development/High stripping Player 通过相同 1,085 项断言、100 次复用及 107/106/2 汇总；日志 `/private/tmp/mui-cache-budget-final-il2cpp-build-20261009.log`、`/private/tmp/mui-cache-budget-final-il2cpp-player-20261009.log`，产物 `/private/tmp/mui-view-cache-il2cpp-20261008.app`（复用验收产物名，内容为本轮最终构建）。构建 Succeeded/errors=0/warnings=0，构建及 Player 退出码 0。退出仍有 13 条 `IL2CPP Free after allocator was destroyed` 提示与 Unity 的 Immediate MemoryLeaks 输出；这些退出指标未归因，不报告干净退出或完整内存验收。
- 本轮最终 Navigation 及依赖构建通过，0 warnings/0 errors；`python3 Tools~/format-code.py --check --braces` 检查 609 个源码文件通过，日志 `/private/tmp/mui-cache-budget-final-navigation-build-20261009.log`、`/private/tmp/mui-cache-budget-final-source-style-check-20261009.log`。Dialogs、DragDrop 和临时 CommonPatterns 项目构建亦为 0 warnings/0 errors；Common Patterns 同步当前源码后随独立 Unity 工程导入成功。未新增仓库测试，未自动提交；目标设备性能及完整缓存/预加载/加载器寿命交错不在上述代表性缓存矩阵范围内。
- 本项记录时，预加载容量仍只在首次归还成功时减记；其叶责任恢复、旧批次确认及迟到失败现由上方预加载记录补齐。缓存证据本身不证明预加载持有权与容量契约。

### 2026-10-09 参数收尾结果与全仓库源码规范

- 受控 Core 驱动复现提交许可归还失败后，候选归还成功将 `ArgsUpdateCleanup.Failed` 覆盖为 `Complete`。许可回调现在由不可安全重试的稳定 CleanupResponsibility 持有，失败登记到本次激活；候选成功仅确认自身，不改写已有失败。登记失败仍保存原错及登记错误，继续独立收尾和结果发布。提交后的参数不回滚。
- 临时驱动 `/private/tmp/mui-args-cleanup-20261008/Program.cs` 使用当前 Core 源码及单线程 SynchronizationContext，通过 11 组、104 项断言；日志 `/private/tmp/mui-args-cleanup-matrix-core-threaded-20261009.log`。覆盖立即完成、准备/提交许可接纳/提交异常、许可与候选组合清理失败、接纳后取消、忽略取消的迟到候选、慢归还及责任登记失败。未知许可与候选回调不重放，未确认责任保留。这是内部 IArgsUpdateHost 注入证据；真实 Navigator 的许可回调只归还 SemaphoreSlim，尚未证明公开业务 API 可触发该许可异常。
- 当前源码的原生操作矩阵 Play Mode 和 macOS IL2CPP Development/High stripping 均通过 40 组、586 项断言，failed=0/reports=86（预期注入故障）。日志 `/private/tmp/mui-args-final-operation-regression-play-20261008.log`、`/private/tmp/mui-args-final-operation-il2cpp-build-20261009.log`、`/private/tmp/mui-args-final-operation-il2cpp-player-20261009.log`；构建 Succeeded/errors=0/warnings=0，Editor、构建及 Player 退出码均为 0。Player 退出仍有 13 条 allocator 提示及预期未知清理故障，不报告干净退出或完整内存验收。
- 全仓库规范审查先确认 16 项成员布局和 7 处控制流大括号，再对仅包含项目源码的临时副本核对空白；另整理 6 个文件的属性及示例异常分支格式。字段及初始化器顺序保持原值，生成器仅删除空行，不变更产物语义。最终 `python3 Tools~/format-code.py --check --braces` 实际检查 609 个 C# 源文件，成员布局/人工冲突/控制流违规均为 0，空白检查通过；日志 `/private/tmp/mui-source-style-final-check-20261009.log`。最初直接对仓库根执行 folder 检查包含 ExampleProject~/Library 第三方缓存；目录形式的 include 又未展开，不能用那两次输出作为源码验收证据。
- 本轮未新增仓库测试、未自动提交。缓存版本/预算/过期、宿主销毁、复杂交错、异步焦点、多模板布局、实体输入、八项示例真实交互及性能等完成标准继续保留，不能以规范检查通过代替这些运行验收。

### 2026-10-08 View、原生节点与提供方最终归还

- 原生驱动复现正常 View 重复 Dispose 隐瞒失败，以及 Prefab 正常归还、初始化失败回滚均在未知 Element 退订失败后销毁原生根；日志 `/private/tmp/mui-view-cleanup-reproduction-play-20261008.log`。此前借用凭证保留不能证明最终控件监听已释放。
- View 在控件初始化前通过最终 LifetimeScope 登记稳定责任，父子及容器仍各自遵守 Element 扫描边界。最终清理撤销视图资格，已有子激活和资源激活未确认时保留控件引用，等待原拥有者排空；初始化重入及初始化中结束后继续宣告成功均拒绝。重复 Dispose/DisposeAsync 保持首次结果，显式恢复只确认已登记责任。原生节点拥有者另以 ViewHierarchyCleanup 捕获各 View 的最终责任，不重新扫描或重复初始化 Element。
- Prefab 实例归还和创建回滚分别保留稳定责任，隐藏、最终 View 归还、销毁请求及实际 Destroy 完成分阶段处理。恢复不重复未知原生步骤；原生实例实际消失后才报告归还。提供方 staging 有未确认回滚节点时保留根，退出不再绕过节点保护。PrefabViewProvider 提供可等待的 DisposeAsync；同步 Dispose 保留启动并观察原生兜底的语义。CreateStagingCleanup 为项目提供方提供同一公开责任入口。
- 普通/虚拟列表最终节点也确认包装 Element 与内部 View 的最终监听；列表最终回调以已有 LifetimeScope 保留各节点确认状态，一项失败仍继续独立节点。显式恢复只检查依赖，不重新退订来源或重复原生收尾。Resource Integration 示例的正常凭证、常驻创建和异步加载失败回滚均持有稳定责任；后端引用仅在实例实际归还后尝试扣减一次，未知部分归还错误不重复扣减。独立工程同步导入后的两份示例源码并给临时驱动显式引用可选程序集。
- 首轮 IL2CPP 暴露 View 的过滤 catch 重抛被后续 catch 吞掉：日志 `/private/tmp/mui-view-provider-result-reproduction-il2cpp-player-20261008.log` 记录 first=null/repeated=null/alive=False/state=Completed。本地生成的 MUI.UGUI__5.cpp 确认重抛被外围生成的后续 catch 捕获。改为单一 catch 内判断进入尝试前的确认状态后，实际 Player 记录首次和重复 AggregateException、state=Failed。首轮构建成功不能证明该运行路径正确；其他异常过滤路径仍随相应完整矩阵核对。
- 最终 Play Mode 与 macOS IL2CPP Development/High stripping 均通过 13 组、60 项断言，reports=2（预期加载回滚故障）。覆盖直接 View 未知错误、正常/初始化失败 Prefab 保留、安全叶恢复、父子边界、常驻及加载提供方退出时的 staging 保留、加载引用延后归还，以及未知后端部分归还不重复扣减。日志 `/private/tmp/mui-view-provider-final-fixed-play-20261008.log`、`/private/tmp/mui-view-provider-final-fixed-il2cpp-build-20261008.log`、`/private/tmp/mui-view-provider-final-fixed-il2cpp-player-20261008.log`；产物 `/private/tmp/mui-view-cleanup-il2cpp-20261008.app`。最终构建 Succeeded/errors=0/warnings=0，构建与 Player 退出码均为 0。
- 列表最终未知监听保留及安全恢复新增四组，Play Mode 通过 47 组、637 项断言、reports=46（预期注入故障），日志 `/private/tmp/mui-view-final-list-cleanup-play-20261008.log`；最终修正后 IL2CPP Player 同样通过 47/637/46。构建 `/private/tmp/mui-view-final-list-fixed-il2cpp-build-20261008.log` Succeeded/errors=0/warnings=0，运行 `/private/tmp/mui-view-final-list-fixed-il2cpp-player-20261008.log`，构建和 Player 退出码均为 0，产物 `/private/tmp/mui-list-candidate-node-il2cpp-20261008.app`。未确认原生节点及后端凭证保留至 Play/进程终止，不通过主动销毁清空责任。
- 两类最终 Player 退出仍各有 13 条 `IL2CPP Free after allocator was destroyed` 提示，且进程结束时输出预期未知清理故障；不报告干净退出或完整内存验收通过。最终 Navigation 及依赖离线构建 0 warnings/0 errors；当前 21 个改动 C# 文件成员布局、大括号和空白检查无违规，六个新增 meta GUID 唯一。未新增仓库测试，未自动提交。
- 最终源码下现有缓存 Play Mode 回归通过 100 次 View 复用、1,011 项断言，configurations=107/textureReturns=106/reports=2；日志 `/private/tmp/mui-view-final-cache-regression-play-20261008.log`。原七组父子换绑回归通过、reports=2，日志 `/private/tmp/mui-view-final-nested-regression-play-20261008.log`。两次 Editor 退出码均为 0；缓存版本、预算与过期不在该百次复用证据内。
- 本轮证明上述最终节点和后端依赖归还路径；缓存版本/预算/过期、宿主主动销毁及复杂交错、完整布局多模板和异步焦点、八项示例真实交互、目标设备性能及全仓库规范仍未完整验收，目标继续保留。

### 2026-10-08 自定义 Element 属性与最终清理

- 核对 main 与桌面 FUI 的自定义控件入口后，发现目标第 2 节要求的 CreateProperty、Track、TrackCleanup 缺失；原 Element 清理抛错仍清空回调，重复 Dispose 静默返回。真实 UGUI 驱动在正常释放与初始化失败回滚两条路径均复现责任丢失，日志 `/private/tmp/mui-element-cleanup-reproduction-play-20261008.log`。
- Element 的属性、同步资源及原生监听复用最终 LifetimeScope，提供稳定 CleanupResponsibility，不新增清理调度器。初次逆序释放并继续独立项，未知失败保留回调；容器显式恢复只确认叶责任，不重放未知副作用。重复同步/异步释放保持首次结果，同步入口仅读取已完成任务而不阻塞主线程。初始化重入拒绝，初始化中结束控件后不宣告成功；已确认恢复的控件在 OnDestroy 中不重复报告历史失败。
- ElementProperty 使用公开属性通知名，初值不调用原生赋值，相等值不通知；比较器执行后再次检查活性，属性回调结束控件后不发送迟到通知。最终释放解除值、比较器和回调引用。Track 仅登记成功时接管同步资源并拒绝重复身份，晚登记不转移持有权。Documentation~/index.md 补充自定义 Slider、生成双向绑定、输入门控及最终资源与激活资源的登记说明。
- 最终 Play Mode 通过 9 组、38 项断言，reports=0；日志 `/private/tmp/mui-element-property-initialization-final-play-20261008.log`。覆盖未知失败保留、明确幂等叶责任恢复、重复释放结果、线程拒绝、属性/比较器重入、初始化递归与初始化内结束。真实 Slider 通过包内生成器及 Navigator/BorrowedViewProvider 验证模型投影、原生事件反向更新、门控不积压、换绑退订和最终资源释放；此证据不证明物理鼠标/键盘或输入法行为。两项未知故障根节点在退出 Play 时仍由默认错误出口报告，不能称无错误退出。
- macOS IL2CPP Development/High stripping Player 同样通过 9 组、38 项断言，reports=0；构建 Succeeded/errors=0/warnings=0，构建及 Player 退出码为 0。日志 `/private/tmp/mui-element-property-final-il2cpp-build-20261008.log` 与 `/private/tmp/mui-element-property-final-il2cpp-player-20261008.log`，产物 `/private/tmp/mui-element-cleanup-il2cpp-20261008.app`。退出仍报告两项预期未知清理故障及 13 条 `IL2CPP Free after allocator was destroyed` 提示，不报告干净退出或完整内存验收。
- 本轮最终基类下现有列表 Play Mode 回归通过 43 组、593 项断言，reports=46（预期注入故障），日志 `/private/tmp/mui-element-list-regression-play-20261008.log`。Navigation 及依赖离线构建 0 warnings/0 errors；当前 10 个改动 C# 文件成员布局、大括号及空白检查无违规，新 meta GUID 唯一。未新增仓库测试，未自动提交。
- 本项记录时外层 View 最终清理尚未提供稳定责任；相关 Prefab 提前销毁及列表最终 Element 失败路径已由上方“View、原生节点与提供方最终归还”记录的后续实现及验收补齐。本项 Element 责任本身不证明外层路径安全。完整缓存、异步焦点、布局多模板、八项示例真实交互、性能和全仓库规范验收继续保留，完整目标未完成。

### 2026-10-08 列表候选节点保留与恢复

- 扩展真实 UGUI 驱动，在隐藏候选的命令退订回调中注入未知失败，观察下一帧的候选 View 与包装节点，复现普通/虚拟列表均提前销毁节点；日志 `/private/tmp/mui-list-candidate-node-reproduction-play-2-20261008.log`。此前仅保留父 View 与资源责任的断言没有覆盖该原生节点缺口。
- NestedViewElement 通过既有 DelegateViewProvider 记录 BorrowedViewProvider 交付的稳定归还责任；已确认记录可移除，旧失败不能被后来的借用覆盖。最终节点收尾先确认所有已有借用已归还；开始收尾后拒绝参数赋值、换绑及新子激活。节点责任只允许重新检查依赖，原生收尾自身失败时保留节点和回调，不重放未知副作用。框架同步收尾只读取已完成任务，不阻塞主线程。新 NodeCleanup 独立文件及 meta 已提供，GUID 唯一。
- 普通/虚拟列表准备对象公开稳定 CleanupResponsibility，内部准备失败也登记到父激活。初次清理继续排空独立条目；显式恢复只确认子视图凭证和最终节点责任，不再次解绑或改写原任务。收尾后解除来源、模型、选择及测量快照引用。固定槽位在首次收尾时捕获节点所有权，集合清空后仍能销毁容器创建的模板实例；借用节点保持原所有权。
- 最终 Play Mode 驱动 `/private/tmp/mui-list-rebind-20261008/MuiListRebindObservation.cs` 通过 43 组、593 项断言，reports=46（预期注入故障）；日志 `/private/tmp/mui-list-candidate-node-guard-final-play-20261008.log`。覆盖原 37 组，并增加两类列表的未知退订节点保留、提前重试不重复回调且不能取得新内容、慢归还时节点及父清理继续等待，以及固定借用槽位和模板挂点的换绑、空槽与最终所有权。已有部分资源归还失败场景另确认恢复前隐藏节点存活，叶责任及容器逐层确认后节点确实销毁。未知失败根节点保留至 Play/进程终止，不人为销毁来清空账本。
- macOS IL2CPP Development/High stripping Player 通过相同 43 组、593 项断言、reports=46；最终构建 Succeeded/errors=0/warnings=0，构建与 Player 退出码均为 0。日志为 `/private/tmp/mui-list-candidate-node-guard-final-il2cpp-build-20261008.log`、`/private/tmp/mui-list-candidate-node-guard-final-il2cpp-player-20261008.log`，产物 `/private/tmp/mui-list-candidate-node-il2cpp-20261008.app`。退出仍有 13 条 `IL2CPP Free after allocator was destroyed` 提示，不报告干净退出或完整内存验收通过。
- 最终源码下原七组父子换绑 Play Mode 回归通过，reports=2；日志 `/private/tmp/mui-list-candidate-node-guard-final-nested-play-20261008.log`。离线 Navigation 及依赖构建 0 warnings/0 errors；七个改动 C# 文件成员布局、控制流大括号和空白检查均无违规。未新增仓库测试，未自动提交。
- 本项证明上述节点及候选责任路径；完整缓存版本/预算/过期、宿主销毁、复杂多模板与动态测量交错、异步焦点、实体输入、八项示例及全仓库规范验收继续保留。下一步另核对自定义 Element 同步清理异常时的回调持有，不以本项节点保留代替全部控件清理契约。

### 2026-10-08 操作候选清理与子视图诊断归属

- 真实 UGUI 驱动先复现页面/子视图的参数更新与 Presenter 换绑共四条路径：候选清理失败只保存异常，父子 View 仍提前归还；失败日志 `/private/tmp/mui-operation-cleanup-reproduction-play-2-20261008.log`。统一释放入口现捕获稳定责任、从实际资源入口释放，并把未确认责任登记到所属激活。责任属性抛错或返回空值也继续真实清理一次；登记与清理双失败保留两项异常及不可安全重试的回退责任。显式恢复不重复未知回调，也不改写首次失败结果。
- 子视图作用域保存父页面诊断值快照，准备、公开参数更新、公开及内部换绑、生命周期回调恢复所属身份；异常传播前附加该上下文。验收向公开操作和子视图准备注入其他随机宿主上下文，确认候选准备及释放仍归属于原页面，且释放阶段正确。
- 临时驱动 `/private/tmp/mui-operation-cleanup-20261008/MuiOperationCleanupObservation.cs` 在 Play Mode 通过 40 组、586 项断言，failed=0/reports=86（预期注入故障）；日志 `/private/tmp/mui-operation-cleanup-registration-play-20261008.log`。四条路径均覆盖同步完成、未知责任、安全恢复、迟到取消、慢清理、提交失败、责任属性异常后的成功及失败收尾；未知失败根节点保留至 Play/进程终止。
- macOS IL2CPP Development/High stripping Player 通过相同 40 组、586 项断言、reports=86；构建 Succeeded/errors=0/warnings=0，构建及 Player 退出码均为 0。日志分别为 `/private/tmp/mui-operation-cleanup-final-il2cpp-build-20261008.log` 与 `/private/tmp/mui-operation-cleanup-final-il2cpp-player-20261008.log`。退出仍有 13 条 `IL2CPP Free after allocator was destroyed` 提示，不报告干净退出或完整内存验收通过。
- 当前源码下列表换绑原生回归通过 37 组、509 项断言、reports=30；日志 `/private/tmp/mui-operation-cleanup-list-regression-play-20261008.log`。离线 Navigation 及依赖构建 0 warnings/0 errors；12 个改动 C# 文件成员布局、大括号及空白检查 0 违规。BindingBuilder 的集合引用曾被误删，已恢复，当前原生编译及 Play/AOT 验证包含该修正。未新增仓库测试。
- 列表候选节点清理失败后的节点保留由上方“列表候选节点保留与恢复”记录的后续实现及验收补齐；多模板、动态测量、缓存版本/预算/过期、宿主销毁、异步焦点、实体输入、八项示例及全仓库规范审查仍待完整验收。

### 2026-10-08 换绑准备清理责任

- 扩展列表验收先通过 31 组资源、布局与大规模来源场景，随后复现自定义 IPreparedBindingTarget 回滚释放抛错后，框架只保存异常却仍归还父 View 的缺口；失败日志 `/private/tmp/mui-list-rebind-unknown-play-20261008.log`。BindingPreview 原先直接调用候选 DisposeAsync 后丢弃引用，没有登记稳定责任。
- BindingPreview 已从 BindingBuilder 拆为独立文件，候选登记时捕获稳定目标责任，逆序释放且继续处理独立项；未知失败由账本保留对象/回调。预览容器提供自己的稳定责任，初次释放实际执行回调，显式重试只确认已登记叶责任；不会重复未知项目清理。失败同步记入本次激活的确认依赖，阻止父 View 提前归还。成功后清除候选、读取检查与目标值快照。自定义 BindingContext 返回的准备对象也由驱动器托管并登记到激活，首次换绑/关闭失败不因后续确认而改写。
- 最终临时原生 UGUI 驱动 `/private/tmp/mui-list-rebind-20261008/MuiListRebindObservation.cs` 通过 37 组、509 项断言，reports=30（预期注入故障）；日志 `/private/tmp/mui-list-rebind-external-preparation-play-20261008.log`。除原 24 组外，覆盖普通/虚拟列表候选资源部分归还、未知准备目标失败、有明确幂等责任的目标恢复、外部 BindingContext 准备对象清理失败，以及虚拟列表准备期间视口/列数变化和 100/1,000/10,000 条来源换绑。未知失败场景在观察结束后继续保留原生根节点，直至进程或 Play Mode 终止，不人为销毁节点来清空责任。
- 大规模场景使用 600×240 局部 Canvas 单位视口、60 固定条目尺寸、0 预留行、capacity=16；本地换绑在调用帧完成，每次已提交来源物化和可输入条目均为 4。先后 3 条初始来源及两批大来源只取得 11 个纹理凭证，延迟首项准备时不启动后续项，不为验证精确位置物化全部数据。该项验证固定尺寸单模板换绑范围，不证明动态测量、多模板或滚动性能预算。
- 最终 macOS IL2CPP Development/High stripping Player 通过相同 37 组、509 项断言、reports=30；构建 Succeeded/errors=0/warnings=0，Player 退出码 0。构建日志 `/private/tmp/mui-list-rebind-preparation-final-il2cpp-build-20261008.log`，运行日志 `/private/tmp/mui-list-rebind-preparation-final-il2cpp-player-20261008.log`，产物 `/private/tmp/mui-list-rebind-il2cpp-20261008.app`。退出仍有 13 条 `IL2CPP Free after allocator was destroyed` 提示，不报告干净退出或完整内存验收通过。
- 最新核心行为修改下原七组父子换绑回归通过，reports=2；日志 `/private/tmp/mui-prepared-target-rebind-regression-play-20261008.log`。最终离线 Navigation 及依赖构建 0 warnings/0 errors；四个 C# 文件成员布局、控制流大括号及空白检查 0 违规，新 BindingPreview 配套 meta 已提供。未新增仓库测试，未自动提交。
- 本轮审查发现 Presenter 换绑准备对象与参数更新候选的释放入口直接调用 DisposeAsync，仅保存失败结果；该缺口已由上方“操作候选清理与子视图诊断归属”记录的后续实现及验收补齐。完整目标验收继续保留。

### 2026-10-08 普通与虚拟列表换绑

- 换绑提交前的来源、布局或资格失效通过取消结果返回，保留旧绑定；不再因内部取消未触发显式令牌而额外记录异常。已开始解绑或候选清理失败仍按故障处理。
- 临时原生 UGUI 驱动 `/private/tmp/mui-list-rebind-20261008/MuiListRebindObservation.cs` 在 RecyclingList 与 VirtualList 各覆盖 12 组场景：本地换源、准备失败后重试、忽略取消的迟到结果、其他页面关闭、父关闭、候选来源变化、旧来源变化、回调重入拒绝、旧异步命令排空、父关闭排空命令、提交失败，以及资源部分归还后的显式恢复。Unity Play Mode 全部通过，cases=24、assertions=249、reports=10；日志 `/private/tmp/mui-list-rebind-commands-play-20261008.log`。报告均为预期注入故障，来源变化取消明确断言无新增异常、结果 Error 为空。
- 当前源码的 Navigation 及其依赖离线构建通过，0 warnings/0 errors。本批列表矩阵与取消诊断修复尚未进行 IL2CPP 验收；候选回滚清理故障、大规模虚拟列表、布局变化、多模板与实体输入矩阵仍待完成，不代表完整设计目标已验收。

### 2026-10-08 子容器释放入口与多层依赖

- 直接释放公开 ChildViewScope.CleanupResponsibility 原先会实际完成归还，但没有发布容器的 IsDisposed/CleanupCompletion。本轮统一两种入口，由 CleanupResponsibility 在实际状态确定后同步通知框架容器，发布其首次结果，不重新进入自己的责任或额外加入异步等待；通知只更新框架状态，不执行项目代码。两种入口均检查子生命周期/命令自等待，直接责任释放与直接重试也同步完成容器任务；合法显式恢复只更新当前确认，IsDisposedSuccessfully 直接读取首次共享任务状态，不被后续恢复改写。
- 临时驱动 `/private/tmp/mui-nested-confirmation-20261008/MuiNestedCleanupVerification.cs` 使用公开 IView/IChildViewHost 协议构造 Root→A→B→C 与独立 Sibling，验证叶模型、叶 View 部分归还失败后兄弟项正常归还、所有祖先继续保留、过早外层重试不重复后端、逐层显式确认及历史任务不变。两个可恢复场景最终 View 获取/实际归还均为 5/5，责任回到各自基线；未知模型保持不可重试并保留责任。还覆盖直接责任释放状态、子生命周期中的两种自等待拒绝及其显式恢复。该核心驱动使用协议实现，不证明完整原生 UGUI 树或物理输入。
- 同一批的 `/private/tmp/mui-nested-confirmation-20261008/MuiBindingCleanupVerification.cs` 覆盖外部 BindingContext 同步抛错、任务失败、延迟失败/成功，及框架泛型会话的路径退订和最终清理故障。延迟解绑完成前持有 View；未知失败后父责任和 View 责任重试均不能提前归还，不重复调用外部解绑/退订/最终清理，也不读取项目 State/CleanupCompletion getter 猜测确认。未知失败保留责任，不把整批责任清零作为验收结果。
- 最终离线与 Unity Play Mode 均通过多层清理 126 项断言、解绑 6 类 54 项断言；日志 `/private/tmp/mui-nested-cleanup-final-core-20261008.log`、`/private/tmp/mui-nested-cleanup-final-play-20261008.log`。断言明确要求同步直接责任归还后容器任务立即完成，并覆盖直接责任自等待拒绝后、尚未调用普通容器释放的显式恢复，不靠额外等待掩盖状态差异。
- 中途原生缓存回归发现新的完成观察使本地清理多等待一次，保留失败日志 `/private/tmp/mui-child-scope-cache-regression-play-20261008.log`；最终同步通知实现下原驱动未经修改通过全部 1011 项断言，cycles=100、configurations=107、textureReturns=106、reports=2、frame=21，百次本地开关仍在调用帧完成。日志 `/private/tmp/mui-child-scope-cache-final-play-20261008.log`。不沿用修正前的 IL2CPP 结果证明当前完成点。
- 最终原生资源/模型依赖清理驱动通过 515 项断言与百次开关，View 获取/归还 100/100、textureReturns=100、frame=1；日志 `/private/tmp/mui-child-scope-dependencies-final-play-20261008.log`。原七组父子换绑驱动全部通过，预期注入故障 reports=2，日志 `/private/tmp/mui-child-scope-rebind-final-play-20261008.log`；Core 原有 50 项清理责任断言也通过，日志 `/private/tmp/mui-child-scope-core-final-regression-20261008.log`。
- 最终 macOS IL2CPP Development/High stripping 的多层清理 Player 通过相同 126 项及解绑 54 项断言；构建 Succeeded/errors=0/warnings=0，Player 退出码 0。日志 `/private/tmp/mui-nested-cleanup-final-il2cpp-build-20261008.log`、`/private/tmp/mui-nested-cleanup-final-il2cpp-player-20261008.log`，产物 `/private/tmp/mui-nested-cleanup-il2cpp-20261008.app`。未知解绑及模型失败按设计继续保留责任，该协议驱动不证明全部原生容器退出已清零。
- 当前源码的原生缓存 IL2CPP Player 也通过全部 1011 项断言，cycles=100、configurations=107、textureReturns=106、reports=2、frame=21；构建 Succeeded/errors=0/warnings=0，Player 退出码 0。日志 `/private/tmp/mui-child-scope-cache-final-il2cpp-build-20261008.log`、`/private/tmp/mui-child-scope-cache-final-il2cpp-player-20261008.log`，产物 `/private/tmp/mui-view-cache-il2cpp-20261008.app`。两套最终 Player 退出仍各有 13 条 `IL2CPP Free after allocator was destroyed` 提示，不报告干净退出或完整内存验收通过。
- 本轮两个 C# 文件通过成员布局、控制流大括号及空白检查，0 个违规，git diff --check 通过；未新增仓库测试，未自动提交。原生复杂容器、完整列表交错、实体输入与最终全部目标验收继续保留。

### 2026-10-08 子容器当前清理确认

- ChildViewScope 接入稳定 CleanupResponsibility，首次清理排空全部子项；后续显式重试只检查依赖，不重新执行生命周期或未知后端回调。子句柄保存实例、激活和 View 凭证的稳定确认依赖，历史失败任务仍保留原值。
- 激活作用域同时等待绑定会话真实解绑。框架会话按订阅、最终清理与命令作用域状态确认；外部 BindingContext 按实际解绑任务确认，不查询项目状态属性推测成功。未知退订及最终清理故障继续阻止确认。
- 原父子换绑 Play Mode 驱动重新通过全部 7 组，预期两项注入故障各报告一次，包含提交失败后顶层 View 实际归还；日志 `/private/tmp/mui-child-confirmation-rebind-play-20261008.log`。多层叶责任恢复、解绑故障矩阵及此次修改后的 IL2CPP 验收尚未完成；下述缓存 IL2CPP 产物早于本次子容器修改。

### 2026-10-08 页面缓存所有权拆分

- CachedViewContent 仅保存 View 取得凭证与缓存/绑定/Provider 代际，不持有 ViewModel、Presenter 或旧实例作用域。关闭先结束激活和解绑，再执行 Presenter 销毁及实例资源清理；全部确认后重置 View 并移交唯一凭证。缓存命中建立新 ViewContent、模型和 Presenter，外部传入模型继续只借用。移交拒绝或重置失败沿用同一凭证归还流程，不重复执行业务实例清理。
- 新增 ICacheableView 与 ICacheableViewAcquisition，前者提供旧渲染状态重置，后者显式保证后端依赖能延长到实际归还并提供重新配置入口。AcquiredView 默认不支持缓存，不能延长依赖的适配器保持直接归还；PrefabViewFactory 的归还独立持有原生根对象，显式支持移交。Prefab 缓存命中执行当前配置回调，不沿用旧活动加载器。IReusableViewPresenter 保留兼容声明但已弃用，示例不再用该标记决定缓存资格。
- UGUI 重置清除自身及静态/池化子 View 的焦点、旧输入阻挡、局部返回、模态屏障、已排空的子作用域和资源加载器，维持隐藏且不可交互。DynamicViewElement 在父激活清理时解除来源、模型、子槽及借用 Provider 引用；列表沿用已有激活引用清理协议。缓存中被外部销毁的 View 在维护扫描或命中时淘汰。并行淘汰的清空等待覆盖全部在途归还，不把已结束的历史淘汰无限串成任务链。
- 临时驱动 `/private/tmp/MuiViewCacheObservation.cs` 在独立桌面工程完成 100 次真实 Prefab View 复用：每次创建新模型/Presenter/句柄，旧订阅回到 0、模型归还一次、Presenter 销毁和实例资源归还各一次，旧句柄和旧模型通知不影响新激活；首批 100 次本地打开关闭均在调用帧完成，纹理凭证每次实际归还，View 由缓存唯一持有，清空等待其原生销毁。另覆盖延迟实例清理阻止入缓存、新页面取得独立 View、缓存配置失败淘汰、借用模型不释放、未声明凭证能力不入缓存及 ResetForCache 失败后实际归还。该基线不替代进程内存或全部子容器矩阵。
- 最终 Play Mode 与 macOS IL2CPP Development/High stripping Player 均通过 1011 项断言，包含自定义 Element 100 次入缓存解除旧模型引用，以及 CachePrepare/CacheReset 原始诊断阶段，cycles=100、configurations=107、textureReturns=106、预期故障 reports=2、最终 frame=21。日志 `/private/tmp/mui-view-cache-play-20261008.log` 与 `/private/tmp/mui-view-cache-il2cpp-player-20261008.log`；最终构建 Succeeded/errors=0/warnings=0，日志 `/private/tmp/mui-view-cache-il2cpp-build-20261008.log`，产物 `/private/tmp/mui-view-cache-il2cpp-20261008.app`，Player 退出码 0。退出仍有 13 条 allocator 提示，不报告干净退出或全部退出矩阵完成。
- 修改后的 Navigation 示例离线编译为 0 错误、0 警告，连同 Core/Resources/Navigation/ChildViews/UGUI 与资源后端示例依赖均通过；真实 Sample 画面与鼠标/键盘输入仍待验收。临时驱动位于 `/private/tmp` 与独立验收工程，没有新增仓库测试或自动提交。
- 自定义 UGUI Element 可覆盖 OnResetForCache 清除项目控件保存的模型、参数和活动引用；钩子失败沿用 View 淘汰，不能在重置期间启动新工作。DynamicViewElement 同时恢复 ShowUnbound 默认值，避免把旧激活的显示选项带入未绑定的新内容。
- 本轮 19 个新增或修改的 C# 文件在临时副本通过成员布局、控制流大括号和空白检查，0 个违规；Runtime/Editor/Samples/Analyzers/docs 的 728 个 meta 无无效或重复 GUID，无缺失脚本 meta。全仓库 CodeStyle 检查仍有其他未修改的 16 个文件需调整成员布局、7 处控制流缺少大括号，留待最终规范审计，不声称全仓库规范已通过。
- 资源/Legacy/TMP 转换回归通过，资源 created=7/released=7，两项注入故障各报告一次；日志 `/private/tmp/mui-cache-regression-contracts-play-20261008.log`。此前父子换绑回归的提交故障组暴露 ChildViewScope 当前确认与历史失败未拆分，失败日志 `/private/tmp/mui-cache-regression-rebind-play-20261008.log`；上述子容器修改后原驱动七组全部通过，保留失败记录用于追溯。

### 2026-10-08 清理依赖与百次开关基线

- LifetimeScope 增加 IsCleanupConfirmed，首次 DisposeAsync 结果仍共享且不变。失败登记保留对应责任的状态读取；安全重试后只确认真实已完成的责任，不重新执行父作用域清理。未跟踪的操作回滚及取消回调错误继续按未知处理。Own 在登记时取得稳定记录，确认查询不重新调用项目的责任属性。
- ViewModelOwnership 原先在调用模型释放前清空持有引用，模型抛错会失去实际持有者。本轮保存 ownedModel 与释放记录直到确认，工厂模型及退役模型的释放接入账本；只实现同步或普通异步释放的模型仍不可自动重试。外层显式重试只核对已处理的叶责任并清除剩余引用，不重复调用未知模型。外部借用模型保持借用。
- ResourceSlot 的清空回调登记为内部幂等步骤，完成清空后不重复执行原生 setter。资源归还故障把同一凭证责任交给作用域确认；ElementResourceOwner 使用自身责任，槽的叶责任确认后才解除剩余控件持有。首次槽错误及计数保持历史原值。后端回滚没有可查询责任时仍按未知保留，不由此推断归还完成。
- 顶层及子视图的最终 View 凭证归还检查实例与激活作用域；依赖不明则保留 ViewInstance.DependencyRelease 到独立账本，不提前交回池或销毁。显式重试检查依赖及原凭证状态，不自行重试后端。Navigator 清理容量补计移出 entries 后仍有责任的页面，同页多个记录合并为一个名额；无页面身份记录逐项保守计入。
- 临时 Core 驱动通过 50 项断言，追加父子作用域失败后叶责任重试确认、父首错任务不变、未知回滚/取消回调不能确认，以及正常释放只执行一次。日志 `/private/tmp/mui-cleanup-dependencies-core-20261008.log`，入口仍为 `/private/tmp/mui-cleanup-acceptance-20261008/Program.cs`，没有新增仓库测试。
- 提交前修正 Own 的条件 lambda 写法以兼容 C# 8；最终源码的 Core 50 项断言再次通过，MUI.UGUI 离线构建及其 Core/Resources/Navigation/ChildViews 依赖均为 0 错误、0 警告。此前同一清理场景的 macOS IL2CPP 构建成功、0 错误、0 警告，日志 `/private/tmp/mui-dependency-cleanup-il2cpp-build-20261008.log`；随后执行该产物通过 515 项断言与 100 次开关、获取/归还 100/100、frame=1，退出码 0，日志 `/private/tmp/mui-dependency-cleanup-il2cpp-player-20261008.log`。退出有 13 条 allocator 提示，不报告干净退出；该产物构建早于缓存重构，不作为新缓存行为证据。
- 独立桌面工程的 Play Mode 通过 515 项断言，涵盖纹理/模型部分归还失败、父视图持续保留、过早外层重试拒绝、叶责任与外层持有者逐层显式确认、后端实际归还仅一次、原关闭失败不变，以及清理容量为 1 的拒绝与恢复。日志 `/private/tmp/mui-dependency-cleanup-play-20261008.log`，入口 `/private/tmp/MuiDependencyCleanupObservation.cs`。
- 同一场景完成 100 次真实 View 创建、强制关闭与 WaitForCleanupAsync：每次路径订阅从 1 回到 0，活动实例/在途请求/超时清理/未确认责任及纹理凭证均回到 0，View 获取/归还 100/100、纹理归还 100 次，模型均归还一次；退役模型通知不写 UI。所有本地开关在调用帧完成，随后检查原生 View 对象均已销毁。该循环不启用缓存，不作为进程内存、全部业务订阅或复杂嵌套容器的完整基线证据。
- 缓存源码审计发现仍保留旧模型与 Presenter，记录为当前设计冲突。缓存启用/清空后的百次基线、普通/虚拟列表的完整换绑和依赖清理矩阵、故障父子容器确认、实体输入及全部示例视觉验收仍未完成；完整目标保持进行中，本轮未自动提交。

### 2026-10-08 独立通知诊断与宿主清理账本

- BindingSession 保存导航准备时的值身份；前向、反向、路径拥有者通知、命令刷新、首次来源提交、就绪、退订及最终清理附加自己的阶段。同一页面的调用链保留当前导航操作名，独立通知使用 Binding/Command/Resource，其他宿主不能替换归属或操作。异步命令在 Execute 调用链保存身份；绑定故障保留传播且按原异常去重报告，不把转换拒绝变成故障。保存的上下文只包含有界字符串和值，不另外维护 View 所有权映射。
- ViewResourceContext 保存激活身份，晚于导航的首次资源键配置沿用该值；资源槽分别为 Load、Assignment、Release 和 Rollback 提供阶段，资源拥有者的通知使用 Notification。加载取消更新 Cancelled 且不写异常日志。原生 setter 的不确定结果继续冻结槽并保留新旧凭证，不以诊断接入改变归还规则。
- CleanupResponsibility 构造时保存备用身份；首次归还在有明确宿主时使用实际归还所有者，支持合法移交及缓存接管，宿主级 ViewId=0 不混入原页面编号。账本发布后身份稳定，重试不能改写它。CleanupRegistry 增加宿主计数和有界采集；Guid.Empty 只匹配无宿主责任。计数与采集集合在同一次账本锁内选择，逐项快照在锁外读取，避免账本锁/责任锁互相等待。
- NavigationSnapshot 增加 HostId、UnconfirmedCleanupCount、CleanupResponsibilities 和截断提示；HasCleanupFailure 明确表示历史失败，原 HasUnconfirmedCleanup 沿用其历史语义。UIHost Inspector 展示数量、责任 Id/拥有者/状态/尝试/重试资格/位置/错误。该 Inspector 接入已通过 Unity 编译，尚不作为面板视觉和实体操作验收证据。
- 临时 Core 驱动通过 40 项断言，追加所有权移交后的宿主/句柄/操作/阶段、没有当前上下文时的登记身份回退、宿主筛选、重试归属稳定及调用链恢复。入口 `/private/tmp/mui-cleanup-acceptance-20261008/Program.cs`，日志 `/private/tmp/mui-cleanup-diagnostics-acceptance-20261008.log`；未新增仓库测试。
- 独立桌面工程的 Play Mode 通过 54 项断言、13 个注入故障各报告一次，frame=3；关闭追踪未启用。覆盖导航完成后在另一宿主上下文内触发绑定、转换/路径 getter/反向 setter/CanExecute 故障、跨异步命令、延后首次资源配置、通知/加载/回滚/归还故障、加载取消不报告、宿主隔离、有界快照、部分归还重试及历史关闭结果保留。原生 RawImage dirty callback 抛错后，新旧凭证保留至目标清空，各归还一次且最终责任清零。入口 `/private/tmp/MuiLiveDiagnosticsObservation.cs`，日志 `/private/tmp/mui-live-diagnostics-play-20261008.log`。该驱动使用原生 UnityEvent 与属性调用，不证明物理输入或全部 Samples 视觉验收。
- 同一场景的 macOS IL2CPP Development/High stripping 构建 Succeeded/errors=0/warnings=0；Player 通过相同 54 项断言、13 项故障单次报告，frame=3，退出码 0。产物 `/private/tmp/mui-live-diagnostics-il2cpp-20261008.app`，日志 `/private/tmp/mui-live-diagnostics-il2cpp-build-20261008.log` 和 `/private/tmp/mui-live-diagnostics-il2cpp-player-20261008.log`。退出仍有 13 条 allocator 提示，不报告干净退出或全部生命周期验收完成。
- 最新代码的 Legacy/TMP 嵌套路径 Play Mode 回归通过 38 项断言、frame=1；日志 `/private/tmp/mui-diagnostics-regression-paths-play-20261008.log`。本轮新增上下文没有使本地绑定人为延后一帧。
- 既有七组父页/静态子视图换绑回归均通过，预期两项注入故障各报告一次；日志 `/private/tmp/mui-diagnostics-regression-rebind-play-20261008.log`。该证据不覆盖普通/虚拟列表的完整 staged rebind 交错矩阵。
- 既有资源/Legacy/TMP 转换校验回归通过，资源凭证 created=7/released=7，两项注入故障各报告一次；日志 `/private/tmp/mui-diagnostics-regression-contracts-play-20261008.log`。
- 最终 20 个修改的 C# 文件在临时副本通过成员布局、大括号及空白检查，0 个违规项；Runtime/Editor/Samples/Analyzers/docs 的 724 个 meta 无无效或重复 GUID，无缺失脚本 meta。上述检查不替代运行验收。
- 保留完整目标：复杂父子容器的失败/重入/换绑/依赖清理矩阵、未确认责任对容量的影响、100 次打开关闭回基线、最终全部列表/焦点/输入和示例验收仍待完成。未知外层清理回调不能仅因某个子凭证重试成功而被确认；当前责任计数为零也不改写历史失败。修改未自动提交。

### 2026-10-08 清理责任与显式幂等重试

- Core 新增 `CleanupResponsibility`、只读状态快照、`ICleanupResponsibilitySource` 和独立于宿主组件的 `CleanupRegistry`。记录稳定 Id、拥有者标签、有界上下文、当前状态/错误及饱和尝试次数。账本只持有在途或失败的真实责任，成功后移除；快照按 1–4096 上限采集，不自动重试或删除未知责任。
- AcquiredResource/AcquiredView/AcquiredPreload 改为使用同一 Core 责任。首次尝试失败后继续保留资产与释放回调；`supportsIdempotentRetry` 默认 false，只有后端显式声明完整回调可幂等重试时才允许再次调用。并发 DisposeAsync 共享首次任务，并发重试共享当前尝试；重试成功不改写已交付的首次失败。释放线程检查在接管尝试前执行，拒绝不使资产或重试资格失效；直接自等待检测统一到 Core，不保留第二套资源释放调用链。
- 资源槽、页面视图归还、预加载和 ContentViewProvider 回滚通过账本接入外部凭证，未知适配器按不可重试保留。LifetimeScope 的普通同步/异步回调失败后继续保留回调；异步回调可声明幂等能力、拥有者与线程。已有凭证、直接持有的责任和子作用域沿用各自记录，不再叠加第二份归还责任。作用域、关闭与 Shutdown 首次失败仍属于历史结果，不能用重试成功复活它们；当前实际责任通过账本查询。
- 临时 .NET 驱动通过 32 项断言：首次共享结果、Pending/Completed、部分归还后的失败、稳定身份与上下文、显式重试、首次失败保留、并发重试、不可重试拒绝、直接自等待、作用域结束后的回调重试、未知适配器共享责任、直接 Own 责任无重复记录，以及初始/重试线程拒绝与正确线程恢复。入口 `/private/tmp/mui-cleanup-acceptance-20261008/Program.cs`，日志 `/private/tmp/mui-cleanup-acceptance-20261008.log`，未新增仓库测试。首次 dotnet run 的构建进程停滞，定位并终止该任务进程后改用现有单工作进程构建及 DLL 运行方式，编译和断言通过。
- 临时原生 RawImage 场景通过 7 项 Play Mode 断言：新纹理提交后旧凭证归还失败不撤销显示；失败责任可查询；并发重试共享任务；后端已部分归还的步骤只执行一次；Pending 仍可查询；确认成功后同一责任移出账本；最终清空当前显示并归还新凭证，首次生命周期失败仍保留。首次资源尝试 2 次、后端归还 1 次，第二资源归还 1 次，全部同步本地步骤在 frame=1 完成。日志 `/private/tmp/mui-cleanup-retry-play-20261008.log`，源码 `/private/tmp/MuiCleanupRetryObservation.cs`。
- macOS IL2CPP Development/High stripping、960×640 Player 构建 Succeeded/errors=0/warnings=0；最终 Player 的同一 7 项断言通过，firstAttempts=2/firstReturns=1/secondReturns=1，退出码 0。产物 `/private/tmp/mui-cleanup-retry-il2cpp-20261008.app`，日志 `/private/tmp/mui-cleanup-retry-il2cpp-build-20261008.log` 与 `/private/tmp/mui-cleanup-retry-il2cpp-player-20261008.log`。退出仍有 13 条 allocator 提示，不报告干净退出；未以该局部场景证明复杂容器的全部依赖清理。
- 最新代码的既有资源/转换、七组父子换绑和 38 项 Legacy/TMP 嵌套路径 Play Mode 回归通过；日志分别为 `/private/tmp/mui-cleanup-regression-contracts-play-20261008.log`、`/private/tmp/mui-cleanup-regression-rebind-play-20261008.log`、`/private/tmp/mui-cleanup-regression-paths-play-20261008.log`。后两类容器完整清理/重试矩阵、独立通知链的宿主上下文及实体交互仍待验收；既有性能基线早于本轮基础生命周期改动。完整设计目标未完成，未自动提交。

### 2026-10-08 嵌套属性路径绑定

- `BindAttribute.SourcePath` 相对于声明成员解析普通、生成及继承属性；中间模型须派生自 ViewModel。`NullValue` 使用目标类型常量，省略时取目标类型默认值。生成器输出无反射的访问器、完整属性路径清单和跨程序集绑定元数据；包内 Analyzer DLL 已同步更新。公开用法及各模式语义见 [入门文档](../Documentation~/index.md#嵌套属性路径)。
- `BindingSourcePath<TViewModel, TValue>` 保存通知链及叶属性读写委托。运行时捕获叶拥有者后读取、转换、检查代际再写入，不在反向提交时重新遍历中间属性；同一模型移除后恢复也会使旧输入失效。中间模型缺失不调用转换器，禁止反向写入，不回放路径恢复前的输入。OneTime 不订阅；OneWayToSource 在初始缺失时跳过源写入，转换期间替换模型也使已暂存写入失效。换绑预览只读捕获路径身份，准备后模型替换会拒绝旧候选。
- 临时 Roslyn 驱动完成 19 组声明观察：生成叶属性、多层同名节点、跨程序集基类元数据及派生绑定、NaN/Infinity 回退，以及缺失/私有/static/indexer/非模型路径、空或非法路径、错误回退、重复反向写入等。非法声明均产生定位到源代码的 MUI001；有效声明的完整生成代码编译通过。入口 `/private/tmp/mui-path-generator-20261008/Program.cs`，日志 `/private/tmp/mui-path-generator-20261008.log`，未新增仓库测试。
- 临时原生场景使用相同 PathRoot 生成模型，Legacy 与 TMP 各通过 19 项断言：初始化/规范化/校验草稿；叶替换及旧叶退订；根与中间模型 null；恢复及无旧输入回放；反向转换替换；同一叶移除恢复；前向转换和叶 getter 重入；解绑；只读准备及候选身份失效；新父绑定；初始缺失与初始转换期间替换。Play Mode 和 macOS IL2CPP Development/High stripping Player 均为 cases=38，首次本地绑定及这些同步操作在 frame=1 完成。未以此证明实体键盘、输入法或多帧异步子容器换绑。
- 构建 Succeeded/errors=0/warnings=0，Player 退出码 0；日志 `/private/tmp/mui-path-binding-play-20261008.log`、`/private/tmp/mui-path-binding-il2cpp-build-20261008.log`、`/private/tmp/mui-path-binding-il2cpp-player-20261008.log`，产物 `/private/tmp/mui-path-binding-il2cpp-20261008.app`。Player 退出仍有 11 条 allocator 提示，不报告干净退出。
- 用最新运行时代码复查既有资源/转换和七组父子换绑场景，Play Mode 均通过；前者包括 Legacy/TMP 校验重入、迟到资源及两项注入故障，后者含准备失败/取消/关闭竞争/提交故障，两组均报告预期的两项故障。日志 `/private/tmp/mui-path-regression-contracts-play-20261008.log` 与 `/private/tmp/mui-path-regression-rebind-play-20261008.log`。这些回归未重新执行 IL2CPP，早前 IL2CPP 与性能基线保留其原版本范围。完整设计目标仍未完成，本轮修改未自动提交。

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

## 历史迁移检查范围

本节为迁移初期的范围记录。当前实现、证据和剩余交付条件以上方“当前交付收尾清单”及最新日期记录为准；下述范围继续用于完整审计，不能据早期未验收措辞否定后续已取得的证据。

1. API：统一异步 Provider、导航、子视图与生命周期，移除第二套同步分支；同步委托走立即完成路径，更新生成器和全部示例。
2. 导航：运行验证关闭提交、转场与最终清理分离；核对短提交队列、实例预留、重复关闭决议、取消、守卫与不可变结果。
3. 资源：缓存仅保留解绑后的 View 和可移交凭证；核对预加载合并、依赖持有、加载器寿命、资源槽切换与失败责任记录。
4. 数据绑定：核对双向校验、嵌套模型、换绑准备隔离、主线程检查、命令代际与扩展控件。
5. 容器与列表：核对父子生命周期、固定槽位及设计列出的全部虚拟列表能力，包括动态尺寸、定位、锚点、焦点与有界物化。已移除 Core/Collections 的分页、分组、树形模型、VirtualListElement 的分页驱动及对应导航示例；保留扁平条目与模板选择。范围及边界通知已接入现有纵向布局，横向已接入共用滚动轴逻辑；完整定位、锚点恢复及其他剩余核心能力仍需实现和验收。
6. uGUI：核对跨宿主输入、完整手势取消、模态屏障、布局、安全区、外部销毁与可选适配。
7. Editor 与交付：校验规则统一、轻量向导、公共控件契约、UPM 文档与清单、样例依赖及生成器发布。
8. 验收：桌面全新 Unity 项目安装包、逐项导入示例、真实交互、异常清理、至少一个 IL2CPP Player 与性能基线。已有后续分项记录，完整交付仍按设计第 8 节审计。

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

### 2026-10-09 项目服务与动态列表联动

设计第 8 节要求语言/主题适配示例同时更新动态列表尺寸与输入资格。原测量预览直接修改字号，没有展示服务接入。本次在现有 Navigation 测量分支借用 sample-local LocalizationService 和 ThemeService：目录订阅批量更新 2000 个模型并发布 Update，保留逻辑键；主题订阅更新模板及当前单元字号并调用 InvalidateSizeMeasurements。服务及订阅由同一个局部 LifetimeScope 收尾。应用目录至测量完成期间使用 View.InputGate 的独立阻挡令牌，不修改 Runtime。

同步到最终新工程后实际运行：初始/窄视口/主题字号/内容更新四次定位均为 Ready，展示单元分别 5/4/4/3；服务切换日志为 fr-FR、large-text、input=False，完成测量后 input=True。画面显示法语大字目标，Console 0 警告、0 错误。停止 Play 后未发现退出异常。证据：`/private/tmp/mui-measured-services-play-20261009.log`。两份修改 C# 的成员结构和大括号检查通过：`/private/tmp/mui-measured-services-style-20261009.log`。八项已导入 Sample 按 package.json 的 displayName 对应核对 254/254 文件一致：`/private/tmp/mui-final-sample-content-after-services-20261009.json`。此检查不代替真实持续拖动补偿。

可选 Input System 已通过 UnityEditor.PackageManager.Client.Add 安装官方 com.unity.inputsystem@1.19.0，manifest 和 lock 均确认 registry 直接依赖；官方提示启用原生后端并重启后，activeInputHandler=2（Both），工程重新打开成功。安装/重启不代替实际新输入模块、返回和模态手势验收。Dialogs 两种 Prefab 已通过 Tools/MUI 的实际菜单创建并保存；按 README 配置 Canvas、目录、独占 UIHost 和 LocalDialogsDemo，文本使用本机 STHeiti 字体。实际 Play 显示完整中文确认框，Console 0 警告/0 错误，记录 `/private/tmp/mui-dialogs-clean-play-20261009.log`。后续键盘结果见下节。CommonPatterns 和 RecyclingList 的人工交互请求尚未收到结果，不计为通过。完成的临时安装及场景配置脚本已移至 `/private/tmp/mui-clean-editor-setup-retired-20261009`，避免重启重复安装或覆盖场景；当前接线脚本仅用于此独立验收工程。

### 2026-10-09 干净工程的对话框键盘与拖放画面

- Dialogs 使用原 StandaloneInputModule：原生 Right/Return 各持续 250 ms 后，确认框关闭并显示“操作完成”提示框，再按 Return 关闭提示框；日志依次为“已取得确认结果”和“提示框结束：Completed”。独立重开 Play 后仅按 Return，默认取消关闭确认框，日志为“用户未确认：Completed/Closed”。操作没有直接调用 UnityEvent 或 ExecuteEvents，画面与结果均已观察，Console 0 警告/0 错误。完整日志 `/private/tmp/mui-dialogs-clean-keyboard-20261009.log`。此前过短按键无结果的原因未确认，不推断为已定位的引擎问题。
- DragDrop 按 README 接线的场景首次实际进入 Play，金色道具、Accept/Reject/Fail 和 Ready 均正常绘制。自动原生拖动未改变 Ready，不能记录为拖放通过；不继续盲试坐标。最初保存日志截段遗漏了停止时的异常，不能用于证明退出成功；完整日志复核和修复见下节。
- 新建独立 DialogsInputSystem 场景，使用官方 InputSystemUIInputModule.AssignDefaultActions，移除 StandaloneInputModule；项目侧取消动作仅调用 UIBackInput.RequestBack。实际运行确认 module=InputSystemUIInputModule、keyboard=True、selection=Cancel，Console 0 警告/0 错误。自动 Escape/方向/提交均未产生动作回调，已请求人工在 Game 画面聚焦后按 Esc；结果尚未收到，保留 Play 现场。临时接线源码 `/private/tmp/MuiInputDialogsSetup.cs`、`MuiInputBackBridge.cs`，日志 `/private/tmp/mui-inputsystem-clean-native-attempt-20261009.log`；未修改包 Runtime。
- 再次读取新工程 manifest/lock、Unity 版本和八项导入内容，确认 MUI 本地引用、TMP 3.0.7、官方 Input System 1.19.0，Sample 文件 254/254 一致，无缺失或差异；报告 `/private/tmp/mui-clean-package-final-content-20261009.json`。本次没有提交或推送。
- 收尾方式核对：原设计第 8 节明确要求 Basic 的真实鼠标/键盘交互；其他协议条件按各自声明的原生控件或输入模块运行路径核对，不为每条重复添加实体硬件门槛。设计第 1–8 节没有单独要求输入法验收，IME 作为补充观察保留，不再作为新增完成门槛；历史记录中的“尚未验证 IME”仍如实保留。这不把未完成的 Sample 操作、Input System 返回/关闭手势、拖动补偿或最终源码对应自动计为通过。

### 2026-10-09 DragDrop 退出修复与 Input System 原生返回

- 读取完整 Editor.log 后确认 DragDrop 首次停止 Play 报 `View is retained until its activation cleanup is confirmed`；原 OnDestroy 在 LifetimeScope 清理前同步 Dispose View，且原生销毁顺序不确定。保留完整失败证据 `/private/tmp/mui-dragdrop-stop-reproduction-20261009.log`，撤销上一条退出成功的误记。其他示例中的相似回调已作源码核对，本轮只修改此已复现的示例。
- DragDropDemo 增加公开共享 ShutdownAsync，提前发布 Task 防止重入；先撤销输入、等待会话及激活作用域，再异步释放 View，确认后销毁拥有的 Canvas/EventSystem。OnApplicationQuit 提前启动清理，OnDestroy 观察同一任务；未确认的失败保留原生对象，不把销毁当成清理完成。Start 拒绝在已经退出后重新创建对象，初始化失败也进入同一收尾。README 明确场景切换前显式等待及原生退出回调的限制。
- 同步最终新工程后实际 Play 并停止，Console 0 警告/0 错误；本次按停止前字节偏移保存完整增量日志，包含实际退出和场景恢复，Exception/MUI error 均为 0。证据 `/private/tmp/mui-dragdrop-stop-fixed-play-20261009.log`。临时原生适配观察再一次合并验证立即 Committed、600 ms 延迟提交取消为 Cancelled、Finished 各一次、重复 Shutdown 共享 Task、拥有 Canvas 销毁、借用 EventSystem 保留，以及 Start 前退出不再构建，共 18 项通过；记录 `/private/tmp/mui-dragdrop-shutdown-observation-play-20261009.log`，源码 `/private/tmp/MuiDragDropShutdownObservation.cs`。该观察调用原生适配入口，没有模拟实体鼠标，不代替真实拖放验收。
- DragDrop 示例及依赖离线构建 0 warnings/0 errors，成员布局及控制流大括号违规均为 0，git diff --check 通过；日志 `/private/tmp/mui-dragdrop-shutdown-build-20261009.log`、`mui-dragdrop-shutdown-style-20261009.log`。首个并行 MSBuild 无输出，确认 PID 后终止；单节点编译 4.59 秒通过。规范检查最初的临时目录缺少工具固定扫描子目录，补齐后通过。未新增仓库测试，没有 Runtime 修改或重复 Player 构建，没有提交或推送。
- Input System 现场随后记录 `/Keyboard/escape` 的真实 InputAction 回调，原生调用栈经 NativeInputRuntime，选择从 Cancel 清为 none，业务日志为“用户未确认：Completed/Closed”；现场画面确认对话框已关闭，随后退出 Play。临时桥接未调用 QueueStateEvent/ExecuteEvents，证据 `/private/tmp/mui-inputsystem-clean-native-escape-20261009.log`。这关闭本场景原生 Esc 返回条件；关闭 Pointer 和下层穿透仍需最终源码对应，不扩大为整个可选适配已完成。

### 2026-10-09 最终 Input System 模态手势与工程清理

- 复用已有 MuiInputSystemObservation 与生成模型，在最终新工程通过官方 InputSystemUIInputModule 执行模拟 Keyboard/Mouse 设备事件。返回提交后视觉退出期间点击未到达下层；退出完成后的新点击有效；另一轮 PointerDown 发起关闭并登记关闭 Pointer，视觉退出后仍命中 Modal Barrier，释放未点击下层，下一次新点击恢复。日志明确记录 nativeBack=True、exitClickBlocked=True、closingPointerHeld=True、releaseNoClick=True、restoredClicks=2，以及 shutdown complete。此为实际输入模块执行证据，使用 QueueStateEvent，没有 ExecuteEvents，不声明实体鼠标操作已通过。
- 当前 452 份 Runtime 源码在本批次前后校验值一致；Editor 刷新编译并执行观察，增量日志没有编译错误或异常，现场停止 Play 后 Console 0 警告/0 错误。证据 `/private/tmp/mui-final-inputsystem-play-20261009.log`、`/private/tmp/mui-final-inputsystem-20261009/start.json` 和同目录 `result.json`。本批次没有 Runtime 修改，不重复构建无关 Player；历史 Input System Player 证据仍保留其源码版本边界。
- DragDrop 临时回归脚本、一次性设置和场景及 meta 已移至 `/private/tmp/mui-dragdrop-shutdown-retired-20261009/imported`；最新 DragDrop README 同步后，八项 Sample 再次核对 254/254 一致，报告 `/private/tmp/mui-clean-sample-final-after-dragdrop-20261009.json`。本批次 Input System 观察入口、场景和已完成的 Dialogs 一次性设置脚本及 meta 移至 `/private/tmp/mui-final-inputsystem-20261009/imported`，避免重启重跑接线。实际 DialogsInputSystem 场景依赖的项目桥接保留。
- 已恢复 Basic Sample 并进入 Play，标题与 Done 正常绘制，无粉色缺失材质。Start 本身自动打开页面，不能把刷新后出现页面归因于先前的自动鼠标点击；自动点击 Done 尚未提交结果，已请求用户按设计执行鼠标 Done(42) 和重开，未收到结果前不计为通过。没有提交或推送。

### 2026-10-09 Basic 键盘与真实图片尺寸修正

- 最终新工程的 Basic 场景通过原生键盘执行 Return 250 ms：画面出现“42: Reopen”，业务结果 Completed/value=42；再次 Return 打开页面，Escape 250 ms 返回，结果 Dismissed/value=0，画面恢复 Reopen；随后 Return 再次打开。操作没有调用 UnityEvent、ExecuteEvents 或直接导航 API，Console 0 警告/0 错误。记录 `/private/tmp/mui-basic-final-keyboard-20261009.log`。之后停止 Play 的第三条 Dismissed 来自场景退出，不把它计为另一项键盘通过。实体鼠标仍没有通过记录。
- 旧布局观察的“image-size”条件仅改变自定义 LayoutElement.Extent，不能证明实际图片加载。本轮在最终新工程使用 Unity 原生 Image 的首选尺寸，不配置合成 LayoutElement 尺寸：1000 项列表定位到键 500 完成后，偏移 17 Canvas 单位，再用 Resources.LoadAsync<Sprite> 加载真实导入的 PNG Sprite，将前一项 Image 从 80 改为 192，并调用公开 InvalidateItemSize；随后同样修改锚点项。纵向及横向均保持锚点 500/偏移 17，实际行尺寸变为 192，原定位结果持续 Ready，不重新发起定位；展示实例为 4，单帧测量最多 2。两方向合计 26 项检查通过、0 失败。
- 图片观察记录 `/private/tmp/mui-image-resize-play-20261009.log`，源码 `/private/tmp/MuiImageResizeObservation.cs`，资源导入配置 `/private/tmp/MuiImageResizeSetup.cs`，前后源码对应 `/private/tmp/mui-image-resize-start-20261009.json`、`mui-image-resize-result-20261009.json`。452 份 Runtime 源码未变，增量日志无编译错误或异常。此为 Editor 原生图片及测量运行证据；没有重复既有列表矩阵或构建 Player，不替代持续鼠标拖动补偿。
- 临时 Image 观察脚本、场景和专用 Resources Sprite 及 meta 已移至 `/private/tmp/mui-image-resize-retired-20261009/imported`，报告 `/private/tmp/mui-image-resize-cleanup-20261009.json`。恢复 Basic 场景；没有新增仓库测试或自动提交。

### 2026-10-09 多宿主共享输入最终核对

- 在最终新工程复用生成命令模型，通过官方 InputSystemUIInputModule 和模拟 Mouse/Keyboard 设备事件验证两个 UIHost 共享 EventSystem：无模态时 Pointer 按实际射线命中宿主分发，原生提交交给焦点宿主；经另一宿主发起的返回仍交给焦点拥有者，同帧重复请求只接纳一次，守卫拒绝不向其他宿主重发，也不发布业务结果。
- 模态进入期间另一宿主射线关闭；明确等待 WaitForEnterAsync 后，模态获得选择，原生提交和 Escape 均交给模态宿主。关闭 Pointer 在关闭提交及视觉退出后继续消费，仍按住时另一宿主保持受阻，释放没有下层点击，新的点击恢复。叠加模态按宿主优先级仲裁，BringToFront 同时提升 Canvas 排序与输入优先级；ForceClose 上层后仍由另一模态屏障独占，全部退出后恢复两个宿主。
- 独立 EventSystem 同帧各自接纳一次返回，跨组来源被拒绝；有意修改 Canvas.sortingOrder 后只报告一次配置冲突并拒绝歧义输入，恢复配置后输入恢复。最终 29 项检查通过，failures=0/reports=1；该一次报告是预期排序冲突。原始日志 `/private/tmp/mui-shared-input-final-play-20261009.log`，源码 `/private/tmp/MuiSharedInputObservation.cs`。使用模拟设备事件和公开返回 API，不使用 ExecuteEvents，不替代 Basic 的实体鼠标要求。
- 独立输入组场景有意同时创建两个普通 EventSystem，Unity uGUI 的 Editor 检查输出 19 条多 EventSystem 警告；来源为 `com.unity.ugui@1.0.0/Runtime/EventSystem/EventSystem.cs` 的 Update 检查。这些警告不计入框架 reports，也不能据此声称该批 Console 无警告；没有为压制验收场景警告修改包实现。
- 首两轮观察把 OpenAsync 完成当作进入转场完成，此时 modalInput=False、wants=False、FocusedHandle 无效，属于观察完成点错误；按设计第 3 节显式等待进入后 selection=Confirm、modalInput=True、wants=True，导航焦点与模态句柄一致。另一次清理诊断来自观察工程先销毁 Canvas 再退役共享登记，临时 Fixture 改为显式撤销登记后销毁。失败与诊断分别保留在 `/private/tmp/mui-shared-input-first-observation-20261009.log`、`mui-shared-input-focus-diagnosis-20261009.log`，没有把它们计为包缺陷或通过结果。
- 本轮没有修改 Runtime，452 份源码前后校验一致，最终日志没有编译错误或异常；对应 `/private/tmp/mui-shared-input-final-start-20261009.json`、`mui-shared-input-final-result-20261009.json`。临时脚本、模型、设置与场景及 meta 已移至 `/private/tmp/mui-shared-input-retired-20261009/imported`，清理记录 `/private/tmp/mui-shared-input-cleanup-20261009.json`，恢复 Basic 场景。没有新增仓库测试、重复无关 Player 构建或自动提交。

### 2026-10-09 Basic、Inspector 与最后缺口闭合

- 用户完成实体鼠标 Done，确认“42: Reopen”，点击重开并选择 BasicPageViewModel；随后确认“定位控件”“打开声明”“Validate Binding Contract”三项都正常。现场核对 Title → Title.Content / BasicPageViewModel.cs:10、ConfirmCommand → Confirm.Clicked / :14，验证消息为“The selected binding contract matches this View.”。源码编辑器打开结果由用户确认；现场 Inspector 目标与合约信息也已观察。此前 UIHost 实际快照操作取得 active/history=1、其余计数=0、failure=False、basic.page/Open、prepared/committed=True/Ready。此项关闭原实体 Basic 与 Inspector 缺口。
- 临时 Core 观察补齐四组 28 项：批量一页拒绝不阻塞其他页；取消区分 Closed、WaitCancelled 与未尝试；生命周期和错误观察者抛错仍继续分发/提交/清理；多个打开/清理等待者共享一次异常诊断，失败归还保留责任，只经显式安全叶重试恢复。`.NET` 结果 `mui-final-gaps-core-20261009.log`；Unity 同源码 Core 结果在 `mui-final-gaps-play-first-20261009.log`，该首轮随后的原生观察失败不计作整批通过。
- 原生补验三种 DragDrop 失效（门控、遮挡、关闭）均只产生一次 Cancelled、清除 pointerDrag/dragging、businessDrops=0；退出转场采样抛错后屏障关闭、画面移除、下层输入及实际 RaycastAll 命中恢复，业务 Dismissed 不改写。24 项通过，reports=1 为主动注入的转场失败。证据 `mui-final-gaps-native-play-20261009.log`。首次合成 drag 的 eligibleForClick 与自然拖动前提不符，只修正临时观察器，没有改包。
- 官方 InputSystemUIInputModule 在 2 秒视觉退出内消费新点击、滚轮、Enter 提交和 Escape 返回，下层计数均为零；视觉结束后新点击与滚轮恢复，Shutdown 完成。证据 `mui-final-gaps-exit-inputs-play-20261009.log`。以上均区分公开/原生入口和模拟设备，没有声称实体鼠标。

### 2026-10-09 最后一批示例输入与连续拖动

- 使用一个借用官方 InputSystemUIInputModule 的合并观察器顺序运行公开 Sample，按钮及拖动经 QueueStateEvent 和真实模块派发，没有直接 Invoke 按钮或 ScrollRect 回调。CommonPatterns 的 Toast、活动通知语言更新、主题 Sprite 更新、加载期间按钮禁用、结束通知/输入恢复与工作中关闭均通过；Settings 原生滑块、Save、Reset、Lock/Unlock 和保存中关闭通过，结束为 binding=Unbound/saveExecuting=False。Legacy 文本编辑明确使用 InputField.text/onEndEdit 回调，覆盖无效值、校验/禁用与 Alice 正常转换，未当作模拟输入法证明。证据 `mui-final-samples-first-play-20261009.log`。
- 首轮 Tabs 观察器误用“Inventory”而实际内容为“Inventory content”，修正预期后仅继续未通过的四组，不重跑 CommonPatterns/Settings。Tabs 初始 Inventory、Quests 首次失败、Retry 成功、切回 Inventory 和 locked=False 通过；RecyclingList 原生增删、字体失败保持原显示、清空/同键恢复通过，显式关闭最终创建 15/归还 15/持有 0；DragDrop Accept 只提交一次，Reject/Fail 均保留 moves=1，等待 Shutdown 后拥有 Canvas 消失。
- 连续列表拖动经同一官方模块保持按下捕获；横纵各在持续拖动中把前项 Image 从 80 改为 192，anchor=500/offset=23 不变、补偿=112，下一次 6 单位指针移动仅推进 6，没有回到旧基准；begin/end 各一次，materialized=6。此为原生模块持续捕获路径，非实体设备观察；满足设计第 5/8 节的拖动补偿条件，Basic 的实体门槛另由用户实际操作满足。证据 `mui-final-samples-remaining-play-20261009.log`。最后四组 failures=0/reports=1（主动字体失败）/ledger=0，前两组各通过且退出基线恢复，六组均关闭。
- 本轮没有 Runtime 修改。最后 gap 与 sample 两批补验各保存 452 文件前后校验，全部一致；相关临时观察、设置和场景及 meta 可恢复地移至 `mui-final-gaps-20261009/imported`、`mui-final-samples-20261009/imported`。没有新增仓库测试或重复无关 Player 构建。

### 2026-10-09 S3 最终交付核对与收尾

- 最终 615 个 C# 文件通过成员布局/语法/大括号检查，0 文件待调整/0 人工冲突/0 大括号违规；日志 `/private/tmp/mui-final-handoff-style-20261009.log`。人工核对 uGUI/Editor/Sample 的 ?. 接收者均为普通托管对象或委托，没有 UnityEngine.Object 条件访问；Unity 对象继续显式 == null。
- 根文件齐全；600 个 meta 无缺失/非法/重复 GUID；41 个程序集（29 个 Runtime/Editor、12 个 Sample）边界保持 Core/Resources/Navigation 无 Unity 引用，Runtime 无 Editor/Sample 依赖，Editor 平台隔离，TMP/Input System 按需启用。八项导入文件 254/254 一致；manifest/lock 保持本地 MUI 0.1.0；Analyzer SHA-256 为 d5dc8324970e10b2960413cc0382b5fef447f8dc80e311893da06eb5f38b44c7。报告 `/private/tmp/mui-final-handoff-audit-20261009.json`。
- 注释继续说明准备/提交、代际、依赖释放、失败保留与原生退出边界；Navigator 表达页面导航协调，UIHost 负责 Unity 装配与帧驱动，保留名称。LifetimeScope、AcquiredView/AcquiredResource、Extent 与公开异步契约命名一致；Core/Resources/Navigation/uGUI/Editor 分层及可选适配、资源后端/自定义 Element 扩展入口保持明确，未引入第二套同步或通用恢复系统。
- 原设计九类场景与 20 条确定性条件全部闭合，S1/S2/S3 完成。必要 IL2CPP 运行及性能基线对应当前源码；目标设备预算与未归因 allocator 提示按上方边界如实保留。恢复 Basic 公开场景，临时路径残留为 0；报告 `/private/tmp/mui-final-handoff-cleanup-20261009.json`。未自动提交或推送。

- 最终刷新后实际窗口为 Basic，Play 已停止，Console 0 警告/0 错误，无粉色缺失 Shader；最后刷新/编译日志 `mui-final-handoff-editor-20261009.log` 未见 C# 编译错误。Documentation 与 Changelog 同步最终验收状态；Sample 的历史开发状态附明确的最终记录入口。
