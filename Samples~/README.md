# MUI Examples

这是一套可直接运行、检查和修改的 uGUI 示例工程。**场景负责工作台，Prefab 负责界面，ViewModel 负责状态与命令，UIHost 负责页面和资源生命周期。** 不在 Start 中创建 Canvas、控件、列表模板或模拟点击。

## 打开与目录

导入 Package Manager 的 **MUI Examples**。目录使用 Unity 标准位置 `Assets/Samples/MUI/<包版本>/MUI Examples`，保留 Package Manager 的已导入识别与 Reimport 功能。打开 `Scenes/MUIExamples.unity`，或使用 **Tools → MUI → 示例**，进入 Play 后从左侧选择课程。

```text
MUI Examples/
  Scenes/MUIExamples.unity        工作台、Canvas、EventSystem、UIHost 及导航按钮
  Resources/MUIExamples/          各课程页面、弹窗、页签内容的真实 Prefab
  Resources/MUIExamples/Artwork/  资源键绑定使用的实际图片
  Art/                           共用圆角 UI 素材及开源中文字体
  Content/Lessons.json            各课演示、配置与源码说明
  Scripts/                       每个功能独立的模型与接入代码
  Editor/                        只负责打开已有场景
```

界面可以在非 Play 状态下打开 Prefab 编辑。运行时只由框架按资源键实例化这些资产。旧版的程序化布局与自动巡检源码已移到仓库 `Tools~/Diagnostics/Samples`，不随教学示例导入，也不作为推荐业务写法。

升级旧版示例时先备份个人修改，再替换原导入目录；不能将旧脚本和新示例合并使用。如果此前示例被移到了 `Assets/MUI Examples`，请先在 Unity Project 窗口中将整个目录移回 `Assets/Samples/MUI/<当前包版本>/MUI Examples`（当前版本为 0.1.0），保留资产 GUID；目标已存在时先备份并核对，避免同时保留两份示例。项目已有 MUISettings 时继续使用项目唯一配置；本示例不另带一份全局配置资产。

也可以在仓库执行 `python3 Tools~/create-basic-example.py /tmp/MUIExampleProject`，生成引用本地包的独立 Unity 2022.3.62f3 项目（输出目录必须尚不存在）。脚本沿用旧名称，现在生成完整工作台。

## 先学正常接入

先读 [SettingsViewModel](Scripts/Settings/SettingsViewModel.cs)，再看 [ExampleWorkbench](Scripts/ExampleWorkbench.cs) 的 Start / ConfigureRoutes / SelectLesson / Open：

```csharp
Generated.ExampleBindings.Initialize();
// 场景工作台 Canvas 是排序 0；页面范围显式放在工作台背景之上。
host.Initialize(renderOrder: new RenderOrderOptions(minimum: 100, maximum: 16000));
// 每个 Host 定义一次；重复打开时复用这个 definition。
var definition = SettingsViewModelDefinition.Create(
    () => new SettingsViewModel(), policy: host.ResolvePolicy());
var opened = await host.Pages.OpenAsync(definition, Unit.Value, token);
```

`SettingsViewModel` 的 UIPage 声明指向 `MUIExamples/Settings`，对应 `Resources/MUIExamples/Settings.prefab`。根挂 View；普通控件保留原生 Slider、InputField、Text、Button，View 的 nativeBindings 保存名称与组件引用。Bind 直接使用 nameof(InputField.lineType)、nameof(Slider.value) 等原生属性，生成器接线；按钮使用 IButtonElement.Clicked 命令契约。无需挂普通 Element 包装组件或手写 onValueChanged 更新业务状态。

工作台的导航按钮是项目启动层，用原生 Unity Button 连接页面路由；各页面内的业务按钮用生成命令。切换课程先关闭当前页面并等待实际 Cleanup，再打开下一页。项目切换场景前应等待 `ExampleWorkbench.ShutdownAsync()`，OnDestroy 只是不能阻止场景销毁的兜底。

右侧教学面板随课程切换，分为“演示内容”“配置与操作”“对应代码”。底部按钮可在编辑器中打开本课源码、Prefab 与教程；构建后的播放器中这些编辑器操作不可用。说明存放于 `Content/Lessons.json`，由场景中的 `ExampleLessonGuide` 读取；界面及引用均在场景中配置。

## 课程与练习

| 课程 | 配置与源码 | 操作 / 预期 |
|---|---|---|
| 属性与表单 | [表单绑定](Scripts/Settings/README.md) | 修改名称、音量；右侧预览同步。空名称显示验证错误，保存按钮不可用 |
| 页面导航 | [页面与弹窗](Scripts/Navigation/README.md) | 打开详情，点击完成返回 42，返回按钮不提交业务结果 |
| 子视图 | [子视图](Scripts/Widgets/README.md) | 替换物品更换模型，清空模型移除绑定，再次替换恢复 |
| 动态集合 | [列表](Scripts/Collections/README.md) | 增删奖励、滚动；节点由框架复用 |
| 固定槽位 | [列表](Scripts/Collections/README.md) | 五个已配置挂点；满额后提示，移除后可继续添加 |
| 虚拟列表 | [列表](Scripts/Collections/README.md) | 滚动 2,000 项，切换列表/三列网格；只物化可见项 |
| 资源绑定 | [资源绑定](Scripts/Resources/README.md) | 切换风景更换真实图片；释放图片清空资源键 |
| 异步页签 | [页签](Scripts/Tabs/README.md) | 背包 / 任务；任务首次加载失败，重试后成功；未解锁项不可用 |
| 对话框 | [页面与弹窗](Scripts/Navigation/README.md) | 确认、取消及提示结果直接显示在页面里 |
| 反馈与主题 | [反馈与主题](Scripts/Feedback/README.md) | 通知、清空队列、进度、任务输入门控和主题切换 |
| 拖放 | [拖放](Scripts/DragDrop/README.md) | 拖到接受/不可用/提交失败区域，或区域外；可开启延迟提交 |

## 场景应该怎么设置

- Canvas：Screen Space Overlay；CanvasScaler 使用 Scale With Screen Size，参考分辨率 1280 × 900、Match 为 Width（0），由 Unity 处理缩放。GraphicRaycaster 负责指针输入。
- EventSystem：一个 StandaloneInputModule。Project Settings → Player → Active Input Handling 使用 Input Manager (Old) 或 Both。本示例不增加输入系统依赖。
- WorkbenchViewport：原生 ScrollRect；WorkbenchContent 使用 VerticalLayoutGroup + ContentSizeFitter，自上而下放置标题、演示区、课程指南和页脚。布局组件均已保存在场景中。
- PageRoot：650 高的演示挂点，带 UIHost，View Root 指向自身。普通页面 Prefab 根节点使用拉伸锚点。
- DialogRoot：Canvas 下全屏拉伸挂点。ExampleViewProvider 按资源键将详情和标准对话框交给对应挂点的 LoadedPrefabViewProvider 创建，弹窗由中心锚点居中，不跟随课程滚动。所有页面共用一个 UIHost。
- LessonGuide：VerticalLayoutGroup 根据 Text 的 preferredHeight 分配正文高度；说明和文件按钮各使用 HorizontalLayoutGroup。正文改变时由 uGUI 自动更新布局。
- ExampleWorkbench：Host、Dialog Root、Navigation、Lesson Buttons、Title、Subtitle、Footer、Lesson Guide 均在场景中配置。修改侧栏项时同步路由顺序。
- 全局规则：MUISettings 保持项目唯一；普通页面解析默认策略，详情/标准对话框解析 Popup。页面排序范围由启动层指定。

## 窗口大小与布局

示例使用 uGUI 原生缩放、锚点和布局组件，不再使用自定义断点、DPI 检测或逐帧重排脚本。左侧课程栏保持固定结构；右侧演示和说明沿纵向排列，窗口较矮时使用滚轮查看。切换课程回到顶部，内部列表仍使用自身的 ScrollRect。

布局调整直接编辑场景中的 CanvasScaler、RectTransform、LayoutGroup 和 Prefab。修改参考分辨率会改变整套界面的显示大小；输入、滑条和按钮事件只更新绑定数据，不改写布局尺寸。

## 接入项目时保留什么

复制所需模型声明和对应 Prefab 契约，使用自己的业务服务及数据；在项目启动层注册生成绑定、初始化 Host 并打开页面。示例的短延迟用于直观看到异步状态，不代表真实存档或联网。页签、拖放、Loading 和主题使用框架公开 API。

示例界面默认中文，不提供语言切换，不引入额外包。随示例附带 Noto Sans SC 字体（SIL OFL 1.1，见 Art/Fonts/OFL.txt），场景和 Prefab 已设置字体引用。TMP 属于可选渲染适配。离线编译、Unity 场景运行与真实鼠标操作的验证范围应分别记录，不互相替代。

普通文本状态和按钮关联优先配置在各 Prefab 的 View 中，由编译器生成成员和连接代码；Settings 额外演示少量手写转换校验与计算属性。修改关联配置后自动同步和编译，不需要每次点击生成按钮。固定的外观、输入限制和取值范围直接由原生组件保存。
