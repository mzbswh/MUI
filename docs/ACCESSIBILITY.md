# 标准控件语义

Core 的 IAccessibleElement 提供名称、说明、值、角色、状态、阅读顺序和显式隐藏标记。uGUI Element 基类实现这些可绑定属性，TMP 适配器自动继承；它们可通过现有 Bind/BindingBuilder 写入，不增加独立的数据绑定系统。

## 元数据

- AccessibilityLabel：供读取的名称。普通 Text/TMP 文本未显式命名时回退到 Content；按钮、输入框等需明确名称。
- AccessibilityDescription：补充说明。
- AccessibilityValue：显式提供的语义值，框架不自动复制输入框/密码内容。
- SemanticRole：默认按实际适配器给出 Text/Button/Toggle/Slider/TextInput/Dropdown/Image/List/TabList，可显式覆盖；Unspecified 表示采用适配器默认值。
- SemanticState：Disabled/Selected/Checked/Expanded/ReadOnly/Busy/Invalid。显式值与原生状态合并，不能用手动标志撤销实际 Disabled；Toggle 自动反映 Checked，输入框反映 ReadOnly，TMP 下拉反映 Expanded。旧 Dropdown 暂无可靠展开状态适配。
- AccessibilityOrder：越小越先读取，相同值保留层级扫描顺序。
- AccessibilityHidden：从语义快照排除自己及后代，不改变原生渲染或点击行为。

输入控件的 Disabled 读取当前 Selectable 和祖先 View 输入门控；该计算是读取时状态，尚无独立的语义变化流。辅助描述、错误和业务选中状态需要业务绑定，而不是只依赖颜色。

```csharp
[ObservableProperty]
[Bind("Confirm", nameof(ButtonElement.AccessibilityLabel))]
private string confirmLabel = "确认购买";

[ObservableProperty]
[Bind("Quantity", nameof(SliderElement.AccessibilityValue))]
private string quantityDescription = "数量 5";
```

示例字段位于业务 partial ViewModel，需引用 MUI/MUI.UGUI 并启用既有生成器；文本本地化仍由项目文案流程提供。标准确认页同时把按钮文案绑定到语义名称，避免显示和读取名称分离。

## 快照与检查

`AccessibilityTree.Capture(view)` 返回按阅读顺序排序的只读扁平快照，包含可见的嵌套 View 内容。它排除无效/禁用的 Element 组件、未激活节点、不可见 View 和显式隐藏子树；仍可见但不能交互的 Selectable 可保留并携带 Disabled 状态。快照中的 Source 是借用引用，操作前须再次检查 IsAlive，不能把旧快照当作长期有效的对象注册表。

按钮/切换/滑条/输入框/下拉/Tab 下的被动 Text/Image 不重复成为节点，其语义由父控件名称/值表达；其中的独立交互子控件仍可保留。此规则依赖正确的适配器和角色元数据，项目自行覆写角色时需重新检查语义结果。

View Inspector 的 Validate Accessibility Semantics 检查基本角色/状态值、交互控件和非装饰 Image 的名称。选择的 Manifest 如果包含有效的正向 AccessibilityLabel 绑定，允许 Prefab 的静态名称为空；纯装饰 Element 应显式标记 AccessibilityHidden。检查仅覆盖当前 View 的绑定边界，嵌套 View 分别检查；它不初始化 View，不运行命令或创建 VM。

## 当前边界

这是控件语义和快照入口，不是 VoiceOver/TalkBack/系统读屏桥接，不负责调用平台动作接口、事件宣布、触摸探索或语义焦点同步。阅读顺序不是 EventSystem 的键盘导航顺序；键盘/手柄可达性仍需实际交互验收。值、标签的正确性也不能仅靠字段存在来证明。

Core/UGUI/TMP/Editor/TMP.Editor 编译通过，零警告、零错误；没有新增测试。尚未运行验证序列化、语义树过滤、动态状态、绑定生成或 Inspector 诊断，也未进行平台读屏验收。
