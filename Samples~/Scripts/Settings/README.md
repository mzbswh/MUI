# 属性、输入与命令

[返回总览](../../README.md)

打开 `Resources/MUIExamples/Settings.prefab` 对照 [SettingsViewModel.cs](SettingsViewModel.cs)。布局已经制作好，修改字号、颜色、位置直接编辑 Prefab。

| Prefab 节点 / 组件 | 模型设置 | 行为 |
|---|---|---|
| PlayerName / InputField | PlayerName → text，TwoWay，PlayerNameConverter | 空值保留输入草稿并显示错误，有效值去掉两端空格 |
| Initials / Text | Initials 计算属性，PlayerName 的 NotifyPropertyChangedFor | 名称变化更新头像缩写 |
| NamePreview / Text | PlayerName → text | 同一模型字段更新右侧预览 |
| PlayerName 的原生 Text / Placeholder | InputField 的序列化引用 | 负责输入显示，关闭富文本；不再运行时构造 |
| PlayerName / characterLimit | Prefab 固定配置为 16 | 不需要生成模型字段 |
| Volume / Slider | 范围 0–1，value 双向绑定 | 用户拖动或模型赋值均能同步 |
| Slider 的 Fill/Handle | Prefab 中的原生引用 | 滑块的视觉结构，可直接修改 |
| VolumeLabel / Text | VolumeCaption 计算属性 | 用 DependsOn 声明 Volume 依赖，生成器自动通知计算属性 |
| NameError / Text | NameValidation → NameError | 转换错误显示在表单下方 |
| Save / Reset / ToggleNameLock | Prefab 关联 Button.onClick | 自动接入命令可用性和执行状态 |

[PlayerNameConverter.cs](PlayerNameConverter.cs) 是项目侧验证。[SettingsViewModel.cs](SettingsViewModel.cs) 的 SaveAsync 用 CommandContext.Token 模拟异步保存，用 context.Apply 更新当前仍有效的页面。

练习：清空名称 → Save 禁用；恢复有效名称 → 保存可用；音量为 0 → Save 禁用；Save 执行期间不可重复提交；Lock name 后输入只读，模型仍能更新控件；Reset volume 恢复 50%。Save 不写磁盘或服务器，接真实业务时替换延迟部分。

代码绑定直接引用原生属性，例如 `[Bind("PlayerName", nameof(UnityEngine.UI.InputField.lineType))]`。
生成器推导组件和属性类型，并缓存强类型访问适配器。InputField.text / Slider.value 自动接入原生通知和无通知 setter；其他原生属性默认单向或首次赋值。
项目自定义属性可用 `[Bind("Control", nameof(MyProperties.CustomValue))]` 引用 `ControlPropertyExtension<T>`，通过 `[NotifiesChange]`、`Listen` 和 `Changed` 提供反向通知。普通组件属性不需要逐个编写 Element 包装。

同名多组件示例：`NamePreview` 节点同时保存 Text 和 LayoutElement 引用，自动生成 `NamePreviewTextEnabled` 和 `NamePreviewLayoutEnabled`，分别绑定 `Text.enabled`、`LayoutElement.enabled`。两条声明的绑定名和属性名相同，生成器仍按组件类型分别定位。LayoutElement 保留默认布局值；同类型出现多个候选时，在 View Inspector 手动拖入具体组件。

普通状态（Volume、Status、NameReadOnly）和按钮入口配置在 Prefab 的 View 绑定列表。输入限制、Slider 范围等固定值直接编辑原生组件。代码只保留转换校验、计算属性、异步保存和业务行为；生成按钮可实现 partial 方法，也可关联已有 [Command] 方法（SaveCommand 保留 CanExecute）。

绑定配置修改后自动同步并请求编译；编译器检查属性、组件类型、命令事件、成员冲突和绑定方向。无需逐次点击“生成绑定”。命令直接连接 Button.onClick / interactable，不再经过 IButtonElement 或 NativeButton。
