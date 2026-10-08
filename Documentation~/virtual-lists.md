# 虚拟列表

VirtualListElement 负责视口布局、条目物化与复用。项目把数据组织为带唯一稳定键的 ObservableList<VirtualListItem>；分页请求、分组/树形展开和多选规则由项目处理。

## 结构与尺寸

ScrollRect 必须明确配置 Viewport 和空 Content；条目模板为同一列表边界内、Content 外的非激活 View。列表拥有 Content 尺寸和条目根节点排布，不能在 Content 上另加 LayoutGroup 或 ContentSizeFitter。条目内部可以使用布局组件。

初始化前调用 Configure，默认纵向滚动；axis 为 RectTransform.Axis.Horizontal 时使用横向单列。纵向支持 columns 对应的 Grid 配置。启用自动列数或 columns 大于 1 时，所有单元格的滚动轴尺寸统一使用 Configure 的 itemExtent，交叉轴宽度为 扣除内边距和列间距后的可用宽度除以列数；不会应用条目自己的 Extent 或逐项测量。切回单列后恢复逐项尺寸规则。示例调用：

```csharp
list.Configure(scroll, template, itemExtent: 120,
    axis: UnityEngine.RectTransform.Axis.Horizontal);
list.ConfigureSizeMeasurement(true, perFrame: 2);
```

初始化前调用 `list.ConfigureAutomaticColumns(minimumWidth: 160)` 启用自动列数。列数按扣除左右内边距的可用宽度计算，计入相邻列之间的间距后向下取整，至少一列；单元格均分宽度。窗口变化时重新计算列数并保留阅读锚点，自动模式下一列仍使用统一尺寸。`Columns` 返回当前列数，自动模式不允许手动赋值。计算列数超过 capacity 时明确失败，不静默截断；恢复可用尺寸后可调用 RetryAsync 重试。

单列列表的 VirtualListItem.Extent 为可选滚动轴尺寸，横向为宽度、纵向为高度。未指定时采用估算值或测量值。字体、语言、模板布局改变后调用 InvalidateSizeMeasurements；测量只处理已物化条目，不为取得远处位置创建全部前置条目。

`VirtualListItem` 的可选 `contentVersion` 用于在同一来源内跨条目实例保留测量：稳定键、显式版本与模板相同时可复用旧尺寸，内容改变须升级版本。不指定版本时仅在同一条目实例内复用。换源、Reset、列数或交叉轴约束变化会清空相关缓存；字体、主题等外部布局规则改变后调用 `InvalidateSizeMeasurements()`。图片加载改变局部布局时可调用 `InvalidateItemSize(key)` 仅重测该项；`ObservableList.NotifyUpdated(index)` 也会明确使该项测量失效，保持阅读锚点。

如果已物化条目的布局返回零、NaN 或无穷尺寸，列表报告该条目错误并暂用估算尺寸；该结果会缓存，避免逐帧重复测量和诊断。条目更新或调用 InvalidateSizeMeasurements 后会重新测量。

## 间距与内边距

初始化前可调用 `list.ConfigureSpacing(new Vector2(12, 8), new RectOffset(16, 16, 20, 20))`。Vector2 对应物理 X/Y 间距，RectOffset 按左、右、上、下提供内边距，均使用 Content 局部 Canvas 单位且非负。列表复制内边距配置，不受调用方后续修改影响。

首尾保留内边距，间距只出现在相邻条目或行之间。自动列数、内容总尺寸、条目布局、定位和可见范围使用相同配置。视口完全位于空隙时可见范围为空；位置快照允许负的锚点偏移，以恢复条目前方的空隙。可用横向空间不足时单元格宽度收缩到零，不制造负尺寸。

## 定位与阅读连续性

```csharp
var result = await list.ScrollToIndexAsync(500,
    alignment: VirtualListAlignment.Center, duration: 0.3f);
```

定位与重试统一使用异步 API，立即完成时不人为等待一帧；旧 ScrollToKey / Retry 同步入口已移除。

按索引请求在接纳时解析为稳定键；也可直接调用 ScrollToKeyAsync。对齐支持 Nearest、Start、Center、End，首尾位置受合法滚动范围钳制。duration 为零立即滚动，正数使用非缩放时间。MaxRevealCorrections 限制动画后的布局修正次数，未收敛返回 Failed。

有效增量更新重新解析目标键；删除目标返回 NotFound。新的定位、换源或重置替代旧请求，拖动和滚轮取消旧请求；底层资源准备及清理仍由列表生命周期负责。

普通更新保留首个可见条目和条目内偏移，删除锚点时使用原顺序的后继、其次前驱。视口沿滚动轴改变尺寸时，先记录变化前的阅读锚点，再按新边界补偿；末尾跟随依据变化前的视口范围判断，补偿重设原生拖动基准。换源和重置默认回到起点。FollowEnd 默认关闭；开启后只有更新前已在 EndFollowTolerance 内、且没有拖动或显式定位时才继续跟随末尾。

## 保存与恢复位置

```csharp
var position = list.CapturePosition();
// 重新打开页面并设置同一 Items 来源后：
var restored = await list.RestorePositionAsync(position);
```

快照保存来源身份、锚点稳定键、条目内偏移和原顺序的键，不保存 View、模型、资源凭证或来源集合。键须使用独立、不可变的业务标识。捕获会复制所有键，时间和空间复杂度为 O(n)，仅在需要保存位置时调用。

恢复默认要求同一个来源集合；不同来源返回 IncompatibleSource，当前位置不变。项目确认两个来源的键空间兼容后，可以显式传入 allowCompatibleSource: true。锚点已删除时使用原顺序中仍有效的后继、其次前驱，均不存在则回到起点，UsedFallback 为 true；空快照恢复到起点。

恢复按当前方向、列数和测量尺寸重新计算位置，完成条件及修正上限与定位一致。新定位、换源、重置或关闭使旧恢复失效，用户拖动或滚轮取消恢复。恢复期间目标再次删除返回 NotFound，调用方可按当前数据重新请求。捕获要求列表已激活。

## 范围通知

Viewport 快照提供 [StartIndex, EndIndex)、VisibleCount、DistanceFromStart、DistanceFromEnd 和 IsScrolling。范围不含预留行，描述几何相交，不保证异步条目已经就绪。

通过列表的 PropertyChanged 观察 Viewport，通知按帧合并；退出时解除监听。项目可据边界距离接入加载更多，但须自行管理请求去重和失败重试。所有距离使用 Content 局部 Canvas 单位。

## 逻辑焦点

项目输入适配可调用 `MoveFocusAsync(MoveDirection)` 按列表轴向或 Grid 行列移动，并自动将目标滚入视口。Grid 左右移动不跨行，末行缺少对应列时返回 NotFound。`FocusItemAsync(key, elementName)` 可指定条目 View 内的绑定元素名，控件不存在、隐藏或禁用时依次使用该条目的默认控件和首个可交互控件。

物化条目的原生 Selectable 自动接入方向事件，先按原生规则在条目内部移动，到条目边界后使用稳定键寻找相邻项；Slider/Scrollbar 保留沿自身轴向调值，Navigation.None 保留禁用导航的配置。页面失焦保存逻辑条目与控件，重新获得输入资格时恢复；缓存再激活清除上次页面焦点。异步物化期间用户选择其他控件会使旧焦点请求返回 Superseded，旧请求不抢回选择。独立 EventSystem 使用宿主配置的系统，恢复失败才按页面默认及有效控件回退。

`CaptureFocus()` 返回当前已物化条目的稳定键、元素名和原生控件路径快照；焦点不属于当前列表时返回 null。使用 `RestoreFocusAsync(snapshot)` 显式恢复，增量插入或移动按稳定键跟踪，换源、Reset 或结束激活后旧快照返回 IncompatibleSource。快照不保存原生控件、View 或模型；未挂 Element 或位于子 View 的控件使用从条目 View 根开始的唯一节点名称路径。同级重名或路径失效时，依次回退到条目默认控件、条目有效控件、所属页面默认控件及页面有效控件；全部失效时清空选择并返回 NoSelectable。

Unity 2022.3.62f3 编辑器内已运行 2000 条动态尺寸演示：初次定位、视口变窄、字号改变和单项内容更新均返回 Ready，工作集为 4–5 个条目。条目回收、换源和 Reset 会解除原生焦点及页面保存的节点引用，避免焦点随复用对象转移到其他数据；页面可按自身规则选择回退控件。

## 条目失败与重试

条目的实例创建、准备或绑定失败只撤销该条目，保留有效空白占位尺寸；其他条目继续准备，列表仍可处于 Ready。`GetItemFailure(key)` 返回当前来源及内容的失败，`FailedItemCount` 提供数量，`ItemFailed` 通知项目错误表现。重复观察复用异常的 `DiagnosticId`，不会重复输出。

通过 `await list.RetryItemAsync(key)` 显式重新物化指定条目。该入口会使目标进入视口，沿用定位的取消、代际和容量检查；目标准备再次失败返回 Failed。滚动不会自动重试失败条目；更新同键内容、换源或 Reset 会使旧内容的失败失效。配置和几何错误仍使用整表 Error 及 `RetryAsync()`。

初始化前可调用 `ConfigureItemFailureTemplate(template)` 提供纯视觉失败模板；模板须是同一边界内、Content 与条目模板外的非激活 RectTransform，不包含 View/Element，也不能跳过父 CanvasGroup。失败画面不接纳输入，仅在视口附近物化。

父页面换绑时，列表在旧来源和旧位置仍有效时准备新视口；提交前不替换订阅或显示节点。候选准备失败保留旧 UI，已进入提交的异常交给页面故障关闭。该流程及条目故障隔离的完整运行矩阵仍待验收。

## 性能观察

`list.MeasurementSnapshot` 返回最近维护帧的 `Frame`、该帧原生测量尝试数 `Attempts` 和此 Element 创建以来的 `TotalAttempts`。读取要求 Element 存活且位于主线程，不会强制布局或物化条目；首次维护前 Frame 为 -1。计数包括测量失败和无效尺寸回退，累计值达到 long.MaxValue 后保持饱和。读取发生在本帧 LateUpdate 前时，Frame 可能仍是上一帧，须依据 Frame 对齐采样。

Unity Profiler 提供以下标记：

| 标记 | 观察范围 |
| --- | --- |
| `MUI.VirtualList.Maintenance` | LateUpdate 与 RefreshCells 的同步维护片段；同步重入及两者嵌套只计一次 |
| `MUI.VirtualList.LateUpdate` | 每帧列表维护，包括尺寸修正、刷新与范围发布 |
| `MUI.VirtualList.RefreshSlice` | 每次刷新迭代器推进的同步执行片段 |
| `MUI.VirtualList.VisibleRange` | 刷新时计算物化范围 |
| `MUI.VirtualList.CellLayout` | Content 尺寸与单元布局写入 |
| `MUI.VirtualList.NativeMeasurement` | 已物化条目的原生布局重建及首选尺寸读取 |
| `MUI.VirtualList.ItemBinding` | 设置子节点模型触发的同步绑定和准备入口 |

标记结束后才等待异步准备，不把等待时间当作 CPU。Maintenance 包含在其片段内同步执行的绑定与项目回调；项目应给业务绑定和资源后端增加自身标记，并在相同帧和调用范围内核对开销。异步完成后的其他回调、原生 Canvas 后续重建及数据源批量变更不由该维护标记完整覆盖。各子阶段是包含关系，不能简单相加作为总耗时。

使用 ProfilerRecorder 时按 `SumAllSamplesInFrame` 汇总单帧调用，容量须大于零，在下一帧读取 `LastValue`。具体语义参见 Unity 的 [帧内汇总](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorderOptions.SumAllSamplesInFrame.html) 和 [LastValue](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorder.LastValue.html)。GC 每帧总量还包含 Unity、输入、渲染及其他脚本，应记录采样环境，不直接归因于列表。

100/1,000/10,000 项、固定/测量尺寸和双模板的桌面 IL2CPP 基线见 [性能记录](../docs/VIRTUAL-LIST-BASELINE.md)。该记录不包含目标设备预算或异步资源加载性能。

## 当前限制

Unity 2022.3.62f3 的 macOS IL2CPP Player 已通过真实方向键从 1501 连续移动到 1513，跨视口与普通/featured 模板保持正确稳定键。Editor Play Mode 已验证 Secondary 原生控件的逻辑移动、回收后的自动恢复，以及物化期间新用户选择使旧请求失效。真实拖动、Grid/Slider 方向交互、全部竞争路径与列表 Player 资源退出仍需完整验收。
