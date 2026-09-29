# MUI

以 FUI 为主要参考、MUFramework 为次要参考的 Unity uGUI 框架。表现主线为 ViewModel → 生成绑定 → View/Element，提供同步与异步导航、UI 生命周期、子界面、Tab、虚拟列表/Grid 和编辑器工具。项目资源系统通过契约接入。框架仍在实现与验证中，已有源码和离线编译结果不代表全部 Unity 运行场景已验收；逐项状态见下方实现记录。

## 文档

- [代码架构图与目录划分](docs/CODE-ARCHITECTURE.md)
- [代码规范与格式检查](docs/CODE-STYLE.md)
- [完整 UI 框架设计](docs/UI-FRAMEWORK-DESIGN.md)
- [实际实现与验证状态](docs/IMPLEMENTATION-STATUS.md)
- [命令、取消与来源上下文](docs/COMMANDS.md)
- [Presenter 与导航接入](docs/NAVIGATION.md)
- [UI 资源绑定与项目资源系统](docs/RESOURCE-SLOTS.md)
- [子视图与 ThingItem 接入](docs/CHILD-VIEWS.md)
- [动态子视图与候选替换](docs/DYNAMIC-CONTENT.md)
- [异步 Tab 接入与边界](docs/TABS.md)
- [异步 Prefab 加载与预加载](docs/PRELOADING.md)
- [Loading 与操作归属](docs/LOADING.md)
- [通知队列与显示](docs/NOTIFICATIONS.md)
- [锚点浮层显示基础](docs/ANCHORED-OVERLAYS.md)
- [拖放会话与提交](docs/DRAG-DROP.md)

## 主要程序集

- `MUI.Core`：ObservableObject、ViewModel、Lifetime、命令、表现契约、绑定与声明属性。
- `MUI.Resources`：独立同步/异步资源加载契约、Lease 与生命周期持有权接入；控件资源槽仅为 uGUI 内部实现。
- `MUI.Tabs`：选择状态、同步/异步内容切换、去重等待、错误重试与加载提示策略。
- `MUI.ChildViews`：Template、Handle、Scope、子界面准备/提交与父子激活；独立于导航。
- `MUI.Navigation`：Route、Handle、ViewInstance、基本打开/关闭/历史/结果协议。
- `MUI.UGUI`：View、Element、基础控件、NestedViewElement、DynamicViewElement、UIHost 和常驻 PrefabViewProvider。
- `MUI.UGUI.Automation`（可选）：本地控件交互、条件轮询、Game View 截图与导航追踪导出；基础 uGUI 仅保留输入适配契约。
- 列表分级：`RecyclingListElement` 用于少量奖励/道具项，保留全部条目并复用空闲节点；`VirtualListElement` 用于长列表/Grid，仅物化视口附近条目。两者复用子 View 生命周期，支持完全同步接入。
- `MUI.Editor`：基于生成 Manifest 的 View 绑定检查器。
- `MUI.Generators`：Roslyn 4.3 增量生成器，作为 Analyzer 发布，不作为游戏运行时程序集。

Core、Resources、Navigation、ChildViews、Tabs 不依赖 UnityEngine 或第三方资源库。包依赖 uGUI；接入基线为 Unity 2022.3.62f3 / .NET Standard 2.1。

## 接入与验收工程

仓库内的 [Basic Example](ExampleProject~/README.md) 是可直接打开的 Unity 2022.3.62f3 工程，包含真实 uGUI Prefab、场景、生成绑定与纯同步类型化导航。包通过相对路径接入，不依赖临时验收目录；按工程 README 在 Play Mode 中点击 Confirm 可看到结果和清理状态。

本仓库是 UPM 包，完整 Unity 工程由接入项目提供。独立验收时使用 Unity Hub 创建的 Unity 2022.3.62f3 工程，通过 Package Manager 添加本地 `package.json`，并从包的 Samples 区域完整导入所需示例。标准导入目录为 `Assets/Samples/MUI/0.1.0`，不要只复制入口脚本或引用另一个项目的 `Library/PackageCache`；uGUI 使用正常的 `com.unity.ugui: 1.0.0` 包依赖。

Samples 导入后是工程中的独立副本；修改本地包的 `Samples~` 不会自动更新已导入文件。验收当前包源码前需重新导入对应示例，并核对其脚本、程序集声明与资源引用。

使用 TMP 适配时还需安装 TextMeshPro，并执行 **Window → TextMeshPro → Import TMP Essential Resources**。安装包与导入默认字体、TMP Settings 是两个步骤。部分示例只在编辑模式保留 Demo 组件，Canvas、控件和页面由 Play Mode 的初始化代码创建；自动演示也可能在完成后清理页面。

首次图形验收前确认所需 Samples 已完整导入、TMP Settings 与默认字体可读取、场景无 Missing Script、Console 无编译错误，并实际运行示例检查画面。不要把批处理能编译当作工程资源和图形环境已经完整。本次独立验收工程已通过上述完整性检查。

图形验收使用正常的编辑器启动入口，例如 macOS 的 Unity Hub 或 `open -n -a <Unity.app> --args -projectPath <工程目录>`。若整个编辑器出现洋红，先检查启动日志中的内置 Shader 错误及安装目录的 `CGIncludes`，并保留异常 Shader 缓存后再重建。当前验收工程曾出现实际存在的 `HLSLSupport.cginc` 无法解析；正常启动并重建缓存后已恢复。仅切换工作目录并不能保证解决此问题。

使用 `-batchmode -nographics` 完成导入或演示，只能作为编译和逻辑证据。编辑器验收可直接使用项目配置的 Unity 版本；设备性能与 IL2CPP 验收另按实际构建配置执行。

## 项目日志接入

框架运行时的未处理诊断异常统一交给 `UIErrors.Report`。项目可在初始化 UI 前设置全局错误出口，直接转发给自己的日志系统：

```csharp
var previousSink = UIErrors.Sink;
UIErrors.Sink = error => projectLogger.LogError("MUI", error);

// 项目退出或卸载 UI 模块时恢复原出口。
UIErrors.Sink = previousSink;
```

配置期间不会强制调用 `Debug.LogException`；未配置时，活动 UIHost 使用 Unity Console 作为默认出口，多个宿主只登记一次。没有 UIHost 的 View/Element 原生销毁异常也会先检查项目出口，再回退到 Unity Console。`UIErrors.Reported` 仍用于额外观察，接收器异常会被隔离，不能中断界面清理。出口是进程级配置，目前框架主动发送的是错误；普通导航状态可订阅类型化生命周期事件，示例和 Editor 工具中的 `Debug.Log` 不属于运行时框架日志。

## 设置页示例

通过 Package Manager 导入 **Settings**，在空场景中给一个 GameObject 添加 `SettingsDemo`。示例自动创建滑条、文字与 Save/Reset 按钮，展示 VM → 生成绑定 → uGUI 的数据流和异步命令。

Settings 示例独立展示绑定与命令；导航行为由 Navigation 示例演示。具体接入说明见 [Settings README](Samples~/Settings/README.md)。当前代码已通过 Unity 2022.3.62f3 的 Settings 批处理 Play Mode 演示，结束时绑定为 Unbound，且没有执行中的保存命令；macOS IL2CPP 构建及独立 Player 启动、画面与退出清理已验证。物理鼠标键盘输入、完整故障场景及其他平台仍待验收。

## 导航示例

先导入 **Resource Integration**，再导入 **Navigation** 示例，使用 Tools → MUI → Samples → Open Navigation Scene，体验同步/异步打开和类型化结果关闭。源码已包含替换、转场、缓存与依赖等接入示例；具体策略与验证范围见 [实现状态](docs/IMPLEMENTATION-STATUS.md)。当前代码已通过 Navigation 与 Tabs 的既有批处理 Play Mode 演示；异步资源槽、完整故障路径和真实输入仍需专项验收。

普通奖励列表另有 **Tools → MUI → Samples → Open Recycling List Scene**：手动进入 Play 后自动创建界面，展示完全同步的生成绑定、添加条目和点击条目删除自己。该新增示例目前仅通过离线编译，尚未完成 Unity 画面与交互验收。

资源绑定演示入口为 **Tools → MUI → Samples → Open Resource Binding Scene**，可在 Inspector 选择同步或异步模式，检查图标、纹理和字体的切换、失败重试及清理计数。异步模式的快速换键、失败重试和在途关闭已在独立 Unity Editor 验证凭证全部归还；同步与异步模式加载完成后的 Warm/Cool 图像及文字切换均已通过 Game 截图确认。加载中瞬态画面和其他平台仍待验收，详见 [实现状态](docs/IMPLEMENTATION-STATUS.md)。

## 完全同步接入

需要全生命周期无异步时，使用 `UIHost.InitializeSynchronous()`；不依赖 Unity 的宿主使用 `Navigator.CreateSynchronous(provider)`。只在普通宿主上调用 `Open()`，不代表关闭、资源释放和子界面也采用纯同步模式。

```csharp
// host 已在 Inspector 配置 Prefab 目录，route 已配置同步生命周期。
host.InitializeSynchronous();
var opened = host.Navigator.Open(route, args);

if (opened.IsSuccess)
{
    // 此处演示主动关闭；实际项目通常保留句柄，等待用户操作。
    var closed = host.Navigator.Close(opened.Handle);
    if (opened.Handle.TryGetResult(out var result))
    {
        // 在这里处理界面返回值，不需要等待任务。
    }

    // 检查 closed.Status：关闭可能被守卫拒绝，并非调用后就一定关闭。
}
else
{
    // 根据 opened.Status、Rejection 和 Error 处理失败，不使用失败句柄。
}

// 宿主退出时直接完成框架清理。
host.Shutdown();
```

接入要求：

- Route 和子界面 Template 显式设置 `supportsSynchronousLifecycle: true`；Presenter 使用同步钩子，命令使用 `SynchronousCommand`。仅声明支持同步不能使异步业务自动变成同步业务。
- Prefab 提供方实现 `ISynchronousViewProvider`。可用常驻 `PrefabViewProvider`，也可参考 `Samples~/ResourceIntegration` 中的 `SynchronousLoadedPrefabViewProvider` 接入项目同步资源后端，无需实现异步加载接口。
- 业务资源由项目加载后通过 `Lifetime.OwnDisposable` 托管同步凭证；Resource Integration 示例提供 `Lifetime.Load<T>(loader, key)` 接线扩展。激活资源随本次显示结束释放；实例资源在实例销毁时释放，启用缓存时可以跨多次显示保留。
- 子界面沿父作用域继承同步模式；Tab、动态子界面及虚拟列表/Grid 使用各自同步入口与本地数据源。异步分页、异步生命周期和仅支持异步释放的资源不能混入此路径。
- 导航提供直接返回的 `Open`、`Close`、`Complete`、`Back`、`Replace`、`UpdateArgs`、`Rebind`、预加载、缓存清理和 `Shutdown`。同步模式不通过阻塞任务或启动异步操作来完成这些操作；能力不满足时明确拒绝。
- 用户稍后关闭界面时，通过句柄的 `ObserveResult(lifetime, callback)` 接收结果；就绪通知使用 `ObserveReadiness(lifetime, callback)`。两者都是无需任务的一次性订阅，订阅所属的 `Lifetime` 也应使用同步模式。立即查询返回值使用 `TryGetResult`，同步替换的源清理结果使用 `SourceClose`，超限替换的清理结果使用 `ReplacedClose`。
- 完全同步业务不要读取用于异步兼容的任务属性，例如 `SourceCleanup`、`ReplacedCleanup` 或 `CleanupCompletion`；这些属性可能按需创建任务。不要通过 `.Wait()`、`.Result` 或 `GetAwaiter().GetResult()` 调用异步 API 来模拟同步。

同步表示框架操作直接执行，不表示所有行为必须在当前调用栈完成：命令内部请求关闭可能由下一次 `Pump()` 同步派发，以避免销毁正在执行的绑定；`UIHost` 默认自动驱动该帧泵。回放或自定义时钟可设置 `host.AutomaticFramePump = false`，再每帧调用 `host.AdvanceFrame(delta)`，同时推进请求派发和界面 Tick。Unity 的 `Object.Destroy` 仍遵循引擎延迟销毁规则，同步资源后端必须保证归还源资源时不会破坏尚存活的克隆依赖。现有程序集仍同时包含异步 API，纯同步模式不是移除 Task 类型的独立发行包。

完整接入示例见 [SynchronousNavigationDemo](Samples~/Navigation/SynchronousNavigationDemo.cs)、[同步子界面](Samples~/Navigation/SynchronousChildDemo.cs)、[同步虚拟列表](Samples~/Navigation/SynchronousVirtualListDemo.cs) 和 [同步 Tab](Samples~/Tabs/SynchronousTabsDemo.cs)。当前同步实现已有 Unity 运行证据；真实 Prefab 导航的 macOS IL2CPP 首场景画面、键盘结果关闭，以及编辑器 500 次缓存/100 次非缓存循环已复验。完整故障组合、物理鼠标/手柄、长时设备性能与其他平台仍待验收。

标准确认框与提示框也可完全同步接入，见 [Dialogs 示例](Samples~/Dialogs/README.md)：直接 Open，通过 ObserveResult 接收用户稍后提交的结果，再由帧驱动打开后续界面，无需异步 DialogService。

本地化与主题服务借用项目已准备的目录，通过构造函数创建，并使用 `SetCatalog` 同步切换、`Dispose` 结束订阅。目录加载、缓存及释放由项目负责，详见 [本地化接入](docs/LOCALIZATION.md) 与 [主题接入](docs/THEMES.md)。

## 子界面与图标资源配置

在父 View 激活前调用 `view.ConfigureSynchronousResources(loader)`，即可为它的资源键绑定提供同步加载器；异步后端使用 `ConfigureResources(loader)`。例如 `ImageElement.SpriteSource` 接收 VM 提供的图标键，由控件资源槽负责替换和归还。

未显式配置的子 View 默认借用最近父 View 的活动加载器，适用于 ThingItem、列表条目和挂载在父层级中的动态子界面。子 View 仍用自己的激活 Lifetime 持有图标，关闭条目即可释放该条目的资源；它不会取得加载器的所有权。父级的业务资源不会因此全部变成子级资源。

子 View 的显式配置优先；需要隔离时，在激活前设置 `InheritParentResources = false`。继承只按激活时的 Transform 层级查找最近父 View，不跨越没有活动配置的中间 View，也不自动连接另一个 Canvas 下的逻辑父界面。纯同步激活不能使用继承来的异步加载器。完整规则见 [资源配置](docs/RESOURCE-SLOTS.md)，可运行示例入口见上方普通奖励列表。

使用 UIHost 的 Inspector 预制体目录时，可统一接入根 View：`host.InitializeSynchronous(configureDefaultView: view => view.ConfigureSynchronousResources(loader))`。允许异步的宿主使用 `Initialize` 和 `ConfigureResources`。该回调只配置默认目录中新建的实例，子 View 沿用上面的继承规则；外部 provider 应通过自身的 `configureView` 入口配置。

如果初始资源未加载好时不应显示页面，在激活前设置 `view.WaitForResourceSources = true`。它等待该 View 默认加载器的资源键绑定，加载失败会使准备失败；默认关闭，页面显示后的资源更新仍渐进加载。同步加载保持直接调用。完整范围与示例开关见 [首次显示前等待资源键](docs/RESOURCE-SLOTS.md#首次显示前等待资源键)。

页面停留在准备阶段时，可在 View Inspector 的“首帧资源准备（只读快照）”中手动查看等待数、失败数和对应控件属性。提交后记录清空，快照不包含任意业务任务或子 View 的加载。

## 绑定声明与属性变化回调

在 View Inspector 选择 ViewModel 契约后，可展开“绑定声明与源码”，从属性或命令绑定的“打开声明”按钮跳到对应源码行。“定位控件”按当前 View 的绑定边界查找并高亮目标；缺失会提示，重名会列出全部候选，不自动选一个。旧版或手写清单没有源码位置时仍可定位控件；更新生成器并重新编译后可跳转源码。

生成属性需要联动派生状态时，可以使用 `OnChanged`，无需手写事件订阅：

```csharp
[ObservableProperty]
private int count;

[OnChanged("Count")]
private void UpdateSummary(int oldValue, int newValue)
{
    Summary = $"数量：{newValue}";
}

[ObservableProperty]
private string summary;
```

这些成员放在继承 `ViewModel` 的 `partial class` 中。回调也可以无参数或只接收新值，必须是同步实例 `void` 方法，参数类型与生成属性一致。同值赋值不调用；批量通知期间回调仍立即执行。回调异常会传给赋值调用者，已写入的属性不会回滚。目标必须是当前模型生成的属性；手写属性和继承属性暂不支持此声明方式。

## 只在绑定时赋值

不随模型后续变化刷新的界面配置可以使用 `BindingMode.OneTime`：

```csharp
[ObservableProperty]
[Bind("PlayerName", nameof(InputFieldElement.CharacterLimit), bindingMode: BindingMode.OneTime)]
private int nameCharacterLimit = 16;
```

每次建立绑定都会读取并写入一次；不会订阅来源或控件的属性变化，也不会反向写入模型。换绑或缓存复开重新建立绑定时会重新读取。OneTime 仍参与目标写入冲突检查，不能与另一条绑定同时写同一控件属性。用于 SpriteSource 等资源键时，只限制键的推送次数，加载与释放仍由控件资源生命周期管理，不表示资源一定同步完成。

## 计算属性通知

计算文字可以直接读取源属性，使用字段上的声明生成依赖通知：

```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(Summary))]
private int count;

public string Summary => $"数量：{Count}";
```

`Count` 实际变化时，先执行原属性通知和 `OnChanged` 回调，再发布 `Summary` 通知；同值不通知。批量更新沿用现有通知去重。目标必须是已有的公共可读实例属性，允许为一个字段声明多个不同目标。这里只通知显式列出的属性，不递归推断依赖；手动调用 `OnPropertyChanged("Count")` 不会自动传播。需要多个计算层级时，把需要刷新的目标都列在源字段上。

## ViewModel 基类复用

抽象的顶层、非泛型 `partial ViewModel` 可以声明 `ObservableProperty`、`OnChanged` 和已实现的 `Command` 方法。生成属性与命令由具体模型继承，不需要为了生成代码把公共基类改成可实例化类型：

```csharp
public abstract partial class CommonViewModel : ViewModel
{
    [ObservableProperty]
    private bool busy;

    [Command]
    private void Reset() => Busy = false;
}

[ViewContract("Example")]
public partial class ExampleViewModel : CommonViewModel
{
    public void Begin() => Busy = true;
    public ISynchronousUICommand ResetAction => ResetCommand;
}
```

基类 `Bind` / `BindCommand` 会按基类到派生类合并，基类属性与命令只生成一次。派生类仍需声明契约（如 `ViewContract`）以生成自己的工厂；Navigation 示例的 `PageViewModelBase` 演示公共标题与关闭按钮绑定。父子声明共用目标写入、反向输入和事件冲突检查，不允许遮蔽或重写基类绑定来源。跨程序集时，使用当前生成器重新编译基类即可保留生成属性和命令的绑定元数据；旧版本元数据明确诊断。手写属性用于跨程序集绑定时需显式指定 `ElementType`，不能依赖已编译 DLL 中不存在的 `nameof` 语法。抽象命令方法仍不支持生成。抽象基类的工厂接收已有实例，不负责实例化；注册表按运行时精确类型选择工厂，不会自动回退。

## 编译

`Tools~/Build` 现有 38 个 Unity、Editor 和 Samples 离线构建项目；连同 1 个生成器项目，2026-09-28 当前工作树共 39/39 个项目通过 Unity 2022.3.62f3 对应的 Release 编译，零警告、零错误。604 个 C# 文件的成员布局、空白及控制流大括号检查通过；全包 746 份元数据的 GUID 格式、唯一性及资产对应关系检查通过，Runtime/Editor/Samples 的 C# 和 asmdef 均有元数据，38 个 asmdef 内部引用及无环检查通过。独立 Unity 2022.3.62f3 项目的 Navigation、Settings、Tabs 既有 Play Mode 批处理演示通过。详见 [实现记录](docs/IMPLEMENTATION-STATUS.md)。这些证据不代替真实输入、渲染、平台及完整故障验收，后续修改仍需按影响范围验证。

纯托管基础层：

```sh
dotnet build 'Tools~/Build/MUI.Resources.csproj' -c Release --disable-build-servers -m:1
```

生成器源码修改后，必须更新随包发布的 Analyzer。`Tools~/Build` 的源码生成器编译通过，不代表 Unity 实际导入的 `Analyzers/MUI.Generators.dll` 已更新；旧 DLL 可能仍生成异步命令，使完整同步路由不可用。发布命令：

```sh
bash 'Tools~/publish-generator.sh'
```

Generator 工程需要 Roslyn 4.3 的 NuGet 包；基础层不依赖第三方 NuGet 包。离线工程统一使用 `--disable-build-servers -m:1`，避免复用挂起的本机 MSBuild 服务。`Tools~/Build` 中的 uGUI、Editor 与示例工程需要传入 `UnityManagedPath` 和 `UnityUIAssemblyPath`，涉及 TMP 时还需 `UnityTMPAssemblyPath`；路径应指向匹配版本的 Unity 程序集，不在仓库中写死机器路径。


Loading 与 Notifications 的显示控件分别位于 `MUI.UGUI.Loading` / `MUI.UGUI.Notifications`，项目 asmdef 使用时请显式引用；基础 UGUI 不依赖这两个可选模块。对应 Editor 校验通过独立扩展程序集注册。源码仍包含在同一 UPM 包内。

主题绑定工具独立为 `MUI.UGUI.Themes` / `MUI.TMP.Themes`；调用 ThemeBindings、PreferenceBindings 或 TMPPreferenceBindings 时请补充对应 asmdef 引用。基础 UGUI/TMP 控件不依赖主题服务。详见 [主题接入](docs/THEMES.md)。

拖放控件与绑定桥接位于 `MUI.UGUI.DragDrop`，校验位于独立 Editor 扩展。使用拖放控件的项目请补充该程序集引用，详见 [拖放接入](docs/DRAG-DROP.md)。

TabBarElement 与 AsyncContentElement 位于 `MUI.UGUI.Tabs`；使用这些控件的项目 asmdef 需显式引用，类型命名空间仍为 `MUI.UGUI`。

标准确认框工厂 `ConfirmationDialog` 位于 `MUI.UGUI.Dialogs`，项目需显式引用。创建确认框的菜单由 `MUI.UGUI.Dialogs.Editor` 提供。基础 UGUI 不再引用现有标准模块。

本地自动化会话 `UIAutomation` 位于 `MUI.UGUI.Automation`。项目 asmdef 使用会话、条件等待或截图时需显式引用该程序集；现有类型命名空间和实例方法签名仍为 `MUI.UGUI`。基础 `MUI.UGUI` 只提供控件输入适配与状态契约，不执行截图或 PNG 编码。

包依赖：uGUI 以及内置 ScreenCapture、ImageConversion 模块（1.0.0）。后两者供可选自动化程序集编译截图/PNG 导出入口使用，不是基础 `MUI.UGUI` 的程序集引用；框架不读写导出文件。JSONSerialize（1.0.0）也是主包依赖，供页面向导读取程序集定义使用；项目资源示例复用该模块。
