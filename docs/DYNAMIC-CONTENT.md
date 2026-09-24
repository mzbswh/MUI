# 动态子视图与子视图替换

`ChildViewSlot` 位于 MUI.ChildViews，负责一个内容位置的准备、提交与回收；uGUI 的 `DynamicViewElement` 负责 Source/VM 绑定和动态节点挂载。二者不进入 Navigator 历史，也不等同于完整 Tab 模块。

## 接入

```text
ParentView [View]
└── Content [DynamicViewElement]
    └── 创建的子 View
```

```csharp
[ObservableProperty]
[Bind("Content", nameof(DynamicViewElement.Source))]
private string contentSource = "ThingItem";

[ObservableProperty]
[Bind("Content", nameof(DynamicViewElement.ViewModel))]
private ThingItemViewModel contentModel;
```

绑定前注册子 VM 的生成工厂，并配置借用的 IViewProvider。例如在 Route 的绑定工厂包装中先配置 Provider，再返回生成 BindingContext：

```csharp
BindingContext<PageViewModel> BindPage(IView view, PageViewModel model)
{
    ((MUI.UGUI.View)view).GetElement<DynamicViewElement>("Content").Configure(provider);
    return PageViewModelBindingFactory.Create(view, model);
}
```

Provider 必须返回独立实例 Lease。Element 在实例仍隐藏时将 uGUI View 移入自己的内容边界，并将本地位置归零；不能返回宿主本身或宿主的祖先。Provider 本身由项目/Host 管理，Element 不 Dispose 借用的 Provider，子实例则始终通过 Lease 释放。

- Source 非空时创建实例，null/空字符串清空当前内容。
- ViewModel 为 null 时仍保留 Source 的实例，不建立业务绑定；默认隐藏，ShowUnbound=true 可以显示未绑定的 Prefab 内容。
- 非空 VM 按生成工厂绑定，VM 被借用。
- SetContent(source, model) 原子提交两项输入，也能重试相同输入，避免分别设置属性产生中间候选。
- PendingChange 返回选择结果；DisplayedSource、DisplayedViewModel、HasInstance 描述实际内容，可能不同于仍在加载的输入。
- 每次父激活由初始绑定重新提供 Source 和 VM；手工赋值应在父激活建立后进行。

当前在更换 Source 或 VM 对象时准备新实例，以保留失败回退能力。同一 VM 的普通属性更新仍直接更新现有绑定，不重新实例化。原节点换绑复用与池策略尚未实现。

## 替换与回收

```csharp
var slot = new ChildViewSlot(parentView.ChildViews);
var result = await slot.ReplaceAsync(async (scope, token) =>
    await scope.PrepareAsync(template, provider, args, assignedModel, token));
```

委托必须返回属于传入 Scope 的 Prepared Handle，不能自行 Commit、返回当前激活项、等待或重入同一个 Slot。Slot 无 await 地提交候选，成功后才关闭旧内容；失败保留旧内容，并恢复提交前的本地 visible/interactable，而不是无条件启用旧内容。此通用槽在准备期间仍允许旧内容运行和交互，Tab 的停用旧激活、输入禁用及恢复策略由后续模块实现。

容量是一个进行中的准备/回收操作，加一个最新待启动请求。新请求淘汰旧请求的提交资格，旧调用方得到 Superseded；实际 I/O 和迟到资源继续被观察、回收。旧提供者不响应取消时，新选择等待可用容量，不不断创建更多实例。

Ready/Empty 表示逻辑选择完成，不代表旧实例物理回收完成。先完成选择结果、再等待旧命令清理，使旧子命令可请求自身替换而不等待自己；进入回收的子命令不能再请求需要等待自身回收的工作。

Clear 立即隐藏并移除当前逻辑内容，同时取消在途候选；回收继续被跟踪，新的创建等待旧回收收敛。取消在提交开始前阻止提交；提交开始后，调用者取消不再将已接受的提交报告成 Cancelled。父取消仍会终结激活。

结果包括 Ready、Empty、Superseded、Cancelled、ParentInactive、Failed；Failed 带 Error。槽最终 Dispose 等待在途准备、迟到回收和当前内容释放，汇总清理错误。父激活 Lifetime 自动拥有槽；所有入口在所属 UI 线程调用。

## 验证与边界

Navigation 示例已在 Unity 2022.3 Play mode 展示：Iron × 3；模拟不响应取消的慢加载被淘汰，旧结果 Superseded、最新结果 Ready；null VM 时实例仍在；重新绑定 Iron × 7；清空 Source 后 HasInstance=false。现有导航和父子关闭示例同次正常结束。

完整故障恢复、提交边界竞争、多层组合和所有清理异常尚未完成运行验收。Tab 默认快照/加载提示流程已实现，见 [异步 Tab](TABS.md)。旧内容冻结与恢复、缓存和超时隔离已有代码，但仍需补齐运行验收。不合作 I/O 仍可能无限延迟物理清理，但不会因此无限创建新候选。资源加载、共享计数、重试和预算由项目后端负责。


子视图槽提交回滚的本地门控恢复已修正并通过编译；尚未完成 Unity 故障路径验收。它只恢复显示/交互状态，不冻结业务任务或解绑订阅，不构成 Tab KeepPrevious 的实现。

## 虚拟网格与列表共用组件

虚拟 Grid 已由 `MUI.UGUI.VirtualListElement` 支持，没有另设 VirtualGridView 组件。`Columns = 1` 为列表，`Columns > 1` 为纵向虚拟网格；内部 `VirtualGridLayout` 负责几何计算。

```csharp
// 初始化前配置四列，每行默认高 100，前后各预留两行，总物化上限 128 个单元。
grid.Configure(scrollRect, itemTemplate, height: 100,
    overscanRows: 2, capacity: 128, columnCount: 4);
grid.Items = items;

// 已初始化后也可切换列数，并等待单元准备与绑定。
grid.Columns = 3;
await grid.PendingChange;
```

Inspector 的 Columns、Row Height、Overscan 与 Max Cells 对应这些配置。列宽均分内容容器宽度，同一行从左到右排列；内容总高度按行计算。位置索引和可见范围以行查询，只为视口及 overscan 对应的单元物化子 View。capacity 的单位是单元数，不是行数，列数增加也会增加同一视口的物化需求；超限明确报错，不悄悄截断可见内容。

| 能力 | 当前实现 |
| --- | --- |
| 固定尺寸网格 | 固定行高、等宽列，末行允许不足一整行 |
| 变高网格 | 每行采用最大条目高度；单元保持自身高度并顶端对齐 |
| 自动测量 | 按当前列宽测量，列宽变化使旧测量失效；仍受每帧测量预算限制 |
| 模板与数据 | 多模板、稳定 ItemKey、集合批次、分页、选择和滚动定位共用列表协议 |
| 动态列数 | 重建行索引，按原可见条目对应的新行恢复偏移，并在边界夹取 |
| 横向网格、瀑布流、跨行/跨列 | 尚未实现；当前不是任意二维布局容器 |
| 组标题和树节点 | 可作为普通单元输入；不自动跨满整行，单列更适合层级阅读 |

不要再让 GridLayoutGroup/ContentSizeFitter 同时驱动列表内容和物化单元的位置，几何由 VirtualListElement 管理。独立间距、外边距和按最小单元宽度自动计算列数当前没有专用配置；项目可在单元模板内部留白，并显式设置 Columns。Navigation 示例已有 100/10000 条数据网格物化量对比、三列布局、变高网格及切回列表流程；当前阶段只复核代码与离线编译，未完成 Unity 网格视觉及性能验收。

## 虚拟列表的显式动态高度

`VirtualListItem` 现在接受可选的 `height`。未指定时使用列表默认行高；指定值必须为有限正数。同一列表可混用默认高度与显式高度：

```csharp
var items = new ObservableList<VirtualListItem>(
    new[]
    {
        new VirtualListItem("short", shortModel, height: 40),
        new VirtualListItem("long", longModel, height: 120)
    },
    itemKey: item => item.Key);

// 高度是不可变的条目描述，通过同键替换触发布局更新，模型仍为借用。
items[0] = new VirtualListItem(items[0].Key, items[0].ViewModel, height: 80);
```

布局使用预建的树状数组行高索引：可见范围定位、条目偏移查询与单行高度修正均为 O(log 行数)，滚动时不扫描全量数据。网格每行取该行条目的最大高度，单元保持自身高度并顶端对齐。全部条目使用默认高度时继续走固定高度计算，无位置数组。

高度变更、变高列表追加、结构变化及列数切换会重建位置索引，当前代价为 O(条目数)。同键同高度的单项模型更新仍走已有增量路径。重建保留首个可见项的稳定键及行内偏移；锚点被删除则使用相邻有效索引，行缩短或内容不足时按新几何夹取。ScrollToKeyAsync 使用目标实际高度；目标高于视口时对齐顶部。物化数量仍受 maxCells 限制，超限走原错误与重试协议。

Navigation 示例新增了不同高度的 1000 项、视口前行高变更及三列网格切换日志。目前仅通过离线编译，尚未完成 Unity 布局验收。此入口接收项目已知或已测量的高度；文本自动测量、按键/宽度/字体缓存测量结果、分帧测量及增量高度索引仍待实现，不能据此认定设计要求的完整动态测量系统已完成。


## 可见条目的自动高度测量

在初始化前调用 `list.ConfigureHeightMeasurement(true, perFrame: 4)`，或在 Inspector 启用 Measure Item Heights 并设置 Measurements Per Frame。此模式下，未提供显式高度的项先用默认行高估算，再在条目绑定完成后的 LateUpdate 中分批测量。每帧最多测量配置数量的可见单元，不额外实例化屏外条目；显式 Height 始终优先。

条目 View 根节点必须通过 uGUI `ILayoutElement` 提供正的首选高度，例如使用 VerticalLayoutGroup 汇总换行 Text 子节点，或由项目组件提供。测量会对该根执行 `LayoutRebuilder.ForceRebuildLayoutImmediate`，然后读取 `LayoutUtility.GetPreferredHeight`。列表控制单元尺寸，避免在根上同时使用争夺尺寸控制的 ContentSizeFitter。无有效高度时进入列表 Error，修复模板后通过 RetryAsync 恢复。

测量缓存最多保留当前来源的条目数，条目删除、替换或父级重新激活时释放相应记录。单元宽度变化使测量缓存失效；集合 NotifyUpdated 即使仍使用同一个模型，也使该项失效。直接修改模型后若不发布集合 Update，应调用 `InvalidateHeightMeasurements()`；字体、语言、字号或模板布局变更后也调用此入口。重建按首个可见索引和行内偏移修正位置，数据结构变化仍采用稳定键锚点。

PendingChange 表示条目准备与绑定完成，不表示分帧测量已全部收敛；初始显示可从估算高度逐步调整。测量发生在 LateUpdate，隐藏或保留旧画面的列表不会主动测量。每批测量只重算受影响网格行的最大高度，并增量更新树状数组；处理 k 个不同行的成本为 O(k × (列数 + log 行数))，不重扫全量数据。初次启用测量预建估算高度索引；结构、宽度或字体失效等全局变化仍 O(n) 重建。布局回调导致来源、激活或刷新资格变化时，未提交测量整批丢弃，防止缓存与几何不同步。Unity 换行、宽度/字体变化、滚动锚点及真机性能尚未运行验收。


## 虚拟列表的命名模板

列表保留默认 `itemTemplate`，并支持在初始化前注册额外模板：

```csharp
list.ConfigureTemplates(new[]
{
    new VirtualListTemplate("header", headerView),
    new VirtualListTemplate("reward", rewardView)
});
var header = new VirtualListItem("section-1", headerModel, height: 48, templateKey: "header");
var reward = new VirtualListItem("reward-1", rewardModel, templateKey: "reward");
```

Inspector 的 Item Templates 提供同样配置。模板键非空、唯一且区分大小写，`TemplateKey = null` 选择默认模板。所有模板需为当前边界内、Content 之外的未激活 View，具有 RectTransform；初始化后不支持替换模板目录。未知模板键进入契约错误，不能静默回退默认模板。各模板仍须满足其 ViewModel 的绑定契约。

单元复用同时检查稳定 ItemKey 和 TemplateKey。相同键更换模板时，先隐藏并排空旧绑定，再实例化对应模板；选择身份仍由稳定键保持。空闲的同模板单元可复用，不兼容单元会移除，不为每种模板永久保留历史池。maxCells 仍限制物化工作集；Unity Destroy 延迟到帧末，不代表同帧原生对象数量立即下降。

模板切换通过替换 `VirtualListItem` 发布，因此旧测量缓存也随条目身份失效。状态提示节点不得位于任何条目模板内部。Navigation 示例提供默认与 featured 模板混排和同键切换演示；目前仅完成离线编译，Unity 生命周期、原生对象回收、选择与焦点行为尚未验收，异步加载模板资源仍未接入。

## 有界分页数据来源

`PagedList<T, TCursor>` 实现 `IReadOnlyObservableList<T>`，可直接赋给 `VirtualListElement.Items`。分页来源位于 Core，不引用 Unity，也不创建 View；需要从具有 UI 同步上下文的线程创建与调用，以便异步完成回到所属线程。

```csharp
var source = new PagedList<VirtualListItem, string>(
    activationLifetime,
    async (cursor, requestedSize, token) =>
    {
        var page = await service.FetchAsync(cursor, requestedSize, token);
        return new ListPage<VirtualListItem, string>(
            page.Items, page.NextCursor, page.HasMore);
    },
    item => item.Key,
    pageSize: 50,
    maxItems: 10000);
list.Items = source;
var result = await source.LoadNextAsync();
```

同一来源最多一条在途请求；重复调用共享该页加载。调用令牌只取消自己的等待并返回 WaitCancelled，父 Lifetime 或显式 DisposeAsync 才取消底层加载。销毁等待底层工作结束，迟到结果不得追加。来源借用条目模型，只清除自身引用；模型资源由项目负责。

IsLoading、HasMore、Error 与 StateChanged 提供加载按钮或页尾提示状态。失败返回 Failed，保留既有条目与游标；再次 LoadNextAsync 重试同一页。末页成功仍返回 Loaded，之后请求返回 EndReached。每次成功页通过一次集合批次追加；跨页重复键、超出请求大小、空页仍声明 HasMore、游标不前进以及默认拒绝策略下总容量耗尽均失败。游标及稳定键应为不可变值；提供者遵守 requestedSize，来源不会自动截断服务端数据。

来源通知、加载器和取消回调内部不能再次加载、重置或等待自身销毁，避免循环等待；通知异常隔离报告，不撤销已提交页。换查询条件可使用下节的 ResetAsync，也可创建新来源替换 Items 并释放旧来源。当前支持显式向后加载及下节介绍的页尾自动触发；头部插入分页见下节；有界条目窗口与查询重置见下节。

Navigation 示例演示 20 + 20 + 5 条分页、第二页首次失败、重试保留游标及独立等待取消。当前仅离线编译，Unity 分页显示、关闭与不合作提供者的迟到结果尚未运行验收。

## 同一来源内切换查询

`PagedList.ResetAsync(initialCursor, loader, cancellationToken)` 在同一个来源内切换筛选条件或从头刷新。传入的新加载器应捕获不可变查询快照，避免在旧请求未结束时修改共享筛选对象。省略加载器时使用最近待应用候选的加载器，没有候选时沿用现行加载器。页大小、插入方向、容量策略及条目键规则保持构造配置。

```csharp
var filterSnapshot = selectedFilter;
var reset = await source.ResetAsync(
    initialCursor: null,
    loader: async (cursor, requestedSize, token) =>
    {
        var page = await service.FetchAsync(filterSnapshot, cursor, requestedSize, token);
        return new ListPage<VirtualListItem, string>(
            page.Items, page.NextCursor, page.HasMore);
    });
if (reset.Status == PageResetStatus.Applied)
{
    await source.LoadNextAsync();
}
```

接受重置后立即替换 `QueryVersion`，取消旧页并等待加载器退出。等待期间保留已显示条目，`IsResetting` 和 `IsLoading` 为 true；显式加载返回 `PageLoadStatus.Resetting`，自动预取暂停。旧加载即使忽略取消并返回数据，提交前仍检查其专属令牌；未提交的旧页返回 Superseded。已在重置前提交的成功页仍是 Loaded，随后由重置清空。

多个待处理重置共享同一排空操作，只保留最后一份加载器和首游标，之前的待处理请求返回 `PageResetStatus.Superseded`。不会为每次切换启动一个新加载器或累积待处理查询链。旧加载结束后，通过一次 Reset 集合通知清空条目和键目录、切换加载器与游标、恢复 HasMore，再返回 Applied。Applied 只表示查询就绪；第一张新页由显式调用或已启用的自动预取加载。集合条目清空也会按现有列表规则清除选择与测量缓存。

调用令牌只取消本次等待并返回 WaitCancelled，已接受的重置继续执行；预先取消的调用不改变查询。父生命周期取消或来源销毁使待处理重置返回 Inactive，销毁等待旧加载与重置退出。不合作的加载器不退出时会阻塞重置和销毁，当前没有分页超时隔离；不会谎报资源已释放。取消回调或集合提交失败返回 Failed，并暴露 Error；失败不应用新加载器与首游标，后续可显式重试重置。上下文失效则来源终止后续操作。

`IVersionedPagedListSource` 是可选消费契约，保持原 `IPagedListSource` 实现兼容。其 QueryVersion 必须是非空、稳定、不会复用的身份对象，读取不得有副作用。VirtualListElement 在版本变化时清除旧分页错误，并在异步返回时检查版本，旧查询的失败不能覆盖新查询状态。

Navigation 演示使用受控的迟到页，在它退出前连续请求两个查询。预期日志为 old=Superseded、first=Superseded、latest=Applied、during=Resetting，中间查询加载次数为 0，新查询加载次数为 1，最终仅保留键 100..102。演示已离线编译，尚未在 Unity 运行，也不构成竞态或异常路径测试结果。

## 分页容量与滑动窗口

默认 `overflow: PageOverflowPolicy.Reject` 保持已有行为：剩余容量小于页大小时缩小请求，满容量后返回 Failed，不调用加载器。若需要持续加载，可在构造时指定 `overflow: PageOverflowPolicy.EvictOppositeEnd`。

```csharp
var source = new PagedList<VirtualListItem, string>(
    activationLifetime, FetchPageAsync, item => item.Key,
    pageSize: 20, maxItems: 30,
    insertion: PageInsertion.Append,
    overflow: PageOverflowPolicy.EvictOppositeEnd);
```

窗口模式始终按配置页大小请求；追加时淘汰头部，前插时淘汰尾部，只移除容纳本页所需的条目。`maxItems` 必须至少容纳一页。删除与插入通过一次集合事务发布，观察者不会看到半个窗口或超出容量的已提交数据。`PageLoadResult.AddedCount` 与 `EvictedCount` 分别记录本次加入和移出的条目数，`Capacity` 返回配置上限。失败不移除旧条目、不推进游标；空末页仍可结束分页，不触发淘汰。

稳定键校验覆盖当前保留窗口和整张新页，即将淘汰的旧条目也不能与本页重复。键与键的相等性必须保持稳定。来源不保留无限增长的历史键集合，已在更早提交中淘汰的键不参与查重。淘汰只移除引用，不销毁业务模型或取消其独立资源。

列表沿用稳定键锚点：锚点仍在窗口内时修正偏移；锚点被淘汰时按原索引在新窗口内截取有效位置。选择项被移除时按已有选择协调逻辑清理。窗口容量限制数据条数，物化单元数量仍由虚拟列表独立限制；候选页、事务副本、项目持有的模型和资源也不包含在该条数预算内。

这是一条固定方向游标的滑动窗口，不提供已淘汰页的自动回看，也不代表双向分页或完整聊天组件。自动预取仍按加载方向触发；容量较小、视口接近覆盖整个窗口时可能连续预取，应由项目选择合适容量或关闭自动预取。

Navigation 示例以页大小 20、容量 30 分别加载三页：Append 的保留范围依次为 0..19、10..39、30..59；Prepend 为 80..99、60..89、40..69。后两页预计分别淘汰 10、20 项，日志同时记录阅读锚点变化。仅有离线编译与源码检查证据，Unity 滚动、焦点、动态高度及释放行为仍需运行验收。

## 虚拟列表页尾预取与分页状态

`PagedList` 现在实现非泛型 `IPagedListSource`，虚拟列表在 Items 赋值时自动识别分页能力。初始化前调用 `ConfigurePaging(true, prefetchItems: 5)`，或在 Inspector 启用 Auto Load Pages；运行时也可通过 `AutoLoadPages` 暂停或恢复自动触发。阈值表示当前可见范围及 overscan 之后剩余的已加载条目数，空来源也能触发第一页。

每帧最多启动一次预取，已有请求未结束时不会重复创建等待；已有内容正在物化或旧画面处于保留状态时不触发。分页失败后停止自动重试，使用 `LoadNextPageAsync()` 显式重试。`IsLoadingPage`、`HasMorePages`、`PageError` 提供独立表现状态，分页错误不替换虚拟列表已有条目或改写其绑定/布局 Error。可以据此显示页尾加载或重试按钮。

分页等待登记到当前激活 Lifetime，父级取消会取消本次等待。来源本身仍由其业务 owner 管理，列表换绑只退订并失效旧回写，不擅自销毁共享来源。换来源与重新激活有版本检查；旧完成不能覆盖当前分页状态。自定义 IPagedListSource 必须遵守 UI 线程、共享加载与取消等待协议。

Navigation 示例将第二页失败后的重试改为列表入口，再定位至第 39 项、启用自动预取末页。该示例仅离线编译，帧驱动预取、错误提示和关闭时序仍需 Unity 运行验收。预取方向由来源 Insertion 决定；暂无滚动速度预测。


## 向头部加载历史页

创建 `PagedList` 时传入 `insertion: PageInsertion.Prepend`，每页通过一次集合事务插入索引 0；默认 Append 仍追加至末尾。页内顺序原样保留，来源不会反转条目。`LoadNextAsync` 表示沿当前来源游标继续加载；在历史来源中，NextCursor 可以是更早一页的游标，不要求数值递增，但必须推进且不得重复稳定键。

自动预取从 IPagedListSource.Insertion 读取方向：Append 检查可见范围及 overscan 后面的剩余条目，Prepend 检查其前面的条目。头部插入后，虚拟列表已有的稳定键锚点计算会修正滚动偏移，保留正在阅读的条目；数据不会强制把视口拉到新插入的头部。第一次空来源没有锚点，保持默认起点，若聊天界面要求首次落在最新消息，应在首屏数据加载后调用 ScrollToKeyAsync 定位最新键。

方向在一个 PagedList 实例内不可更改；页大小、总容量、跨页键校验、共享请求、失败重试与迟到取消协议两种方向共用。Navigation 示例新增 80..99 与 60..79 两个历史页，先定位 90，再加载前一页并记录可见锚点是否保留。该用例尚未在 Unity 中执行；多列/动态测量下的锚点、实时消息追尾、双向独立游标仍需进一步实现或验收，当前不代表完整聊天列表组件。


分页来源显式保存创建时的 UI 同步上下文。页结果提交、成功/失败状态发布和最终集合清理都通过该上下文回到创建线程，不依赖项目加载器维持当前线程的 SynchronizationContext。null 或仅为基类 SynchronizationContext 的配置在构造时拒绝；派生上下文的 Post 仍必须实际回到创建线程。调度抛错或回到错误线程时，来源返回 Failed、暴露 Error 并失效后续加载，不在错误线程调用 Changed/StateChanged，也不无限重试调度。失效上下文导致无法执行清理时，DisposeAsync 如实报告失败，不表示已经释放全部持有引用。该异常路径仅编译与源代码检查，尚未运行验收。

## 动态测量后的定位就绪

`ScrollToKeyAsync` 现在区分数据变化与测量修正：Items 更新、结构变化、列数变化或新定位请求仍使旧请求返回 Superseded；同一数据来源的自动测量修正不会直接淘汰定位，而是按最新高度重新对齐。

开启自动测量时，定位等待当前物化工作集的未测量项完成，再确认目标实际高度落在视口内；高于视口的目标对齐顶部。需要焦点时，完成上述条件后才选择目标控件。显式高度和固定高度无需额外等待测量。普通 PendingChange 仍只表示条目准备与绑定完成，不能替代这个更强的定位就绪结果。

等待使用列表 LateUpdate 帧信号，不在同一帧反复轮询。最多进行 maxCells + 8 次校正，每次等待新帧最长 2 秒；超过预算返回 Failed 并携带 TimeoutException。调用取消、父级关闭保持原 Cancelled/Inactive 语义。停止更新、隐藏或处于显示保留阶段的列表可能无法完成测量，此时不会错误报告 Ready。该流程及焦点重入尚未经过 Unity 运行验收。


虚拟列表刷新现在捕获来源版本与激活身份，并在原生隐藏、尺寸调整、模板初始化和选择表现回调后重新复核。项目回调同步换来源或关闭父级时，旧快照不得继续启动绑定或激活单元。尚未交接工作集的创建结果由创建路径自己清理；清理逐项继续，原始创建失败与独立清理失败聚合保留。此回调失效路径仅通过离线编译，尚未在 Unity 注入相应回调故障验收。

## 分组数据与折叠

`GroupedList<T>` 位于 Core/Collections/Grouping，将显式分组快照展平为 `IReadOnlyObservableList<T>`。它负责组顺序、展开状态和集合通知；渲染仍使用同一个 `VirtualListElement`，组标题与正文都使用普通 VirtualListItem 和子 View，不增加独立的分组渲染器。

```csharp
var groups = new GroupedList<VirtualListItem>(
    item => item.Key, maxItems: 10000, maxGroups: 100);
groups.Reset(new[]
{
    new ListGroup<VirtualListItem>(
        key: "weapons",
        header: new VirtualListItem("header:weapons", weaponsHeader,
            height: 50, templateKey: "group-header"),
        items: weaponRows),
    new ListGroup<VirtualListItem>(
        key: "materials",
        header: new VirtualListItem("header:materials", materialsHeader,
            height: 50, templateKey: "group-header"),
        items: materialRows,
        initiallyExpanded: false)
});
list.Items = groups;

// 标题按钮的业务命令可以调用同一个入口。
groups.SetExpanded("materials", !groups.IsExpanded("materials"));
```

示例中的 `group-header` 需要在列表初始化前通过 ConfigureTemplates 或 Inspector 注册。标题模型可与正文模型使用不同绑定工厂；展开图标、计数及标题按钮命令由标题 ViewModel/Presenter 表达，适配器不会猜测或修改业务模型属性。组键与行键属于不同空间，但所有标题和正文的行键必须全局唯一、不可变；即使正文折叠也参与重复键校验。同一个物品显示在多个组时，行键需要包含分组或其他显示身份。

- `SetExpanded(key, value)` 在一个集合事务内发布标题 Update，并插入或移除正文。重复设置不通知；未知组键报错。空组仍可记录展开状态并发布标题更新。
- `SetAllExpanded(value)` 一次重置全部可见行，不按组连续触发布局。
- `Reset(groups)` 替换分组快照并按组键保留现有展开状态；新增组采用 InitiallyExpanded，删除组的状态立即移除。传入 `preserveExpansion: false` 可统一采用快照中的初始配置。业务自行决定分组、排序和组内顺序，适配器按传入顺序输出。
- `Count` 是当前输出的行数；`TotalItemCount` 包含所有标题与正文，折叠不会减少它。`Capacity` 对 TotalItemCount 生效，GroupCapacity 限制分组数。无效键、重复键或超限在提交前失败，旧分组、展开状态和可见行保持不变。
- `GetGroup(index)` 返回不可变结构快照；数据结构不自动订阅输入列表。业务结构变化后提交新的快照，模型普通属性变化仍沿既有绑定更新。

来源所有访问和编辑在创建线程进行；集合通知、键选择器或输入枚举回调中禁止重入编辑。展开状态在集合发布前切换，Changed 观察者看到一致的状态。通知异常沿 ObservableList 的隔离报告规则处理，不撤销已提交数据。

折叠只移除可见行，列表依稳定键协调选择、测量和锚点。仍可见的锚点保持位置；选中正文被折叠时清除选择，不自动选中标题。定位隐藏正文返回未找到，项目需要先展开其组再调用 ScrollToKeyAsync。数据层继续借用标题与正文模型，折叠、重置或 Clear 不负责销毁业务资源；原生单元的隐藏和回收仍由虚拟列表负责。

Reset 的校验与展平为 O(全部条目数)；单组切换查找前缀为 O(组数)，当前 ObservableList 事务会复制可见集合，因此编辑整体为 O(可见条目数)，不是 O(1)。滚动仍沿现有位置索引查询，不逐帧重新展平分组。容量约束针对已接受的来源数据，输入快照、候选事务与业务自身的引用也占用内存。

当前提供单层分组及普通标题行。多列网格中标题仍占一个单元，不提供整行跨列或吸顶标题；多层树见下节；异步组内容加载和专用标题交互编辑器尚未实现。Navigation 的 GroupedList 演示使用 100 组、每组 25 项，共 2600 行，复用 featured 模板作为标题，记录折叠前方组的锚点、选中项折叠、分组反转、展开后定位和全部折叠的物化数量。已离线编译，Unity 视觉、输入、焦点、性能及故障路径仍未运行验收。

## 多层树形数据

`TreeList<T>` 位于 Core/Collections/Trees，将节点快照按先序展平为标准可观察列表。它支持多个根、任意输入顺序的父引用，以及每个节点独立的展开状态。稳定节点键由构造时的 `itemKey` 获取，与最终虚拟条目的键一致；ParentKey 为 null 表示根节点，其他值必须引用同一快照中的节点。

```csharp
var tree = new TreeList<VirtualListItem>(
    item => item.Key, maxNodes: 10000, maxDepth: 64);
tree.Reset(new[]
{
    new TreeNode<VirtualListItem>(rootRow, initiallyExpanded: true),
    new TreeNode<VirtualListItem>(branchRow, parentKey: rootRow.Key),
    new TreeNode<VirtualListItem>(leafRow, parentKey: branchRow.Key)
});
list.Items = tree;

// 隐藏后代先展开路径，再沿普通列表入口滚动、物化与聚焦。
tree.ExpandAncestors(leafRow.Key);
await list.PendingChange;
await list.ScrollToKeyAsync(leafRow.Key, focus: true);
```

结构验证覆盖全部节点，包括折叠子树。重复键、空键、缺失父节点、自引用、父链环、超出节点容量或深度上限都会在提交前失败，旧树与可见集合保持不变。根深度为 0，maxDepth 是包含边界的最大深度。构建使用显式遍历栈，不递归调用，因此异常输入不会通过深层递归耗尽调用栈。根与同级节点各自保持输入顺序；子节点可以先于父节点出现在输入中。

`SetExpanded(key, value)` 只改变指定节点状态。折叠祖先会隐藏全部后代，但保留后代自己的展开状态；再次展开时恢复。`ExpandAncestors(key)` 在一次事务中展开目标的全部祖先，不改变目标自己的展开状态。`SetAllExpanded` 一次展开或折叠全部节点，完全折叠时保留所有根。`Reset(nodes)` 默认按稳定键保留展开状态，重排或换父级后仍保留；新节点采用 InitiallyExpanded，删除节点的状态立即丢弃。`preserveExpansion: false` 可以恢复全部描述中的初始配置。

查询入口包括 `GetNode`、`GetParentKey`、`GetDepth`、`HasChildren`、`IsExpanded` 和 `GetVisibleIndex`，未知键报错。存在但被折叠祖先隐藏的节点可见索引为 -1，深度和父级查询仍可用。Count 是可见节点数，TotalNodeCount 包含隐藏后代。所有操作在创建线程执行，输入枚举、键选择器与集合通知中禁止重入编辑。

内部预建先序顺序、父索引、深度及子树结束位置，展平时直接跳过折叠子树；不会创建与整棵业务树同规模的 Transform 层级。结构与展开状态、可见索引先统一切换，再发布一次集合 Reset。渲染继续使用 VirtualListElement 的稳定键锚点、选择协调、模板、动态高度和物化预算。选中项被隐藏时清除选择，不隐式选中父节点。模型和节点描述均为借用；折叠、重置和 Clear 不销毁业务资源。

结构重建为 O(全部节点数)；可见展平为 O(可见节点数)，但每次展开编辑仍复制全量展开状态和可见索引，因此整体 O(全部节点数)，不宣称局部编辑 O(1)。滚动期间只访问已经展平的列表与行索引，不重新遍历业务树。存量状态受 maxNodes 限制，候选快照和业务引用仍有额外内存成本。

缩进、展开箭头、节点按钮与专用键盘交互由节点 ViewModel/Presenter 表达，可通过 GetDepth/HasChildren/IsExpanded 同步表现；数据层不会直接操作 Transform。异步懒加载子节点、节点级拖动排序、专用树无障碍语义和键盘导航尚未实现。

Navigation 的 TreeList 演示有 20 个根，每个根 5 个分支，每个分支 20 个叶子，共 2120 个节点；初始根展开而分支折叠，显示 120 行。演示隐藏叶子的祖先展开、视口前方折叠时的锚点、选中节点随祖先折叠、后代状态保留、反转输入后刷新和路径恢复。全部折叠后仅显示 20 行，单独恢复一个叶子路径后显示 45 行。示例通过离线编译，Unity 视觉、输入、焦点、性能和错误路径尚未运行验收。


## NestedViewElement 的同步模式

父激活 Lifetime 为 Synchronous 时，静态嵌套控件使用独立同步路径：先完成旧句柄释放，再通过 Scope.PrepareSynchronous 创建借用子视图，最后提交。模型设为 null 时同步移除。整个替换登记在父 Lifetime，期间禁止控件重入赋值和当前子命令更换自己，避免仍在使用原生节点时重新绑定。

新模型准备或绑定失败且候选清理成功时，尝试同步恢复旧模型的显示，仍向属性赋值方报告原失败。ViewModel 表示请求模型，DisplayedViewModel 表示实际显示模型；恢复后两者可以不同。再次赋入同一个失败请求模型会重新尝试，不因旧模型仍活动而错误短路。

旧句柄、失败候选或恢复候选清理失败时停止恢复，并禁止当前激活继续复用该原生 View。ChildViewPreparationException 的 CleanupCompletion 在同步清理失败时已经完成为失败，异常包含原始准备及清理错误；不会启动后台清理器。能力违例导致未完成释放时不能把旧节点当成可复用资源。

PendingChange/Preparation 仍提供共同的完成观察接口，但同步路径不执行 ChangeAsync/RunAsync、不等待任务，也不启动异步错误观察器。模型借用、不销毁父 Prefab 内的原生子 View、绑定边界与父级显示门控沿用已有规则。允许异步的父激活仍使用原有异步替换路径。

Navigation 示例使用 SynchronousNestedDemo 及其嵌套的 SynchronousNestedViewModel：配置场景父 View、NestedItem 包装节点及其 ThingItem 子 View，可用组件菜单同步换模型和清空。代码不使用 async/await。当前仅离线编译，Unity 真实绑定、重入、失败恢复和资源释放未验收。静态嵌套控件与动态子视图均已接入同步路径；虚拟列表已接入纯同步视口协调，见后文；Tab 同步链见 TABS.md。

## DynamicViewElement 与内容槽的同步模式

父激活使用 `LifetimeMode.Synchronous` 时，`DynamicViewElement` 直接调用 `ChildViewSlot.Replace/Clear` 和 `Scope.PrepareSynchronous`，在设置器返回前完成候选准备、提交和旧内容同步清理。清空 Source 会同步撤下并释放内容。同步路径不调用异步准备或释放接口，不等待任务，不启动后台清理。`PendingChange` 和父准备信号只记录已经完成的结果。

`ConfigureSynchronous(ISynchronousViewProvider)` 可接入只实现同步契约的提供方；`ContentViewProvider.CreateSynchronous` 提供对应挂载包装。原有 Configure 仍可配置同时支持两种契约的提供方。模板、绑定、命令和资源释放必须满足同步能力要求。同步槽拒绝异步退役回调与准备超时配置；同步调用不提供异步加载中的占位过程。

替换先准备隐藏候选，失败保留旧内容；提交后同步释放旧内容。返回状态 Ready/Empty 且 Error 非空表示提交成功但清理失败，调用方必须检查 Error。DynamicViewElement 同时通过设置器异常和 Preparation 失败信号报告此错误，保留 PendingChange 的实际提交状态。清理失败后禁止当前槽继续切换，最终 Dispose 汇总历史错误；不会自动重试释放或隐藏后台任务。业务可以使用 DisplayedSource/DisplayedViewModel 检查实际显示状态，失败请求可以再次赋相同值重试，但不能绕过清理失败保护。

整个替换登记在父与 Scope 的同步操作中，准备、提交和退役回调期间禁止重入变更选择意图或释放槽。父销毁前逐层预检同步清理能力。同步释放 Unity 实例仍使用正常 Destroy，其原生销毁由 Unity 帧末执行，不代表 DestroyImmediate。

`Samples~/Navigation/SynchronousDynamicDemo.cs` 展示完全同步的动态道具创建、替换、清空和父级清理。当前验证为离线编译，尚未完成 Unity 运行验收。虚拟列表/grid 与 Tab 的同步路径见后文；顶层导航完整同步模式已经由 `Navigator.CreateSynchronous` 接入，但运行验收仍不能用离线编译替代。


## 虚拟列表与 Grid 的纯同步模式

父激活为 Synchronous 时，VirtualListElement 的来源更新、滚动、Columns 切换和视口重建直接在 Lifetime.Run 中执行。列表与网格仍使用相同的稳定键、行索引、模板目录、物化容量、选择、动态高度及父级门控。新建单元通过同步 NestedViewElement 绑定，出视口单元同步解绑后才能复用；移除空闲单元沿已有统一释放入口销毁包装节点。

两种模式共用 RefreshCells 协调迭代器，避免复制一份布局/复用算法。它只执行同步状态变更并标记嵌套绑定边界；异步驱动等待边界的既有任务，同步驱动直接推进，不调用 RefreshAsync、不检查异步任务是否完成。PendingChange 仅保留已经完成的结果信号。异步模式仍在排空一组出视口单元后开始复用，并在单个候选准备后显示；任务信号在对应操作发起时捕获，后续回调不能替换等待对象。

纯同步项目使用普通 IReadOnlyObservableList/ObservableList，禁止自动分页配置和 IPagedListSource 来源；这些异步能力必须放在允许异步的父模式中。LoadNextPageAsync、RetryAsync、ScrollToKeyAsync 在纯同步模式下拒绝。同步入口提供 Retry 和 ScrollToKey(key, focus)，后者共用最终焦点/门控复核。动态测量正常由 LateUpdate 帧驱动；显式同步定位会在同一调用中进行有界测量，不等待后续帧，无法收敛则返回 Failed。单次定位可能比普通滚动消耗更多 CPU，隐藏或未完成外部布局的视口不能保证同步测量成功。

同步刷新回调若不断提交新来源，以 maxCells + 8 次协调上限停止并报错，避免无限占用主线程。数据模型仍由项目拥有；单元绑定失败按嵌套控件规则回滚，清理失败的单元不能复用。Unity 原生 Destroy 保持帧末销毁，不使用 DestroyImmediate。

SynchronousVirtualListDemo 提供 1000 个道具、本地增量删除、单列/三列切换及同步定位。当前通过离线编译，Unity 滚动、布局、焦点、资源释放及设备性能尚未运行验收。


## 活动子视图同步换绑

纯同步 Scope 创建的 ChildViewHandle<TViewModel,TArgs> 提供 Rebind(nextModel)，保留句柄与原生 View，显式借用新模型，不重新运行打开/关闭钩子。Presenter 沿既有 ChangeViewModel 钩子更新模型，失败时反向恢复；绑定、子准备和原工厂模型释放均使用同步入口。当前子项自己的命令或生命周期回调内禁止换绑，业务可由外部父级交互发起。

调用方必须同时检查 Status、Cleanup 和 RecoveryFailed：Applied 且 Cleanup.Failed 表示新模型已提交但旧模型释放失败；换绑失败且 RecoveryFailed=false 表示旧绑定可恢复；RecoveryFailed=true 触发同步故障关闭。新借用模型不会由失败回滚或最终销毁释放。准备/释放能力不满足时明确报告，不能转入后台清理。纯同步参数事务现已提供 UpdateArgs，见下节。


## 子视图同步参数事务

纯同步 ChildViewHandle<TViewModel,TArgs> 提供 UpdateArgs(nextArgs)。Presenter 实现独立 ISynchronousArgsUpdatePresenter<TArgs>，返回 ISynchronousPreparedArgsUpdate，不需要实现任何 Task/ValueTask 方法。准备阶段只创建隔离候选；Commit 可以同步修改模型，Rollback 必须恢复部分提交，Dispose 无条件清理尚未移交的资源。成功提交需要长期保留的资源，应由 Commit 交给实例或激活 Lifetime。

执行器先检查实例与输入资格，再屏蔽输入、准备候选并复核有效性，然后更新 Handle/Presenter 的参数并调用 Commit。提交异常后恢复原参数并调用 Rollback。无论成功或失败均同步调用候选 Dispose；只有清理结束后才允许故障关闭。回滚失败或输入屏障释放失败触发原实例关闭，关闭失败则不主动解除仍有效的屏障，避免开放损坏内容。

返回结果沿用 ArgsUpdateOutcome：Applied 且 Cleanup.Failed 表示新参数已提交，候选释放失败，不回滚已生效的新状态。历史清理失败保留到最终关闭，后续成功更新不能抹去。当前子项的生命周期/命令回调不允许更新自身，更新与换绑互斥；整个操作登记在父 Scope 的同步生命周期中，不启动异步任务。准备方在返回候选之前抛错时，仍须自行清理其未移交资源。

SynchronousChildDemo 使用 SynchronousThingPresenter，增加正常参数更新与部分提交失败后回滚两个菜单。仅离线编译通过，Unity 中的提交/回滚、输入、资源释放与关闭故障路径尚未运行验收。

### 动态内容重复赋值与重试

`DynamicViewElement.SetContent` 在资源键、模型引用均未改变且最近准备没有失败或取消时直接返回，不重建已显示实例，也不重启相同的在途加载。模型自身的属性变化通过绑定更新，不需要再次调用 SetContent。最近准备已失败或取消时，相同参数允许重新发起准备；父激活是否仍有效继续由子视图作用域检查。

异步准备结果即使为 Ready，也可能带有旧内容释放错误。该错误会和同步路径一样传给准备观察者，不代表新内容已经回滚。调用方应检查当前显示状态后决定恢复方式，不能仅根据异常推断旧页面仍在显示。实际更换 ViewModel 时，如果当前实例来自同一提供方和资源键、模型运行时类型相同、资源版本有效且已完成首次绑定，则通过 ChildViewSlot.Rebind/RebindAsync 复用原节点；首次准备、未绑定显示、提供方或资源变化仍使用候选替换。正在换绑或不可换绑的实例也会走替换队列，不并发修改同一实例。

自动复用不会从当前子界面自己的命令执行链进入换绑，以免解绑等待正在发起请求的命令。不同模型类型继续创建新候选，使未被新契约写入的控件从 Prefab 作者状态开始；显式调用句柄或槽换绑时，调用方仍需保证自身的绑定契约完整性。
