# MUI 入门

## 安装范围

目标版本为 Unity 2022.3.62f3。通过 Package Manager 的 Add package from disk 选择包根目录的 package.json；运行时代码保留在 Packages 引用中，示例由 Package Manager 的 Samples 独立导入。

Core 不引用 Unity；Resources、Navigation 与 Widgets 分别定义资源凭证、页面导航和子视图生命周期。MUI.UGUI 提供原生 uGUI 的 Element 适配。TMP 与 Input System 位于独立程序集，项目按需安装对应包；基础页面不要求这些可选依赖。

## 页面打开、关闭动画

在 `MUISettings` 的转场设置中配置动画总开关、默认进入动画和默认退出动画。总开关关闭时，页面覆盖也被禁用，正在播放的动画在下一次 UI Tick 收尾。运行时可设置 `settings.AnimationsEnabled`；默认资产变更在下一次创建播放实例时生效，不替换正在播放的实例。减少动态效果设置仍优先跳过动画。

View Inspector 的转场设置提供 `Inherit`（继承全局）、`Disabled`（此页面禁用）和 `Override`（页面覆盖）。覆盖模式可分别指定进入、退出资产或组件，组件优先于资产。没有配置动画时沿用原有 `EnterDuration` / `ExitDuration` 淡入淡出时长，零表示无动画。代码工厂 `EnterTransitionFactory` / `ExitTransitionFactory` 优先于资产和组件，但仍服从总开关及页面禁用设置。

通过 `Assets/Create/MUI/Curve View Transition` 创建内置曲线动画资产。可同时启用透明度、缩放和位移；同一资产可以用于进入与退出。进入从隐藏态到稳定态，退出从当前画面到隐藏态。缩放为相对稳定态的倍率，位移为相对稳定态的局部偏移，曲线允许回弹，透明度限制在 0—1。`Transition Target` 为空时使用页面根 RectTransform，建议指定不受布局和业务绑定驱动的内容节点。退出隐藏后及缓存复用前仅恢复动画控制过的位置和缩放，不重置纯淡入淡出页面的布局。

自定义动画继承 `ViewTransitionAsset`（共享配置）或 `ViewTransitionBehaviour`（预制体组件），实现 `CreatePlayback(ViewTransitionContext)`，每次返回独立、尚未启动的 `ITransitionPlayback`。也可以在页面准备阶段通过工厂直接注入实例。内置 `TransitionPlayback` 基类提供完成任务和幂等停止、释放：

- `OnPlay()` 启动播放器；外部动画结束后调用 `Complete()`，失败时调用 `Fail(error)`。
- `OnTick(unscaledDeltaTime)` 为可选的框架非缩放时间驱动；自行计时的播放器可不实现。
- `OnStop(reason)` 必须同步停止所有后续视觉写入、取消播放器并解绑完成回调。`Interrupted` 保留当前姿态供退出动画接管；其余原因按实现收尾。不能仅取消等待而让动画继续播放。
- `OnDispose()` 释放本次播放资源，不能启动新动画或破坏中断保留的姿态。播放器操作和视觉写入必须在 Unity 主线程进行。

框架等待 `Completion` 的实际结果，同时保留策略的进入/退出超时。完成信号在 UI Tick 中处理，因此自定义动画同样需要宿主帧泵。退出播放不绑定业务激活的取消令牌；业务取消后，画面和资源仍保留到退出完成。打开中途关闭时先停止进入播放器，再创建退出播放器。`ViewTransitionContext.SetAlpha()` 通过框架门控设置透明度，避免直接写根 CanvasGroup 与可见性控制冲突。自定义适配器可调用 `context.RegisterTargetRestore(position, scale)`，登记退出隐藏后需要恢复的目标属性；只登记本动画控制的属性。

可在项目可选程序集内实现 Tween、Animator 或 Timeline 适配器；MUI 核心不依赖这些播放器，当前包内提供曲线实现和自定义接入协议，未内置第三方适配器。正常打开使用句柄的 `WaitForEnterAsync()` 等待视觉完成，关闭使用 `WaitForExitAsync()`；`OpenAsync()` 仍只等待就绪，不等待动画。

## 从 Basic 开始

导入 Basic Example 并打开 Basic.unity。BasicPageViewModel 声明标题绑定与 Confirm 命令，BasicView.prefab 包含名为 Title 和 Confirm 的 Element。导入的 BasicMUISettings 可作为项目唯一全局配置；页面资源标识 BasicView 直接对应 Resources/BasicView.prefab，无需在 UIHost 登记。BasicExampleModule 声明生成注册入口，BasicDemo 负责注册、解析配置策略、初始化 UIHost 的默认提供方并调用 OpenAsync。

普通页面不要求 Presenter；需要跨服务协调时再引入。不要手写或编辑生成的绑定订阅、命令属性和 PageDefinition 接线。若看不到生成类型，先检查 Console 中的声明诊断，以及包内 Analyzers 的 RoslynAnalyzer 导入标签。

## 项目 UI 配置

项目只保留一份 `MUISettings`。页面向导、控制台和 UIHost 自动查找 Assets 下唯一的配置资产，无需在每个宿主拖入。通过 `Assets/Create/MUI/配置 (Settings)` 定位已有资产；不存在时创建 `Assets/MUI/MUISettings.asset`。有多份资产时报告冲突路径，运行与构建不任意选取；请合并并保留一份。没有配置时使用内置默认值。

编辑器自动将唯一配置登记到 Player 的 Preloaded Assets，保留其他预加载资产；构建前再次检查唯一性、配置有效性并同步引用，Player 与 Editor 使用同一资产。配置不会启动宿主，仍需项目显式调用 `Initialize()`。旧 UIHost 的局部配置引用不再生效，Settings 读取全局配置。

Layers 为命名层级。排序值越大越靠上，同层按导航顺序排列；拥有者与依赖约束仍参与最终显示顺序。命名层映射为 `PagePolicy.Layer`，页面仍挂在同一宿主根节点，不会自动创建独立 Canvas。内置层级与预设如下：

| 预设 | 层级 | 默认行为 |
| --- | --- | --- |
| Background（背景层） | Background / -100 | 独立页面，不接收输入、不阻挡射线、不处理返回 |
| Default（继承默认规则） | Default / 0 | 继承页面默认规则；初始为主页面，获取焦点，返回时关闭 |
| FullScreen（全屏主页面） | Default / 0 | 主页面，隐藏下层页面；不自动调整 Prefab 尺寸 |
| Popup（模态弹窗） | Popup / 100 | 默认依附当前主页面；下层可见但不接纳输入；无主页面时需显式传入 PageOwner.Host |
| Notice（提示层） | Notice / 200 | 独立页面，不接收输入、不阻挡射线、不处理返回；由业务关闭 |

同层顺序在打开请求接纳时确定，与预制体加载、准备和转场完成顺序无关。例如先请求 A、再请求 B，B 先准备完成时可先显示；A 完成后仍在 B 下方。新依赖沿用父请求的顺序，并按 RequiredBefore / AttachedAfter 确定与父页面的相对位置。只有显式 BringToFront 才更新既有页面的同层导航顺序；ReturnReady 复用保持原位置。打开历史按请求顺序插入已提交页面，置前不改写历史，替换继续继承源页面的历史位置；取消或失败的准备不会留下历史记录。

“页面默认规则”提供公共值；“页面行为预设”中勾选的规则组覆盖公共值，未勾选组继承。“未指定时使用”仅作用于未明确传入预设名称的解析调用。预设展开后显示关键生效值及来源，不包含代码中的页面专属设置。可配置页面角色、输入、遮挡、焦点、模态、返回、多实例、Tick、缓存与各阶段超时；Cache、Instances、Tick、Timeouts 在资产中按组覆盖。命名层、预设名区分大小写。配置校验会检查名称、引用、层级排序值、容量及解析后的策略，不验证项目业务回调或原生点击效果。

ReceivesInput=false 会禁用页面输入及自身射线阻挡，也不获取焦点；Coverage 单独控制对下层页面的影响。旧资产的 Notice / Background 在加载时补齐输入关闭设置，已有显式输入覆盖保留。模态在 Coverage=None 时自动阻挡下层输入；取消模态会撤销自动阻挡，但不会撤销显式配置的 BlockInput。

创建路由时显式解析策略：

```csharp
var popupPolicy = host.ResolvePolicy("Popup", overrides: new PagePolicyOverrides
{
    CacheMode = ViewCacheMode.KeepAlive
});
var route = InventoryViewModelDefinition.Create(
    () => new InventoryViewModel(), policy: popupPolicy);
```

代码中的 `PagePolicyOverrides` 每个字段分别覆盖，`null` 表示继承，`false` 和 `0` 是显式值。优先级从低到高为项目默认 → 所选预设 → Inspector 本页覆盖 → 代码／项目配置表覆盖。也可传入 `layer: "Popup"` 选择命名层；不能同时传入命名层和 `overrides.Layer` 数值层。

在页面 Prefab 根 View 的「界面配置 → 页面策略」中点击「配置当前页面」，即可选择预设并按组覆盖层级、页面角色、遮挡、输入、焦点、模态、返回、实例、Tick、缓存、排序跨度及阶段超时。未勾选的组显示继承值；清除本页规则可恢复代码所选预设。配置保存在全局 `MUISettings` 资产，按 `ViewResource.Key + Version` 关联，不要求运行前加载 Prefab；生成契约使用版本 `1`，共享该资源标识的路由共享本页规则。页面预设留空时继承代码预设，Inspector 此时按全局默认预设预览。

配置资产 Inspector 的「页面专属规则」提供集中筛选、编辑、删除和 Prefab 关联入口，与 View Inspector 共用规则编辑控件。关联以 Prefab GUID 保存，不把 Prefab 引用带入运行时加载依赖。新规则及重新打开 View 配置的旧规则会补齐关联；也可在集中列表手动关联。捕获到关联 Prefab（或其父文件夹）的删除事件且 GUID 对应资产已不存在时，编辑器在导入结束后移除规则并保存配置。移动／重命名只更新关联路径；仅标记为按 Resources 路径生成的资源标识会随路径更新，代码契约标识保持不变。新标识冲突或移出 Resources 时保留原标识并在列表提示。未关联的代码／配置表规则、没有确认删除事件的缺失关联不会自动清理，可手动删除或解除关联；规则编辑和删除支持撤销。

`host.ResolvePolicy`、`MUISettings.ResolveGlobalPolicy` 和 `settings.ResolvePolicy` 会复制页面规则。`PageDefinition` 构造时按资源选择最终策略，因此运行时导航和构建目录检查读取相同的 `route.Policy`。已有路由不会随资产编辑改变，需要重新创建路由。直接传入 `new PagePolicy(...)` 表示项目完全控制策略，不自动合并 Inspector 配置。

项目也可以使用配置表，将表数据转换成 `PagePolicyOverrides` 后传给上述解析方法，或者对返回策略调用 `overrides.Apply(policy)`；代码覆盖会在本页覆盖之后应用。资源加载器、打开参数、Presenter 业务数据仍由项目代码配置，不存入页面规则。

策略在创建 PageDefinition 时复制为不可变对象。编辑资产后，新建路由使用新规则；既有路由及其活动页面保持原策略。直接使用 `new PagePolicy(...)` 或生成的 PageDefinition.Create 而未传入解析策略的旧代码，不会自动套用资产；其默认角色与内置配置统一为 Main。需要独立叠加的页面应显式指定 PageRole.Independent。UIHost.Initialize 的缓存、清理、终态记录、排队请求与预加载容量参数省略时继承资产，显式参数优先；初始化后修改资产不会调整导航器容量。这些可选容量参数现为 `int?`，普通调用仍可传入 `int`，已有方法组委托需对应调整并重新编译。

## MUI 调试窗口

打开 `Tools/MUI/控制台 (Dashboard)`，或点击 UIHost Inspector 的“打开控制台”。窗口顶部可切换编辑器语言。选择当前场景的宿主，进入 Play Mode 并完成初始化后点击“采集运行状态”。默认手动采集，可启用每 0.5 秒自动刷新；未初始化时提供引导并禁用运行操作。

UIHost Inspector 只提供只读全局 Settings、View Root、简短状态及 Dashboard 快捷入口；页面资源通过标识加载，不在宿主维护目录；自动更新界面统一在 MUISettings 的“运行设置”中配置，初始化后配置引用不可编辑。完整导航快照与生命周期追踪统一在 Dashboard 操作，快照上限为 1—4096。

- 页面与宿主：按最终显示顺序查看状态、层级、解析后的策略及配置来源、焦点、隐藏和输入阻挡来源、返回历史、依赖及清理责任。支持定位原生 View、单独采集页面输入与资源准备、复制状态报告。绑定和射线检查继续通过 View Inspector 操作。
- 生命周期：开始新一轮记录、停止、清除、筛选和复制已有时间线；记录容量为 1—8192，满时覆盖最旧事件。关闭窗口不会停止导航器记录，需要显式停止。
- 配置与校验：创建或编辑配置资产，自动关联所有宿主，校验配置和已登记的页面目录。层级以名称与排序值表格编辑，实际顺序由排序值决定。预设分别折叠，只显示启用的覆盖项；默认规则按类别分组，名称引用通过选择器填写，缺失引用保留原值并报告错误。列表增删及字段修改支持撤销，配置自动校验。配置资产自动关联所有宿主，无需逐个绑定。

状态采集不启动宿主，也不打开、关闭页面。导航门控反映策略账本，控件实际点击资格需结合原生页面输入与射线检查；快照截断时会标明。页面目录校验仍只覆盖项目通过 `UIBuildValidation.RegisterCatalog` 登记的页面，不会把所有 Prefab 自动视为已验证。

### 编辑器语言

在 Dashboard 的“配置与校验”页选择“编辑器语言”。默认简体中文，内置英文；选择保存在当前用户的 EditorPrefs 中，已打开的 MUI 窗口和 Inspector 随之刷新，不改变 MUISettings 或游戏运行时语言。菜单入口使用固定中英双语，因为 Unity 的 MenuItem 和 CreateAssetMenu 路径必须是编译期常量。Unity 自身的原生控件、对象选择器等继续遵循 Unity 的语言设置。

语言由 `Editor/Localization/Languages/*.mui-language.json` 提供，不使用语言枚举。新增语言可以复制英文资源到项目 Assets 目录，修改 `id`、`displayName` 和词条的 `text`，也可以先提供部分翻译并声明回退语言：

```json
{
  "id": "fr-FR",
  "displayName": "Français",
  "fallbackLanguageId": "en-US",
  "entries": [
    { "key": "language.label", "text": "Langue de l’éditeur" },
    { "key": "language.reload", "text": "Recharger les langues" }
  ]
}
```

文件名须以 `.mui-language.json` 结尾，导入后自动加入语言下拉框；也可点击“刷新语言资源”。每种语言的 `id`、同一资源内的词条 `key` 必须唯一。保留原词条键及 `{0}`、`{1}` 等格式占位符，参数顺序可调整。缺少的词条沿 `fallbackLanguageId` 查找，最后回退到内置中文；回退循环不会导致无限递归，格式错误的词条也会继续尝试回退。未知字段或枚举使用原名称，路由键、资源名、序列化字段名和真实枚举值不翻译。

自定义编辑器程序集引用 `MUI.Editor.Localization`，通过 `MUIEditorLocalization.Get(key)` 或 `Format(key, args)` 取词；UITK 的 `@key` 文本可用 `BindTree(root)` 绑定，动态内容用 `Track(root, refresh)` 跟踪语言切换。诊断词条可额外声明 `source`，将已有框架消息映射到稳定键；未知项目诊断保持原文。新增语言无需修改 C# 语言列表，也无需安装 Unity Localization 包。

## 异步完成点

PageDefinition 实例额度已满时，普通 `OpenAsync` 返回 `OpenRejection.Busy`，不启动新的 Provider 获取。单实例配置 `ReturnReady` 只复用参数与指定模型兼容的就绪页面；准备中的实例仍返回 Busy。显式 `CloseOldest` 通过旧页面的关闭守卫执行替换；同 PageDefinition 的 `ReplaceAsync` 借用源实例额度，同一源句柄的并发替换返回 Busy。

同一激活只接纳一个关闭意图。普通 `CloseAsync`/`BackAsync` 可合并等待同一决议；正在确认 Done 或 Replace 时，另一关闭意图返回 `CloseStatus.Busy` 或 `ReplaceRejection.Busy`，不再打开第二个确认框。每次 Complete/Replace 调用是独立请求；需要多个观察者时由调用方一次转换为 Task 并共享该任务，不比较业务结果值。确认期间的参数更新或换绑成功提交使旧许可失效，本次关闭返回 Superseded，更新后的页面保留。

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

## 自定义 Element

继承 `MUI.UGUI.Element`，在 `OnInitialize()` 中通过 `CreateProperty` 创建属性状态，以公开属性转发 `Value`。通知名称使用对应公开属性的 `nameof`；初始值不会调用赋值回调，相等值不重复赋值或通知。以下适配保留 Prefab 的 Slider 初值，模型写入不派发原生用户事件：

```csharp
using MUI.UGUI;
using UnityEngine.UI;

public sealed class AmountElement : Element
{
    private Slider slider;
    private ElementProperty<float> amount;

    public float Amount
    {
        get => amount.Value;
        set => amount.Value = value;
    }

    protected override void OnInitialize()
    {
        slider = RequireComponent<Slider>();
        amount = CreateProperty(nameof(Amount), slider.value,
            (_, next) => slider.SetValueWithoutNotify(next));
        slider.onValueChanged.AddListener(OnNativeValueChanged);
        TrackCleanup(() => slider.onValueChanged.RemoveListener(OnNativeValueChanged));
    }

    private void OnNativeValueChanged(float value)
    {
        if (CanReceiveInput(slider))
        {
            amount.Value = value;
        }
    }
}
```

模型可声明 `[Bind("Amount", nameof(AmountElement.Amount), bindingMode: BindingMode.TwoWay, ElementType = typeof(AmountElement))]`；Prefab 对应节点命名为 Amount 并挂载该适配。原生用户回调须先检查 `CanReceiveInput`，门控期间忽略的事件不在恢复后回放。显式属性写入继续更新原生状态。

`Track(resource)` 仅在登记成功时接管同步 `IDisposable`，同一对象不能重复登记。`TrackCleanup(callback)` 登记同步收尾；它与属性、原生监听共用最终 `LifetimeScope`，按登记逆序释放，一项失败仍继续处理独立项。初始化失败也沿同一路径收尾，结束后禁止新登记和属性读写。属性赋值先保存值再调用项目回调；回调抛错时不会回滚任意原生副作用。

这三个入口管理控件最终拥有的资源。每次打开或换绑取得的模型、命令及异步资源仍登记到该次激活的显式 `LifetimeScope`。缓存复用不会执行最终收尾；自定义活动引用沿 `OnResetForCache()` 解除。

Element 提供稳定 `CleanupResponsibility`；重复 `Dispose` 或 `DisposeAsync` 观察同一次首次结果。未知清理失败保留回调，不能通过重复调用猜测归还；完整回调确实幂等时可在 `TrackCleanup` 显式声明重试能力。先恢复允许重试的叶责任，再确认 Element 容器责任，容器不重新执行未知回调。这里的控件责任不代表外层 View 或原生节点已完成归还。

## 嵌套属性路径

`Bind.SourcePath` 声明相对于被标注成员的属性路径。例如模型的 `Child` 属性指向另一个 ViewModel，可在该成员上声明：

```csharp
[ObservableProperty]
[Bind("Name", nameof(IInputFieldElement.Value), bindingMode: BindingMode.TwoWay,
    SourcePath = "Profile.Name", NullValue = "")]
private WidgetModel child;
```

这会绑定 `Child.Profile.Name`。中间模型必须派生自 ViewModel，路径只包含属性名，支持普通公共属性、生成属性及继承属性；不支持索引器、集合索引或方法调用。普通属性的 setter 应发布对应属性名或全部属性通知。生成器在编译期校验访问权限及转换类型，生成访问器和逐层通知订阅，运行时不解析字符串或使用反射。

替换 Child 或 Profile 后，绑定同步移除旧链订阅并投影新值。任意中间模型为 null 时，目标使用 `NullValue`；未声明时使用目标类型默认值，转换器不会收到该缺失路径。`NullValue` 必须是可隐式赋给目标类型的属性常量，仅用于含前向更新的路径绑定。实际叶属性的值为 null 仍交给转换器，和中间模型缺失分别处理。

TwoWay 默认用模型初始化控件，路径缺失期间忽略反向写入。路径恢复不会回放旧输入；转换或读取期间替换路径，会使正在处理的旧输入失效，即使随后恢复同一个模型也如此。OneTime 仅投影首次读取的值，不订阅后续模型替换；OneWayToSource 初始路径缺失时跳过初始源写入，路径恢复后只接纳新的输入。叶属性读写使用本次捕获的拥有者，避免在提交输入时再次遍历已变化的模型链。

需要手写自定义绑定时，可向 `BindingBuilder.Property` 传入 `BindingSourcePath<TViewModel, TValue>`：其读写委托访问捕获的叶拥有者，`BindingSourceMember` 依次提供属性名和拥有者选择委托。选择委托必须在中间模型缺失时返回 null，使用所属 UI 线程，沿用同一绑定会话的退订与换绑规则。

## 主线程状态更新

`ObservableObject` 和 `ObservableList` 归创建线程所有。生成属性通过 `SetProperty` 在字段修改前检查线程；自定义 setter 应先调用 `RequireOwningThread()`。自定义模型或集合从后台发出的通知会被绑定与列表适配拒绝，不会更新 Unity 控件。

初始化宿主后可在主线程取得 `host.Dispatcher` 并交给项目服务。后台工作完成时通过 `await dispatcher.InvokeAsync(() => viewModel.Count = count, cancellationToken)` 提交状态；若调用已在所属线程，委托会立即执行。派发回调执行前检查到取消时会跳过委托。

## 验证状态

2026-10-09 已在 Unity Hub 新建的 Unity 2022.3.62f3 工程完成设计验收，覆盖八项 Sample 导入、Basic 实体鼠标/键盘、Editor 定位和必要 IL2CPP 运行。完整标准见 [设计目标](../docs/DESIGN-GOALS.md)，后端、操作与证据范围见 [实现记录](../docs/IMPLEMENTATION-STATUS.md)。目标设备预算未指定，列表性能交付可复现基线；Player 退出的 allocator 提示未归因，不声明完整内存证明。

命令统一实现 `IUICommand`；立即完成的业务可传给 `new AsyncCommand(context => { /* 业务操作 */ })`，不需要独立同步命令。绑定入口要求可等待清理的激活作用域，以保证关闭或换绑时撤销命令来源并等待在途工作。

### 手动触发命令

只需触发、不等待结果时使用 `Execute()`，需要结果时使用 `ExecuteAsync()`：

```csharp
// 不依赖界面来源的普通命令
viewModel.RefreshCommand.Execute();

// 命令需要关闭界面或返回结果时，传入当前界面的 ICommandTarget。
viewModel.CloseBtnCmd.Execute(commandTarget, cancellationToken);

// 需要等待并检查结果
var outcome = await viewModel.RefreshCommand.ExecuteAsync(
    cancellationToken: cancellationToken);
```

`Execute()` 在当前线程立即进入同一个执行流程，不调度到后台线程、不延迟一帧，也不阻塞等待异步完成。AsyncCommand 实例和 IUICommand 接口变量均可使用；接口变量通过 MUI 命名空间的扩展方法调用，无需修改自定义 IUICommand 实现。

此入口自动观察失败结果及执行异常，并交给 UIErrors；取消和资格/并发拒绝不会作为错误报告。调用仍需遵守命令的 UI 线程约束，CanExecute、并发策略和执行状态与 ExecuteAsync 相同。

手动调用不会自动归属某个绑定会话，也不会推断界面来源；调用方需管理取消与必要的等待。调用 RequestClose（含带结果重载）的命令仍需有效的 ICommandTarget。按钮点击继续由原绑定会话执行，以保留会话的输入门控、取消和清理；外部业务关闭页面应优先使用导航关闭 API。

### 请求关闭与返回结果

```csharp
context.RequestClose();       // 不携带结果请求关闭
context.RequestClose(true);   // 携带结果请求关闭
```

CommandContext 和 Presenter 的 ActivationContext 均提供这两种入口。它们只发出请求，不等待页面完全关闭；关闭守卫、结果发布和清理仍由宿主原有协议处理。带结果关闭不是单独设置结果，也不意味着请求已经获准。带结果关闭统一使用 `RequestClose(result)`，不提供 `Complete(result)` 旧入口。

### Presenter 命令

需要业务协调的操作可直接声明在 `partial Presenter<TViewModel, TArgs, TResult>` 上，使用与 ViewModel 相同的 `[Command]` / `[BindCommand]`。不需要在 ViewModel 中声明 Handler，也不需要在 OnPrepareOpen/OnClose 中手写转发或订阅。

```csharp
public partial class ShopPresenter : Presenter<ShopViewModel, ShopArgs, bool>
{
    private bool CanBuy => ViewModel.Quantity > 0;

    [Command(CanExecute = nameof(CanBuy))]
    [BindCommand("Buy", nameof(MUI.UGUI.ButtonElement.Clicked))]
    private async System.Threading.Tasks.ValueTask BuyAsync(CommandContext context)
    {
        var model = ViewModel;
        var productId = model.SelectedProductId;
        var quantity = model.Quantity;
        await shopService.BuyAsync(productId, quantity, context.Token);
        context.Apply(() => model.Message = "购买成功");
    }
}
```

上例的业务服务由项目构造注入，模型和参数类型由项目定义。生成器在 Presenter 上生成 `BuyCommand`；按钮通过当前绑定会话执行它，命令实例不写入共享 ViewModel。同步 void、Task、ValueTask、CommandContext、CancellationToken 以及并发选项与 ViewModel 命令一致；禁止 async void。

页面和子视图宿主都会自动接入。关闭、停用和换绑沿用会话的取消、解绑与清理；换绑预览也校验 Presenter 命令，但不会执行命令或 CanExecute。异步返回后用 `context.Apply` 写入状态，并遵守其 UI 线程要求。命令仍在执行时能否完成换绑，沿用宿主已有的在途命令规则。

CanExecute 可读取当前 ViewModel；模型属性通知和命令执行状态会自动刷新按钮。条件只依赖 Presenter 私有字段或外部服务时，改变条件后调用生成的 `BuyCommand.NotifyCanExecuteChanged()`。框架不会自动观察普通业务字段。

同一控件事件不能同时由 ViewModel 和 Presenter 命令接管，生成器报告 MUI001；运行时会话还会检查真实 Element 上的事件和属性写入冲突。基类 Presenter 的命令沿用继承绑定，不重复订阅。`CommandManifest` 提供当前 Presenter（含基类）的命令清单，模型工厂的 Manifest 仍只描述模型绑定。

使用 Prefab 自动绑定时，明确选择（UIPage.PresenterType）或唯一匹配的 Presenter 手写命令优先于同目标的自动命令。多个 Presenter 候选无法确定时不会猜测；请明确指定类型或移除该目标的自动命令声明。Presenter 仍须由路由/子视图工厂选用；仅声明类不会替换手动路由的 Presenter。自定义绑定上下文应继承 `BindingContext<TViewModel>` 才能接入实例命令，直接继承非泛型基类的上下文会明确报错。

Navigation 示例的 Confirm 现位于 PagePresenter，Close 仍位于 PageViewModelBase，展示两种命令在同一页面共存。

诊断接入：通过 `UIErrors.Sink` 接管错误输出；操作结果与导航事件/追踪的 `DiagnosticId` 可关联同一次异常，也可使用 `UIErrors.GetDiagnosticId(error)` 查询。查询不会触发报告。同一异常实例只报告一次，单项聚合复用原因标识，多项聚合逐项报告实际失败；重试产生的新异常有独立标识。错误出口和观察者抛错不打断其他观察者或框架清理。

在错误出口调用 `UIErrors.GetDiagnosticContext(error)` 可读取宿主、句柄编号、路由、操作与原始阶段，关闭时间线追踪也保留这些字段。上下文仅含值与有界字符串，按异常弱键保存，不保留页面、模型或资源。项目适配器可使用 `UIErrors.BeginContext(...)` 提供外部阶段，或 `AttachContext(...)` 在传播前补充位置；传播只填补缺失字段，不重写原始故障位置。Unity 默认出口将标识、上下文和异常栈合并为一条错误日志。

### 开发期 API 与配置

当前只维护一套 API 和配置格式，不提供弃用别名、旧序列化字段映射或自动升级。控件命名规则使用 `Expression`、`ComponentTypes` 和 `DefaultBindingProperties`；页面目录直接保存完整 Assets 路径。页面预设直接声明角色与输入策略，覆盖字段使用当前 `PagePolicyFields`。旧配置需重新设置，旧自动绑定需重新生成；仓库 Basic 示例资产已按当前字段更新。手写控件契约和 Element 组件适配器是仍支持的功能，不属于历史迁移入口。

## 输入转换与校验

`IValidatingBindingConverter<TSource, TTarget>` 的 `TryConvertBack` 返回 `BindingConversionResult<TSource>`。无效输入返回 `Failure(message)`，保留原模型值和控件草稿；成功返回 `Success(value)`，模型 setter 完成后重新投影实际值，支持裁剪及规范化。已有 `IBindingConverter` 的反向转换异常也转为校验失败，取消异常仍传播。目标读取、模型 setter 和校验状态 setter 的异常属于实际绑定故障。

在反向 Bind 声明中设置 `ValidationProperty = nameof(AmountValidation)`，指定一个可写的 `BindingValidationState` 模型属性；它也可由 ObservableProperty 生成。默认状态有效，`IsValid` 和 `Message` 可用于派生可绑定文字或命令资格。校验状态目标必须与数据属性不同，且只有一个反向写入者；生成器在编译期检查。Settings 示例的 PlayerNameConverter 和 NameError 展示完整接入。

手写绑定通过 `BindingBuilder.Property` 的 `tryConvertBack` 与 `writeValidation` 使用同一机制。双向绑定先由模型初始化控件，提交后开启反向写入并重置校验状态；OneWayToSource 初始无效值只发布校验失败。后续模型变化重新投影并清除旧输入错误，解绑后旧输入不能发布状态。校验发布可能同步触发业务回调，回调不得等待自身绑定清理。

## 资源键与直接对象

ImageElement.Sprite、RawImageElement.Texture、TextElement.Font 和 GraphicElement.Material 可在资源键槽存在时直接赋值。赋值立即使旧请求失效，成功替换显示后归还旧凭证；异步归还仍由同一激活生命周期跟踪。直接 null 清空，直接对象只借用，不自动销毁。调用方必须覆盖整个显示期间的持有权；即使赋予槽已显示的同一对象，也须有独立持有权。

直接对象赋值将请求键和显示键都置空，非空对象的 Snapshot.State 为 Displayed，清空为 Empty；查询实际对象使用控件属性。被取代请求即使忽略取消，其迟到凭证也归还。Snapshot.CleanupFailure 和 CleanupFailureCount 保留旧归还、回滚及最终清空失败的诊断，不改写新资源的显示结果。释放失败仍使最终生命周期清理失败，重复等待不会自动重试。

旧凭证的责任属性抛错或返回 null 时，仍执行一次真实归还；属性错误保留于清理诊断，不覆盖新资源的显示状态。归还失败采用不可安全重试的回退责任，不能根据内部叶凭证或原生对象已销毁猜测外层 callback 完成。加载异常附带 `ResourceLoadException.CleanupCompletion` 时，操作继续等待实际回滚；失败任务保存为独立未知责任，不重新执行后端回滚。

## 预加载与容量

调用 `await pages.PreloadAsync(route, cancellationToken)` 准备资源，不创建页面、绑定模型或接纳输入。同一资源身份与提供方内容版本合并底层准备，`ReusedReservation` 表示复用了预留，并不表示资源已经就绪。失败后新请求可以重新获取；版本变化使旧候选失效，迟到凭证由旧请求归还。

每个调用方独立持有准备资格。首次请求取消返回 `Cancelled`，合并观察者取消自己的等待返回 `WaitCancelled`；单个消费者取消不停止其余消费者的获取，只有最后一个消费者在驻留提交前取消才请求底层停止。`Ready` 后资源由批次作用域持有，原调用方再取消不释放驻留。后台取消时，UI 等待不因解除取消登记而同步等待项目回调；已经启动的底层取消仍由原准备跟踪到结束。

`PreloadReservationCount` 包含准备中、驻留、等待实际归还和未确认失败的预留。迟到凭证不发布为 Ready，但归还完成前仍占容量。凭证明确支持安全重试时，叶责任显式确认后恢复额度；读取数量与诊断只观察已捕获的责任，不访问项目责任属性或重放清理。首次清除失败及历史错误保持原值，未知部分归还不能仅凭原生对象消失释放额度。项目取消回调抛错同样保留未知责任及容量，不自动重放回调。

`await pages.ClearPreloadsAsync()` 使接纳时的预加载失效，并等待当时已在途清除与相关迟到凭证归还；后来接纳的预加载属于新批次。重复调用继续等待其各自接纳时的在途批次，实际归还失败会使等待失败。`ClearInactiveContentAsync()` 同时清理页面缓存，并在这次闲置内容清除结束前拒绝新预加载；`ShutdownAsync()` 停止新工作，等待本宿主持有的资源，不销毁项目共享提供方。

## 清理责任与显式重试

自定义通知访问器或绑定最终回调抛错时，框架保留实际回调与旧通知拥有者，报告未确认清理。路径切换时退订失败不会在最终解绑时悄悄丢弃，未知 remove/finalizer 不再次执行，也不会据此允许父 View 归还。对应责任在账本中使用 `Binding.Subscription`/`Binding.Finalizer` 标签，并沿用绑定的宿主和页面身份；这些未知回调不提供安全重试。

`CleanupResponsibility` 记录归还责任的稳定 Id、拥有者标签、状态、错误、诊断上下文与尝试次数。状态区分未启动的 Retained、在途 Pending、失败或归还状态不明的 Failed，以及已确认的 Completed。`CleanupRegistry.CaptureSnapshot(maxEntries)` 按上限读取在途或失败记录，`UnconfirmedCount` 返回总数；完成记录立即移出全局账本，不长期保存成功历史。账本独立于 UIHost 组件，场景或作用域结束后仍持有未确认责任及回调。

`PageManager.CaptureSnapshot()` 提供稳定 `HostId`、`UnconfirmedCleanupCount` 和有界 `CleanupResponsibilities`；使用双参数重载可分别限制实例与责任采集数量，截断由 `CleanupResponsibilitiesTruncated` 标明。`HasCleanupFailure` 表示页面或缓存清理曾失败；显式重试成功后当前责任数量减少，历史失败及已交付的关闭结果保持原值。MUI Dashboard 分别显示这两类信息，不从快照启动重试。

组件销毁后可用保存的 HostId 调用 `CleanupRegistry.GetUnconfirmedCount(hostId)` 或 `CaptureSnapshot(hostId, maxEntries)` 查询当前责任；`Guid.Empty` 只查询没有宿主身份的责任，不是全部宿主的通配符。账本选择和每项状态读取不冻结异步完成，采集期间完成的项可能显示 Completed，下一次采集消失。责任登记保存值身份作备用；合法移交后首次归还采用实际所有者的上下文，公布到账本后不随重试改变归属。

`AcquiredResource<T>`、`AcquiredView` 和 `AcquiredPreload` 公开同一 `CleanupResponsibility`。后端能保证完整释放回调可幂等重复执行时，构造凭证显式设置 `supportsIdempotentRetry: true`，并用 `owner` 标明资源或池身份。回调必须记录部分完成的步骤：已经扣除的引用、已归还的池对象或已经请求的销毁不能再次执行。默认不允许重试，失败不能推断为未释放。

项目选择失败责任后调用 `await CleanupRegistry.RetryAsync(id)`，或直接使用凭证的 `CleanupResponsibility.RetryAsync()`。并发重试共享当前尝试，成功更新同一记录并解除其持有；已确认完成的责任不再次执行释放。线程约束沿用凭证的 `releaseThreadId`，线程拒绝不接管新的尝试。重试任务由账本持有，调用方负责观察其结果；没有自动重试调度或丢弃失败记录的入口。

重复 `DisposeAsync` 始终观察首次清理结果。显式重试成功不改写已经交付的关闭、Shutdown 或 LifetimeScope 失败任务，也不复活页面、作用域或输入；判断当前未完成责任使用账本快照。控件 Snapshot 的 CleanupFailure/Count 继续表示历史上已记录的错误。

`LifetimeScope.IsCleanupConfirmed` 查询当前实际责任是否均已确认，区别于首次释放任务是否失败。子作用域和已登记凭证的安全重试可使确认状态变化；没有责任记录的回滚错误或取消回调异常仍按未知处理。视图归还前检查激活和实例作用域，未确认时在账本中保留 `PageInstance.DependencyRelease`。项目先显式处理允许重试的叶责任，再重试控件/模型持有者和视图的剩余步骤；外层检查不自动重试未知后端。已移出导航实例列表的失败页面仍占一个清理名额，同页多个责任不重复计数；没有页面身份的责任逐项保守计入，确认归还后才恢复新请求准入。

`LifetimeScope.OnDisposeAsync` 也可显式声明回调的幂等重试、拥有者标签和释放线程。未声明能力的普通清理回调、外部自定义凭证和同步清理按不可安全重试处理，失败仍保留对象与回调。自定义凭证若自己管理责任，应实现 `ICleanupResponsibilitySource` 并让释放入口更新该记录，框架不会再为它建立第二份归还责任。

项目加载操作在取消、提交失败或代际失效后归还候选时，可调用 `await CleanupRegistry.ReleaseAsync(resource, owner, lifetime)`。此入口捕获一次稳定责任，失败后把实际确认条件登记到所属作用域；安全叶责任显式恢复后，`IsCleanupConfirmed` 可更新，首次释放任务仍保留原错误。调用必须在该作用域跟踪的操作退出前完成，不能在操作已经结束后用它补挂新的归还工作；正常交付的资源通过 `lifetime.Own(...)` 接管。三参数入口已登记失败，调用方只传播或报告错误，不再以无确认条件的 `RecordCleanupFailure` 重复登记。

自定义 `IBindingRebindTarget` 返回的准备目标也遵守同一清理契约：框架逆序释放已取得的候选，一项目标失败仍继续清理其他目标；失败目标及其回调由责任账本保留，并阻止所属页面 View 提前归还。默认不重复执行未知清理；目标能保证完整回调幂等时，可实现 `ICleanupResponsibilitySource` 公开自己的责任。显式恢复先处理目标的叶责任，再确认 `BindingRebindPreparation` 和外层视图责任。外层确认不重新执行目标回调，也不改写首次失败结果。自定义 `IBindingRebindPreparation` 返回的准备对象同样由页面激活持有清理责任。

参数更新和 Presenter 换绑返回的候选也登记到所属激活；未确认清理时保留父子 View。责任属性抛错或返回空值时，框架仍执行一次真实清理并保留登记异常；实际清理失败则保留不可安全重试的回退责任。直接调用子视图准备、参数更新和换绑 API，以及子视图生命周期回调，均沿用父页面的宿主、页面和路由身份。

列表拥有的隐藏候选节点只有在子视图借用凭证确认归还后才能最终收尾。节点开始收尾后不再接受新子内容；原生收尾自身抛错也保留节点及责任，不重复未知副作用。列表准备对象显式恢复仅核对已登记的叶责任和节点状态，清理后解除来源与模型快照引用。

UGUI `View` 的最终控件责任同样稳定：它在初始化控件前登记责任，初始化失败也沿同一逆序清理。最终清理立即撤销视图资格；该视图已有的子激活和资源激活尚未确认时，保留控件和原生引用，先由其原拥有者排空激活。`View.DisposeAsync()` 只结束最终控件持有，不销毁 GameObject；重复调用保持首次结果，安全恢复后确认 `View.CleanupResponsibility`。

活动 View 被外部销毁时，先使原生身份失效并通知拥有者故障关闭，再启动最终清理，避免输入通知回调等待自己正在执行的责任。原生销毁兜底异步观察拥有者已经启动的激活清理；慢归还完成前继续保留凭证，不把同步 OnDestroy 返回视为资源已归还，也不隐式重试未知清理。缓存 View 外部销毁在下一次缓存维护或请求时淘汰。

原生节点拥有者另捕获节点内各 View 的最终责任，各 View 仍只清理自己的 Element 扫描边界。Prefab 凭证等待最终 View 责任及实际 Destroy 完成后才报告归还；列表节点也先确认包装 Element 和子 View 的最终监听，不能以借用凭证归还代替最终清理。恢复依次确认叶责任、Element、View、ViewHierarchy，再确认列表节点及列表最终回调，或 Prefab 实例凭证。已确认层级不重复执行，首次关闭结果保持原值。

`PrefabViewProvider.DisposeAsync()` 等待提供方 staging 根实际销毁；现有同步 Dispose 只启动并观察原生兜底清理。退出立即拒绝新获取，但有未确认回滚实例时保留 staging 和清理责任。项目提供方可用 `PrefabViewFactory.CreateStagingCleanup(root, owner, endAdmission)` 管理同一责任：第一次调用结束准入，只有子实例全部实际归还才请求根销毁。节点收尾和结束准入的未知失败不重放。Resource Integration 示例在原生实例确认后才归还加载引用，显式恢复不会重复扣减已尝试的后端引用。

UIHost 的 `ownsProvider: true` 将提供方最终释放交给宿主；默认资源加载提供方同样由宿主持有。退出接纳后，`UIHost.Provider` 责任保留导航及迟到准备的等待、依赖确认和最终提供方释放。组件销毁后用保存的 HostId 查询 CleanupRegistry，仍能看到在途或失败责任。只有当前导航持有权全部确认后才调用提供方释放；历史清理错误不阻止这一步，但首次 Shutdown 失败结果保持不变。取消 Shutdown 等待只结束调用方观察，不停止该责任。

导航叶资源释放失败时，先显式恢复允许重试的叶责任，再通过 `CleanupRegistry.RetryAsync(id)` 确认 `UIHost.Provider`；依赖仍未确认时不会调用提供方。提供方实现 `ICleanupResponsibilitySource` 时，宿主捕获其稳定责任，只调用真实释放入口一次；提供方叶责任显式恢复后，外层重试只确认结果，不重新执行项目回调。没有该契约的同步或异步提供方使用不可安全重试的 `UIHost.ProviderRelease` 回退责任；部分释放抛错后，外层可重试资格只允许核对依赖，不代表允许重放未知释放。责任属性抛错或返回 null 时同样保留一次真实释放及属性错误，不能以内部叶状态猜测外层 callback 已完成。提供方释放回调直接等待本宿主 Shutdown 会立即失败。

`ownsProvider: false` 的项目共享提供方继续由项目持有；宿主只归还自身取得的凭证，不释放该服务。正常销毁宿主前仍应显式等待 `ShutdownAsync()`；OnDestroy 仅共用同一次退出作为兜底，不承诺进程结束后继续清理。上述契约不能防止 Unity 直接销毁整个原生层级，独立责任仍用于保存和诊断未确认的逻辑归还。

绑定会话及 View 资源激活保存宿主、页面句柄和路由的值快照。导航回调结束后的模型通知、命令执行和资源换键仍沿用所属页面身份；前向、反向、路径通知、命令刷新、加载、原生赋值、通知、回滚及归还使用各自阶段。绑定故障保留异常传播并向错误出口报告一次；输入转换拒绝和加载取消仍属于可预期分支。错误上下文不持有 View、宿主组件或模型。

无法确认原生 setter 失败后的引用时，槽冻结并暂停相关 Graphic 渲染，保留可能仍被引用的凭证；最终清理先清空原生引用再归还。框架不会自动恢复该控件的渲染。新的资源拥有者须等原槽成功清理后配置；TMP 换 FontAsset 可能间接替换材质，因此仍拒绝在材质槽持有期间执行该操作。

## 固定容量内容

页面缓存只持有解除绑定并重置的 View 凭证，每次打开仍创建新的模型、Presenter、实例作用域、激活、句柄和结果。正常关闭清理模型与 Presenter 的实例资源后才尝试缓存，外部传入模型仍只借用；故障关闭、强制关闭与宿主退出直接归还。缓存命中不重复获取 View，但会重新执行当前激活所需的资源配置；重置或重新配置失败的 View 淘汰并归还，不继续复用。

渲染器通过 `ICacheableView.ResetForCache()` 清除旧模型引用、焦点、输入与资源上下文；凭证通过 `ICacheableViewAcquisition` 显式保证必要后端依赖能延长至实际归还，并通过 `PrepareForReuse()` 注入当前配置。`AcquiredView` 默认 `supportsCaching: false`，项目后端满足此保证才显式启用。UGUI View 和 PrefabViewProvider 已支持；Prefab 的配置回调在缓存命中后再次执行，须允许重复配置。Presenter 或模型不会跨关闭保留。缓存未接管任何旧实例的 LifetimeScope 或业务资源。

构造 PageManager 时传入 `maxCachedEstimatedBytes` 后，PageDefinition 必须给出 `estimatedRetainedBytes` 才能进入缓存；未知估算和超出总预算的 View 直接归还。`ReservedCacheEstimatedBytes` 包含缓存目录、在途淘汰与未确认失败的估算额度；移出目录不提前归还额度。`FailedCacheEstimatedBytes` 仅反映当前未确认的失败额度，明确支持安全重试的凭证确认后恢复预算，首次清理错误及 `HasCleanupFailure` 保持原值。两项查询不启动清理、不重读项目责任属性；仅观察已捕获责任。原生节点消失不能代替未知部分归还的确认。

自定义 UGUI Element 可覆盖 `OnResetForCache()`，解除自己保存的模型、参数和活动引用。此钩子在旧业务实例已清理、View 隐藏且不可交互时同步调用，不能启动新工作；抛错时整个 View 淘汰并执行最终释放。

少量已有槽位或显式挂点可使用 [固定槽位列表](slot-lists.md)，共用子视图生命周期与候选换绑。

## 屏幕布局与场景宿主

缩放使用 Unity CanvasScaler；列表尺寸、间距、滚动偏移和定位误差使用 Content 的局部 Canvas 单位。`SafeAreaFitter` 放在屏幕根 Canvas 的直接子 RectTransform 上，把安全区与可选键盘区域的屏幕像素转换为目标锚点，不再乘一遍 Canvas 缩放。Overlay 与明确配置 worldCamera 的 Camera Canvas 均可使用；Camera 视口参与裁剪。Unity 把缺相机的 Canvas 实际降为 Overlay 时，按其有效模式处理。World Space、非主显示及相机 RenderTexture 的屏幕安全区映射不在该适配器保证范围内。

调用 `SetSafeAreaOverride(rect)` 可注入平台安全区；默认读取 Screen.safeArea。启用 `AvoidKeyboard` 后，通过 `SetKeyboardAreaOverride(rect)` 提供以屏幕左下角为原点的键盘像素矩形，空矩形表示无遮挡，`ClearKeyboardAreaOverride()` 恢复读取 Unity 提供的区域。适配器选择键盘周围最大的无重叠矩形；窗口或方向变化时平台提供方须更新覆盖值。`Refresh()` 可同步应用配置，列表随后沿原生布局变化保持阅读锚点。

UIHost 默认随场景销毁。项目切换场景前等待 `ShutdownAsync()`，确认资源归还后再卸载；框架不接管场景加载。持久宿主由项目显式调用 DontDestroyOnLoad，并把 View 挂在同一个兼容持久层级，EventSystem 也由项目明确管理。持久缓存只保存独立原生 View 凭证，重新激活注入当前加载器，不能保留待卸载场景对象或旧业务激活。

Unity 2022.3.62f3 的真实窗口 IL2CPP 观察已覆盖上述两种 Canvas 模式、CanvasScaler、1000×700/700×1000 分辨率、安全区/键盘坐标、跨场景活动/缓存内容及 Unity Resources 后端归还。配置和完整证据见 `docs/IMPLEMENTATION-STATUS.md`；该结果不代表任意项目后端、实体输入或目标设备性能达标。

## 页面向导与 Editor 校验

`Tools/MUI/页面向导 (Page Wizard)` 从全局配置读取全部页面行为预设（包括自定义项），通过下拉框选择预设，下方仅显示当前选中项继承项目默认规则后的实际层级、遮挡、输入、焦点、模态及返回策略。无配置时读取框架内置预设列表。预设删除或改名后需重新选择，不能沿用失效名称生成代码。向导自动使用全局 MUISettings 预览策略；生成代码通过 `ViewModelDefinition.CreateDefinition()` 自动读取全局配置，也可传入 `ViewModelDefinition.CreateDefinition(overrides: overrides)` 覆盖该页面的具体行为。不接收输入的预设生成无关闭按钮和射线目标的 Prefab，由业务保存打开句柄并显式关闭；接收输入的预设生成关闭按钮。预设解析为现有 PagePolicy，不引入运行时页面类型；默认只生成 ViewModel 源码，不要求 Presenter。向导将预设记录在 `UIPage.PolicyPreset`，编译时由 Source Generator 生成 `ViewModelDefinition.CreateDefinition()`，不再创建独立的 `Page.cs`。已有目录可复用，目标文件已存在时拒绝覆盖。

新建 UGUI/TMP 页面将标题和关闭按钮的自动绑定声明、初始标题及 Inspector 属性选择保存在 Prefab 中。编译器生成 `Title`、`CloseCommand` 和绑定代码；ViewModel 仅保留 `partial void OnClose(CommandContext context)` 的关闭逻辑，无输入页面不生成关闭命令。已有 Prefab 和自定义 Element 模板仍保留显式绑定，不改写已有资产。普通命名空间直接输出为 `Game.UI`，仅 C# 关键字段使用 `@` 转义。

业务通过 `InventoryPage.OpenAsync(...)` 打开页面。引用 UGUI 时始终生成 `InventoryViewModelDefinition.CreateDefinition()`，省略 PolicyPreset 时读取项目默认预设；特殊页面可以显式指定预设。Create 为显式构造路由的底层 API，支持自定义工厂、策略和依赖。

为不接收输入的预设向导新建的页面保留 View 根节点，将背景、标题和按钮放入拉伸的 `Content` 子节点，默认设置为“隐藏后如何处理 → 停用内容”。仅当页面完全不可见时停用 Content；可见但禁止交互、以及仍需显示的退出转场不会因此停用。重新显示时恢复框架先前停用的节点，不激活原本已停用的内容。已有 Prefab 和手动添加的 View 保留原有行为，不自动迁移。

需要隐藏后继续更新的页面可选择“保持内容运行”。停用内容会触发原生启停回调，并停止挂在该子树上的协程；页面的数据加载与业务生命周期应由 Presenter 或服务管理，不能假定协程会在重新显示时自动续跑。

复用已有 Prefab 时，必须移除 Selectable 并关闭所有 Graphic 的 Raycast Target；向导只生成业务源码，不修改原资产。类型化参数/结果和可选 Presenter 沿用同一生成绑定及导航入口。

项目通过 `UIBuildValidation.RegisterCatalog(id, collect)` 登记每个导航配置，用 `UIBuildCatalog.AddPage(route, prefab, manifest)` 提供页面及依赖闭合。手动 `Validate()` 和构建前检查每次重新采集全部目录，不依赖增量结果缓存。未登记目录仅提示没有覆盖；已登记目录的错误阻断构建，并在报告中包含路由及 Prefab 路径。

Prefab 保存、导入及其依赖资产变化按路径合并，在 Editor 空闲后校验受影响目录，复用相同规则。脚本或程序集变化、资产删除/移动及域重载重新采集全部登记目录。编译、导入或 Play Mode 期间保留待检查标记，结束后补跑；队列最多保存 2,048 个路径，达到上限改为完整检查。检查不初始化业务模型、不写资产，也不触发递归导入；未变化的增量报告不重复输出。

打开记录由框架自动维护：显式打开的页面加入 `PageManager.History`，关闭后移除，仅随父页面打开的依赖不单独记录。此列表用于查询和诊断，不保存页面状态，也不参与返回目标选择。原 `EnterHistory` / `enterHistory` 配置已移除，调用方无需再设置。

### 页面生成目录

统一设置 Inspector 中 MUIEditorSettings 的“页面生成目录”分别配置 Prefab 与脚本的完整项目路径，支持 `{PageName}`，不共用根目录。例如：

- Prefab：`Assets/LocalResource/{PageName}/`
- 脚本：`Assets/Scripts/UI/{PageName}/`

Inventory 会生成到 `Assets/LocalResource/Inventory/InventoryView.prefab` 和 `Assets/Scripts/UI/Inventory/`。路径以 `Assets/` 开始，接受末尾斜杠；输入的 `assets/` 会统一为 Unity 的 `Assets/`。目录规则直接保存完整 Assets 路径，不转换旧版相对目录。

默认规则仍保持 `Assets/{PageName}/Resources` 和 `Assets/{PageName}/Scripts`。缺失目录自动创建，已有文件不覆盖；预览、程序集检查和实际写入使用同一套解析路径。

Resources 子目录中的 Prefab 自动生成对应资源键；例如 `Assets/Resources/UI/Inventory/InventoryView.prefab` 的键为 `UI/Inventory/InventoryView`。Resources 外的 Prefab 默认键仍为页面名加 View，项目需通过资源加载器映射到实际资源。关联已有 Prefab 时保留原位置。

### 页面资源加载

`host.Initialize()` 默认按页面的 `ViewResource.Key` 从 Unity Resources 加载 Prefab（相对 Resources 的路径，不含扩展名）。页面向导新建的 Prefab 放入 Resources 文件夹。`Version` 用于提供方的资源身份区分，不自动拼接到加载路径。

已有资源系统通过 `host.Initialize(resourceLoader: loader)` 一次接入 `IResourceLoader`；页面和子控件使用该加载器，无需在 UIHost 重复登记。加载器由项目持有，应在宿主 Shutdown 完成后释放。高级项目仍可传入 `IViewProvider`，它与 `resourceLoader` 参数互斥。

迁移旧场景时移除 UIHost 本地列表依赖，将 Prefab 放入对应 Resources 路径，或接入项目加载器。基础示例需重新导入。资源接入示例中的 `LoadedPrefabViewProvider` 和 `UnityResourcesLoader` 现位于 `MUI.UGUI` 命名空间；重新导入旧示例时应清除旧版本脚本。构建校验目录仅用于校验，不参与运行时资源解析。

### 全局模态遮罩

在全局 MUI Settings 的“模态遮罩”中设置默认遮罩预制体和底色。View 勾选“使用全局默认遮罩”后共用此配置，取消勾选则使用本页的样式资产或颜色。页面向导新建的 View 默认勾选；已有 View 保留原配置。该选项只控制外观，是否模态仍由路由策略决定。

预制体根节点需为 RectTransform，可使用 Image/RawImage，不可包含 Canvas、Renderer、Selectable 或 View。框架会将其铺满宿主区域并独立处理输入阻挡。底色绘制在预制体下方；只需要预制体自身效果时将底色透明度设为 0。没有预制体时显示纯色遮罩，没有全局配置时使用黑色、45% 不透明度。配置在遮罩实例创建时读取，已创建实例保持原外观。

### 页面内部自动排序

无需在 View Inspector 登记排序目标。框架自动收集页面内开启 `overrideSorting` 的子 Canvas 及 Renderer（包括 inactive 节点），按首次发现时的 Sorting Layer 顺序和 sortingOrder 分组，再映射到页面排序范围内。相同原始排序值使用相同偏移，普通 UI 仍按 Hierarchy 顺序显示。偏移 0、1 留给遮罩和页面主体；自动收集的对象从 2 开始，位于主体之上。要让按钮位于特效上方，可将按钮放入独立子 Canvas 并设置更高的原始 sortingOrder。

旧资产的显式偏移与 `RegisterRenderOrderTarget(s)` 继续生效；自动分配避开这些保留值。运行时新增特效后调用 `view.RefreshRenderOrderTargets()`，无需逐项登记。请先在 inactive 节点下创建、完成刷新后再显示，避免未排序的特效提前露出。自动排序不会每帧扫描排序或重新读取已经接管对象的作者排序。

“运行诊断”中只读显示页面排序范围及对象最终 Order。排序层数超出页面跨度时仍会报错，需要增加跨度；普通 Renderer 与 Overlay Canvas 混排、启用的 SortingGroup 仍不支持。

### Inspector 自动识别页面契约

向导把 ViewModel 完整类型名与程序集保存到 Prefab 的 View 关联元数据。View Inspector 提供类型选择，与自动绑定声明共用同一份元数据；没有绑定成员也可以单独保存关联。

Inspector 只读取 Prefab 的显式 ViewModel 关联，不使用 GUID、Resources 路径或名称兜底。类型改名或程序集改变后会提示关联失效，需重新选择。动态资源加载和指定 ViewModel 实例仍由类型化路由及绑定契约决定，编辑器关联不会强制运行时实例类型。

### 原生控件绑定与代码双向声明

全局 MUI Settings 的“控件绑定规则”配置前缀与类型，默认 `Img_` → Image、`Txt_` → Text/TMP、`Btn_` → Button。前缀后的部分作为绑定名称，例如 `Img_Icon` 对应 `Icon`；不带前缀的装饰节点不会收集。每个前缀可多选组件类型；每次取最长前缀并继续解析后续前缀。规则支持修改、添加和删除，名称必须满足生成代码的 C# 标识符要求。

View 的“绑定与校验”统一展示规则扫描结果及 ViewModel 已有 `[Bind]` / `[BindCommand]` 声明。名称、引用、状态、定位、源码放在同一行区域。已有有效引用优先保留；新增目标按前缀、绑定名称和类型自动匹配，不会靠猜测消除重名。代码声明在检查器初始化（含编译后重建）或切换契约时自动同步唯一匹配，可撤销；不在重绘时写入。缺失目标可以拖入并立即记录，已有非空引用不会被自动替换。规则扫描候选在生成绑定时记录，不再提供独立的保存引用按钮。Element 组件契约只读显示，不隐式删除组件或改写源码。

点击“生成绑定”保存控件引用和自动声明，并请求重新编译。匹配到的文本默认生成同名字符串属性，图片生成 Sprite 属性，滑块/开关/输入框/下拉框生成 Value 对应的属性，滚动区域生成 Vector2 属性。输入类默认双向绑定，显示类默认单向绑定。全局命名规则可覆盖属性或 Action 事件名、绑定方向；属性类型从适配器推导，自定义组件需要注册适配器。

声明保存在 Prefab，`.AutoBindings.g.cs` 和 `.Bindings.g.cs` 由编译器生成，不向 Assets 写 C# 文件，也不修改手写 ViewModel。编译后 Presenter 可直接写 `ViewModel.Title = "背包";`、`ViewModel.ItemList = new Vector2(0, 1);`。在 IDE 对属性转到定义可查看生成文档；Unity 的源文件打开接口不能直接打开编译器虚拟文档。生成日志会列出文档名、成员和调用方式；“已请求编译”不等于编译成功。

按钮 `Btn_Close` 自动生成 `CloseCommand`，业务只需在 ViewModel 分部类实现钩子（不再写 Command/BindCommand 声明）：

```csharp
partial void OnClose(CommandContext context) => context.RequestClose();
```

没有实现钩子时点击不执行业务操作；可直接手写 `[Command]` / `[BindCommand]` 接管该控件。已有手写绑定按目标属性或事件优先，包括转换器和路径；同一控件的其他属性仍可自动生成。重复控件名、成员名冲突和不支持的适配属性会报错。旧业务中的 UI/Controls 直接调用需要改成更新 ViewModel。

在 View Inspector 展开控件后，点击“添加属性”搜索适配器的可绑定属性或事件。默认只列出已选项，可以编辑 ViewModel 名称、绑定方向，或移除不需要的属性。例如 Title 选择 text → Title、color → TitleColor、fontSize → TitleFontSize；类型沿用实际组件（Text.fontSize 为 int，TMP.fontSize 为 float）。属性名按当前声明精确匹配，不再自动改写旧属性别名。代码声明单独显示并可定位源码。选择保存在 Prefab，支持 Undo；修改选择后点击“生成绑定”更新编译器文档。生成时读取文本、布尔值、int/float、Color 和 Vector2 的控件当前值作为初值；资源引用等其他类型仍需业务赋值。移除已生成成员后，业务中仍使用该成员的代码需要同步调整。

属性选择器列出原生组件的全部公开实例属性（含继承属性），支持按名称、属性类型和声明类型搜索，多个关键词用空格分隔；只读属性可选择为实时读取入口；索引器和弃用成员默认隐藏，可通过“显示不可用 / 已弃用属性”查看原因。仅选中的原生属性生成强类型属性包装，包装实例由框架缓存并随 View 释放，无需业务 new，也不使用运行时属性反射。全局命名规则可设置默认属性列表（逗号分隔，如 text, color），仅影响未保存选择的控件。

原生属性默认支持单向/首次赋值。Slider、Scrollbar、Toggle 的数值、UGUI/TMP 输入框文本、下拉框数值、ScrollRect 归一化位置复用适配器的通知映射，可以双向绑定；生成 setter 沿用适配器的无通知赋值，事件输入沿用门控。其他自定义组件属性可以自动生成单向包装，反向通知仍需属性扩展或自定义适配器提供，不凭空监听原生属性变化。

只读属性生成 getter，例如 `ViewModel.TitlePreferredWidth` 每次读取当前计算值，`ViewModel.TitleRectTransform.sizeDelta = ...` 可修改返回引用对象的内部属性，但不能替换 rectTransform 引用。此入口仅在绑定会话有效时可用，解绑后会清除读取委托，提前或解绑后访问会抛出明确异常。它不保存独立状态、不自动产生变化通知；返回值类型（结构体）的副本不能用于改写原对象。不要将此类 UI 引用长期缓存到业务对象。

自动生成目前支持 Assets 下的 Prefab 和非嵌套、非泛型 ViewModel；同一模型只能由一个 Prefab 提供自动声明。Prefab 是编译输入，必须提交到版本控制。修改规则或引用后再次点击生成；仅保存场景不会更新自动声明。

手写绑定可使用跨后端公共契约；原生属性选择器只展示原生属性名，避免重复：

```csharp
[ObservableProperty]
[Bind("Title", nameof(ITextElement.Content))]
private string title;

[ObservableProperty]
[Bind("Volume", nameof(IValueElement<float>.Value), bindingMode: BindingMode.TwoWay)]
private float volume;

[ObservableProperty]
[Bind("ItemList", nameof(MUI.UGUI.IScrollElement.NormalizedPosition), bindingMode: BindingMode.TwoWay)]
private UnityEngine.Vector2 listPosition;

[Command]
[BindCommand("Close", nameof(IButtonElement.Clicked))]
private void Close(CommandContext context) => context.RequestClose();
```

Presenter 修改 `ViewModel.Title`、`ViewModel.ListPosition` 等状态，也可声明业务命令，不持有原生控件。简单页面可不编写 Presenter。Text/TMP 共享 ITextElement；按钮使用 IButtonElement；Slider/Scrollbar 使用 IValueElement<float>，Toggle 使用 IValueElement<bool>；UGUI/TMP 输入框、下拉框分别使用 IInputFieldElement、IDropdownElement。图片、RawImage、滚动区域契约位于 MUI.UGUI。输入框支持逐字或结束编辑后回写；下拉框支持文本选项。ScrollRect 包含归一化位置、速度和方向；它不是虚拟化列表，列表数据与资源加载继续使用已有列表/资源能力。

模型写入使用无通知原生 setter 或同步保护，不发布用户操作命令。有效交互先发布 PropertyChanged，再发布业务事件；门控检查 View、Host 和控件交互状态。绑定上下文负责激活阶段的业务解绑，适配器负责自身原生事件清理，不通过 RemoveAllListeners 清除他人的监听。绕过适配器直接修改 Text、Image 等原生属性不会自动通知模型。

#### 附加属性扩展（推荐）

只扩展属性逻辑时无需注册或替换整个适配器。声明公开、非嵌套、非泛型的扩展类，添加 `[ControlPropertyExtension]` 并继承 `ControlPropertyExtension<TComponent>`，提供公共 `NativeControl` 构造函数。Inspector 自动发现适用于当前组件及其基类的扩展，在“添加属性”中可搜索扩展类名和属性名。选择后生成器直接构造并缓存扩展，不使用运行时反射。不要在构造函数中修改组件值。

```csharp
using MUI.UGUI;
using UnityEngine.UI;

[ControlPropertyExtension]
public sealed class PriceProperties : ControlPropertyExtension<Text>
{
    public PriceProperties(NativeControl owner) : base(owner) { }
    public decimal Price
    {
        get => decimal.TryParse(Component.text, out var value) ? value : 0m;
        set => Component.text = value.ToString("0.00");
    }
}
```

选择 `Price` 后，业务只需给生成的 ViewModel 属性赋值。扩展不会在编辑器中实例化或读取初值，业务应设置需要的初值。只读扩展属性同样支持实时读取。同名原生属性与扩展属性会标出来源，由用户选择，禁止同时生成同一个目标名的两套绑定，避免隐式覆盖。扩展内部如果写入另一个已绑定属性，用户仍需避免两者同时控制同一状态。

需要界面回写时，在属性上标记 `[NotifiesChange]`，并在真实变化回调调用 `Changed(nameof(Property))`。该标记是通知契约，不会自动监听变化。Unity 输入事件使用 `Listen(Component.onValueChanged, value => Changed(nameof(Property)))`，自动执行输入门控并随控件释放解绑；普通 C# 事件使用 `TrackCleanup` 注册解绑，在用户输入回调中先检查 `CanReceiveInput()`。模型写入应使用 `SetValueWithoutNotify` 等无通知 API，避免触发用户命令。`ForwardChanges("Value", nameof(Property))` 可复用原适配器通知；扩展通知按类型隔离，不会串到同名原生属性。

#### 完整控件适配器（高级扩展）



自定义接口继承 `IElement`，自定义适配器继承 `NativeControl<TComponent>` 并实现接口。实现属性 setter 时调用 `Changed()`；外部值变化调用 `Changed(nameof(...))`。UnityEvent 可通过受保护的 `Listen` 接入输入门控和自动解绑；普通 C# 事件用 `TrackCleanup` 登记解绑，并在用户操作回调中检查 `CanReceiveInput()`。不要在构造函数里修改控件状态，编辑器校验也会创建并释放适配器。

```csharp
public interface IProgressDisplay : IElement
{
    float Progress { get; set; }
}

// MyGauge 是项目已有组件，此例假设它具有 Value 属性。
public sealed class GaugeAdapter : MUI.UGUI.NativeControl<MyGauge>, IProgressDisplay
{
    public GaugeAdapter(string name, MyGauge target) : base(name, target) { }
    public float Progress
    {
        get => Component.Value;
        set { if (Component.Value == value) return; Component.Value = value; Changed(); }
    }
}

public static class GaugeBindingSetup
{
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
#endif
    public static void Register() => MUI.UGUI.NativeControl.Register<MyGauge>(
        (name, component) => new GaugeAdapter(name, component));
}
```

业务声明 `[Bind("Gauge", nameof(IProgressDisplay.Progress))]` 即可；同时在命名规则里添加 MyGauge 或手动绑定引用。注册在编辑器与运行时都需要执行。按实际组件类型优先、再按最近基类匹配；自定义注册优先于内置适配器，同类型重复注册覆盖原工厂，可使用 `Unregister<T>()` 撤销。工厂必须保留绑定名称和目标引用。未注册的自定义 MonoBehaviour 仅有通用引用/可见性能力，不能自动推断任意业务属性。适配器、转换器和自定义 Element 都是扩展入口，不需要修改框架生成器。

#### 多组件及自定义组件规则

规则面板使用“前缀 + 组件多选”的紧凑行，不再展开数量和条目。`Txt_` 默认同时接受 Text、TMP_Text；未安装 TMP 时仍可使用 Text。类型菜单支持项目中的公开、非泛型 MonoBehaviour，取消已有勾选可移除类型。

`Img_Btn_Confirm` 会依次使用 Img_ 与 Btn_ 规则，在同一节点查找 Image 和 Button，生成 `ConfirmImage`、`ConfirmButton` 两个独立引用。单前缀仍沿用原名称：`Img_Icon` → `Icon`。一个规则命中多个不同组件类型时也追加实际类型名；同类型组件有多个时提示歧义，不自动挑选。重复命中的同一个组件只保留一次。

例如配置 `Widget_` 匹配 MyGauge，节点 `Widget_Gauge` 对应绑定名称 `Gauge`；注册上面的 GaugeAdapter 后即可通过 IProgressDisplay 绑定，不生成原生组件访问属性。

#### 搜索组件与名称匹配方式

组件选择器支持搜索类名、命名空间和程序集，多个空格分隔的词同时匹配；提供“全部 / 常用 / 已选”过滤。勾选立即写入配置并支持 Undo，弹窗保持打开，可连续多选。已选但找不到的类型仍保留显示，允许取消选择。

规则可切换“前缀 / 后缀 / 包含 / 正则”。前缀、后缀、包含会移除匹配文本，继续尝试其他规则，例如 `Icon_Img` 使用后缀 `_Img` 得到 `Icon`。包含只移除当次首次出现的文本；同一位置多个规则匹配时取表达式字符串较长的规则，同长度按列表顺序。正则使用 `(?<name>...)` 捕获最终绑定名称，例如 `^UI_(?<name>.+)_Text$` 将 `UI_Title_Text` 映射为 `Title`，匹配后停止继续解析；正则采用 50ms 超时限制，错误或超时在 View 面板提示并阻止生成。旧规则默认继续使用前缀模式。

### IDE 生成源码输出

MUI 全局设置的“开发工具 → 输出生成源码供 IDE 使用”默认开启。编辑器自动配置生成源码输出，并在导入、编译及域重载结束后同步 IDE 工程；不再需要单独点击 Enable 菜单。关闭后移除 MUI 自己的输出参数，IDE 工程恢复使用 Source Generator，Unity 自动绑定与运行逻辑不变。切换开关自动应用；已有 Library 输出文件保留，但关闭后不再加入 IDE 工程。项目自行设置的其他输出目录和编译参数不会被删除。


### 从多个异步打开入口生成参数

页面可使用 `UIPage(PresenterType = typeof(...))` 启用自动参数契约并默认返回 Unit；需要业务结果时使用 `UIPage(typeof(TResult), PresenterType = typeof(...))`；双类型参数声明用于手写 Args。Presenter 为 partial class，无需手写基类或 Args：

```csharp
[UIPage(typeof(int), PresenterType = typeof(InventoryPresenter), ResourceKey = "UI/Inventory")]
public partial class InventoryViewModel : ViewModel { }

public partial class InventoryPresenter
{
    [OpenEntry("ByTab")]
    private ValueTask OnPrepareOpenAsync(int tab, CancellationToken token)
    {
        // 无需等待时可立即完成；需要加载时改用 async ValueTask。
        return default;
    }

    [OpenEntry("ByItem")]
    private async ValueTask OnPrepareOpenAsync(long itemId, bool highlight = true,
        CancellationToken token = default)
    {
        await LoadItemAsync(itemId, token); // 项目自己的业务加载方法
    }
}

var route = InventoryViewModelDefinition.Create(() => new InventoryViewModel());
await InventoryViewModelDefinition.OpenByTabAsync(pages, route, tab: 2);
await InventoryViewModelDefinition.OpenByItemAsync(pages, route,
    new OpenCallOptions(cancellationToken: token), itemId: 1001);
```

每个命名入口生成 `ArgsByXxx`、`OpenByXxxAsync`、`BeginOpenByXxx`。自动 Args 位于 `Presenter.OpenArgs`，各入口对应独立的只读数据分支。用于 ReplaceAsync、UpdateArgsAsync 等操作时，可调用 `ArgsByXxx(...)` 取得参数。路由策略、参数比较、参数更新协议和结果等待方式均保持原样；切换入口不会强制重开或绕过复用策略。

入口支持 ValueTask、Task 和立即完成的 void，统一通过 IAsyncOpenPreparation 在 Context 建立后的异步准备阶段调用一次；导航等待其完成再提交打开。末尾 CancellationToken 为框架注入参数，不出现在生成 Args 或便捷调用的业务参数中。async void、泛型入口、ref/out/in、params 和开放泛型页面不受支持。入口名称必须唯一；同参数类型的不同入口可使用不同业务方法名。一个自动 Presenter 只对应一个页面契约和结果类型。

页面向导生成 `[OpenEntry] private void OnPrepareOpen(...)`，只保留业务参数，不再额外创建 Args.cs。无参特性按业务参数签名生成 `CreateArgs(...)`、`OpenAsync(...)` 和 `BeginOpen(...)` 重载，同一 Presenter 可声明多个。内部 Args 分支名称由参数类型自动生成，建议通过 CreateArgs 获取参数而不依赖分支名称。命名入口可同时存在。末尾 CancellationToken 不参与签名区分；重复业务签名及省略可选参数造成的相同前缀歧义会产生编译诊断。其他重载选择遵循 C# 规则，例如传入 null 或 default 时可能需要显式指定类型。无需等待时使用 void；需要等待数据加载时改为 async ValueTask 或 async Task，可选声明末尾 CancellationToken，由框架注入。无论业务是否异步，框架均通过异步准备协议等待入口完成。


`OpenEntry` 表示显示前的业务初始化，不是“已经显示”或“入场动画结束”的通知。执行顺序为资源加载 → 建立模型、绑定与 Context → 执行入口并等待完成 → 提交打开、显示界面并开始入场。入口抛出异常或异步准备取消时，走现有打开失败或取消清理流程，不提交成功打开。

普通初始化不需要任务返回值或取消参数：

```csharp
[OpenEntry]
private void OnPrepareOpen(string title)
{
    ViewModel.Title = title;
}
```

若必须先取到数据再显示，将入口改为 `async Task` 或 `async ValueTask`，在方法内部 await 数据加载并写入 ViewModel；按需添加末尾 `CancellationToken` 参数以配合框架取消。仅仅使用异步导航 API，不要求业务方法也声明 async。


同一页面的不同打开方式无需手动命名：

```csharp
[OpenEntry]
private void OnPrepareOpen(int tab) { }

[OpenEntry]
private void OnPrepareOpen(long itemId, bool highlight) { }

// 根据参数选择对应的业务入口。
await InventoryViewModelDefinition.OpenAsync(pages, route, tab: 2);
await InventoryViewModelDefinition.OpenAsync(pages, route, itemId: 1001L, highlight: true);
```

同类型同顺序的参数无法仅凭参数名称区分，例如 `OnPrepareOpen(int tab)` 和 `OnPrepareOpen(int itemId)`；这种情况仍需不同方法名和命名 OpenEntry。


### 直接通过页面名打开

引用 MUI.UGUI 后，路由声明同时生成业务页面入口。`InventoryViewModel` 和 `InventoryPageViewModel` 默认生成 `InventoryPage`；名称冲突时通过 `UIPage.PageName = "InventoryWindow"` 指定唯一入口名。底层 `InventoryViewModelDefinition` 工厂和显式导航 API 保留。

启动时初始化一次 `UIHost`，普通业务调用不再传入宿主、导航器或路由，也不需要 Bind：

```csharp
// 启动阶段：完成项目注册后初始化场景中的唯一宿主。
host.Initialize();

// 业务阶段：多个无名 OpenEntry 仍按参数签名选择。
await InventoryPage.OpenAsync(tab: 2);
await InventoryPage.OpenAsync(itemId: 1001L, highlight: true);
await InventoryPage.OpenAsync(new OpenCallOptions(cancellationToken: token), tab: 2);
```

无需构造依赖的页面自动创建并复用路由；声明 PolicyPreset 时读取全局页面预设。路由缓存和工厂属于本次 UIHost 生命周期，关闭或销毁宿主后释放，后续宿主重新创建。框架拒绝第二个尚未关闭的宿主；未初始化或已关闭时调用页面入口会明确报错。页面入口需在主线程调用。

有服务依赖或自定义策略的页面，在宿主初始化后、该页面首次使用前配置一次工厂：

```csharp
InventoryPage.ConfigureDefinition(() => InventoryViewModelDefinition.Create(
    modelFactory: () => new InventoryViewModel(),
    presenterFactory: model => new InventoryPresenter(inventoryService)));

// 后续仍只传业务参数。
await InventoryPage.OpenAsync(tab: 2);
```

页面首次取用路由后不允许替换配置，避免同一页面的活动实例与新配置混用。缺少必要工厂时不会偷偷构造依赖，而是提示先 ConfigureDefinition。`BeginOpen(...)` 及 `CreateArgs(...)` 也可直接通过短页面入口调用。未引用 MUI.UGUI 的核心程序集继续使用显式路由 API，不引入 Unity 依赖。


向导的“接收打开参数”只生成 ViewModel 和 Presenter，不生成占位 Result.cs；参数与结果互相独立。无结果页面默认声明：

```csharp
[UIPage(PresenterType = typeof(InventoryPresenter), ResourceKey = "UI/Inventory")]
public partial class InventoryViewModel : ViewModel { }
```

需要业务结果时手动指定 `UIPage(typeof(long), PresenterType = typeof(...))`，或使用自定义多字段/多分支结果类型。双类型参数声明用于显式定义 Args 和 Result。此调整只影响新建页面，不删除已有项目的 Result 文件。


新建页面不生成路由 Key，默认从 ViewModel 类型推导；所选预设等于项目默认预设时也不生成 PolicyPreset，特殊预设仍明确声明，确保向导预览与首次运行一致。已移除源码中的 PrefabAssetGuid 字段及读取，不提供旧字段兼容或自动迁移；旧声明需删除该命名参数，并在 Prefab Inspector 明确设置 ViewModel 关联。


### 省略约定资源键

`[UIPage]` 和 `[UIWidget]` 默认把模型类型名的 `ViewModel` 后缀去掉，再补 `View`（已有 View 后缀不重复）：InventoryViewModel → InventoryView。无需手写相同字符串。向导在实际资源键与约定一致时生成无参声明；Resources 子目录或自定义资源地址仍显式保留，例如 `[UIPage(ResourceKey = "UI/Inventory/InventoryView")]`，不会为了简写改变加载地址。

显式空字符串或 null 仍属于无效声明，不等价于省略参数。Prefab 的 ViewModel 关联仍只从 Prefab 元数据读取，与资源键推导无关。反射读取契约的工具应使用 `contract.GetResourceKey(modelType)` 获取最终资源键；ResourceKey 属性表示显式设置值。重命名模型会改变默认键，需要固定地址时明确指定资源键。


### 省略约定 Presenter

自动参数页面只需声明 `[UIPage]`。生成器在相同命名空间或相同外层类型内，将 InventoryViewModel 匹配到 InventoryPresenter；不扫描其他作用域、不按相似名称猜测。需要返回结果时写 `[UIPage(typeof(long))]`。非约定命名可以显式设置 PresenterType。找不到约定 Presenter 或类型无效时产生编译诊断，不静默改用空 Presenter。向导的新页面使用该简写；无 Presenter 的纯展示页面仍显式声明 Unit 参数和结果。

页面使用 `[UIPage]`，由导航管理打开、关闭和结果；子视图使用 `[UIWidget]`，由父界面管理绑定与释放，仅生成绑定，不生成导航入口。同一模型不能同时声明两者。Tab 内容、列表项等可组合视图使用 UIWidget，业务类名无需带 Widget 后缀。两种特性都位于 MUI 命名空间。


### 页面管理模块

`MUI.Pages` 统一管理 `[UIPage]` 页面，宿主通过 `UIHost.Current.Pages` 提供 `PageManager`。业务优先使用生成的 `InventoryPage.OpenAsync(...)`；通用关闭、返回和批量操作通过 `Pages` 调用。`[UIWidget]` 子视图继续由父界面管理。

`PageDefinition<TViewModel, TArgs, TResult>` 描述页面资源、创建工厂、策略和依赖；`PageHandle<TResult>` 标识一次打开的实例；`PageResult<TResult>` 表达页面完成的业务结果。打开操作结果 `OpenOutcome<TResult>` 与业务结果分开。生成的 `InventoryViewModelDefinition.CreateDefinition()` 提供底层定义工厂，自定义依赖通过 `InventoryPage.ConfigureDefinition(...)` 配置。

目录按职责划分为 Definitions、Handles、Operations、Lifecycle、Ownership 和 Diagnostics。常用公开类型位于 `MUI.Pages`；事件、快照和追踪类型位于 `MUI.Pages.Diagnostics`。`Internal` 下按打开、关闭、依赖、缓存、预加载和显示职责组织实现；PageManager 仍统一协调生命周期提交，不改变执行顺序。


### 配置和生命周期边界

`MUISettings` 只保存运行时页面策略、容量、输入与显示配置。控件命名、绑定生成规则、页面生成目录和 IDE 源码输出开关保存于 `ProjectSettings/MUIEditorSettings.asset`，由 `MUIEditorSettings` 管理，只在编辑器加载。同一个 MUISettings Inspector 同时编辑两份配置，编辑器配置不加入 Player 预加载。`RuntimeSettingsAsset` 负责运行时资产发现、创建与构建接入。不会从旧运行时字段自动迁移配置。

页面预设为 `PagePolicyPreset`，序列化配置值为 `PagePolicySettings`，最终运行策略为 `PagePolicy`。通用清理仍称 `ViewInstanceCleanup`，页面与 Widget 共享它。

`OnPrepareOpen` / `IAsyncOpenPreparation.OnPrepareOpenAsync` 均在显示提交前执行；有异步操作时框架等待准备完成，再提交激活。`[OpenEntry]` 方法也属于准备阶段，向导默认命名为 `OnPrepareOpen`；不能把回调开始视为界面已经显示完成。打开方通过 `OpenAsync` 的成功结果观察就绪，转场完成另见句柄的转场任务。

子视图模块统一为 `MUI.Widgets`：`WidgetDefinition` 定义工厂，`WidgetScope` 管理父级所有权，公开 `WidgetHandle` 提供访问和操作契约，内部 `WidgetInstance` 执行生命周期，`WidgetSlot` 协调单内容槽的异步替换。它们不进入 PageManager 的顶层页面集合。

PageManager 仍协调打开和关闭的提交；独立的 `PageTraceBuffer`、`PageCacheCatalog`、`PageCacheBudget`、`PagePreloadRequest`、`PageRenderOrderAllocator` 分别负责追踪存储、缓存目录与淘汰计算、缓存释放预算、共享预加载请求以及显示区间分配。虚拟列表由 `VirtualListSelection` 管理选择状态，由 `VirtualListCell` 管理单元格的原生订阅与节点释放，已有 `VirtualGridLayout` / `VirtualRowIndex` 继续负责几何计算。


### 打开完成回调与定时更新

UIPage 与 UIWidget 的 Presenter 都可重写 `protected override void OnOpened()`。此时本次激活已提交显示；页面还会等待进入效果结束（无动画时立即完成）。这是逻辑显示完成通知，不保证 GPU 已绘制一帧。每次激活最多触发一次；缓存重新激活会再次触发，单纯隐藏/覆盖后恢复以及更新参数、换绑不会重复触发。准备失败、显示前关闭不触发；回调抛出异常按打开失败清理。

`OnOpened` 是同步通知，可读取 `ViewModel` 和 `Context.Args`；必须在显示前完成的异步加载仍放在 `OnPrepareOpenAsync`。`OpenAsync` 返回的就绪结果保持原有语义，不额外等待进入动画或 `OnOpened`；打开方等待动画请使用句柄的转场任务。

```csharp
protected override void OnOpened()
{
    // 已打开：播放业务提示、发送展示事件等。
}
```

Presenter 通过 OnUpdate 统一更新，无需实现频率接口。默认禁用；显式配置后启用，配置在实例创建时读取。

```csharp
// 非缩放时间，每帧调用。
protected override UpdateOptions UpdateOptions => MUI.UpdateOptions.EveryFrame;

protected override void OnUpdate(UpdateTime time)
{
    // DeltaTime / UnscaledDeltaTime：上次回调以来累计的缩放/非缩放秒数。
    // ElapsedTime / UnscaledElapsedTime：本次激活可更新期间累计的两种秒数。
}
```

每两秒调用一次可将配置改为 `MUI.UpdateOptions.Every(2f)`；使用缩放时间调度则为 `MUI.UpdateOptions.Every(2f, UpdateTimeMode.Scaled)`。每帧按缩放时间调度使用 `new UpdateOptions(0f, UpdateTimeMode.Scaled)`。选择 Scaled 后，timeScale 为零不调用 OnUpdate；Unscaled 仍可更新。同一回调始终提供两种时间，全部使用 double 秒数。

每帧最多调用一次，卡顿时合并遗漏的回调，保留间隔余量；DeltaTime 与 UnscaledDeltaTime 传实际累计时长，不伪装成配置间隔。准备和进入动画期间、隐藏暂停或缓存停用期间不累计；缓存重新激活从零开始，隐藏后恢复保留累计值。缩放时间停走而界面仍可更新时，非缩放累计值继续增长，在下次回调可读取。

UIPage 和 UIWidget 共用规则。页面暂停由 PagePolicy.TickPause 控制（默认隐藏暂停），Widget 还受父级暂停及 WidgetDefinition.PauseTickWhenHidden 控制。UIHost 自动传递 Time.deltaTime 与 Time.unscaledDeltaTime；手动驱动使用 AdvanceFrame(deltaTime, unscaledDeltaTime)，PageManager.Tick 与 WidgetScope.Tick 同样接收两种帧时长。输入必须为有限非负数，不能同时使用自动与手动驱动。页面进入/退出动画仍使用非缩放时间。

### 创建和直接引用 Widget

页面向导的“创建类型”可选择 Widget。Widget 生成独立 View Prefab、`[UIWidget]` 模型及可选 Presenter / Args，不生成页面导航和关闭按钮。已有 Widget Prefab 不要求 Title / Close。目标程序集需引用 MUI.Core、MUI.Resources、MUI.Widgets、MUI.UGUI；Page 模式还需要 MUI.Pages。

在父 Prefab 编辑模式选择根 View，将 Widget Prefab 拖入层级，或使用“子 Widget → 添加引用”。父 View 自动发现当前边界内关联了 UIWidget 的子 View；确认绑定名称和父 VM 属性后点击“生成绑定”。这会保存父 View 的子引用和生成声明，不添加包装节点，不改锚点和尺寸，也不改子 Prefab 的内部绑定。多次使用相同 Widget 时为每个引用指定唯一名称和父属性。其他 View、NestedViewElement、动态容器、列表模板均是发现边界。

父模型生成具体类型的属性，例如 `ItemViewModel Item`，绑定到父 View 内部的 `WidgetReference.VM`；子模型访问会登记子模型的生成工厂。内部控件继续由子 View 独立索引。已有手写 `[Bind("Item", nameof(WidgetReference.VM))]` 优先，不覆盖源码。手写接线需显式调用子 BindingFactory.Register 或模块 Initialize。生成声明保存在父 Prefab，移除实例后再次生成以清理声明；业务仍使用已删除属性时由编译器报告。

勾选“自动创建子 VM”时要求公共无参构造函数，每个父模型惰性创建独立子模型。生成的默认子模型归父模型所有，替换后保留到父模型最终释放，避免提前销毁仍在清理或被其他视图投影的对象。框架创建的父模型由框架收尾；外部持有父模型时，在所有投影结束后调用 `ReleaseWidgetVMsAsync()`。关闭自动创建后默认 null，由业务赋值；业务赋值的模型只借用，不自动释放。null 清理绑定并隐藏静态子 View，不删除它的 GameObject。

Presenter、BindingContext 和 WidgetHandle 提供 `VM` 简写，原有 ViewModel 属性兼容保留，类型名仍使用 XxxViewModel。新 Presenter 模板使用 `VM`。Widget 的 Presenter 由生成工厂登记，可用 `[UIWidget(PresenterType = typeof(ItemPresenter))]` 明确选择。多个匹配 Presenter 时需显式选择，或在手写接线前调用 `WidgetRegistry.Register<TVM, TArgs>(presenterFactory, bindingFactory, replace: true)` 注入业务工厂；生成登记不会覆盖业务替换。`BindingRegistry.Reset()` 同时清除 Widget 工厂。

初始化参数可绑定到 `WidgetReference.Args`（应先于 VM 提供），默认使用对应参数类型的 default 值。活动期间的参数更新要求 Presenter 实现 `IArgsUpdatePresenter<TArgs>`；不支持时明确报错。父模型的分阶段换绑保留子参数，不能同时以同一事务改变业务参数和子模型，应分别执行参数更新和父模型换绑。静态引用和动态内容共用已登记的类型化 Widget 工厂；旧 NestedViewElement 保留借用模型的兼容入口，并复用静态子项管理逻辑。

代码构建层级可在父 View 初始化前调用 `ConfigureChildWidgets(...)`，以子 View 对象引用登记，不使用路径查找。Navigation 示例菜单“打开直接 Widget 场景”演示不带包装节点的打开、模型替换、清空、恢复和父级清理。
