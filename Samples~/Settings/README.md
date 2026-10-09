# Settings sample

> 最终验收：2026-10-09 的设计必需条件已闭合，具体后端、输入方式及证据见包内 `docs/IMPLEMENTATION-STATUS.md`。本文中“尚未验收/离线编译”等开发阶段说明保留当时范围；当前状态以该记录顶部的最终审计为准，未列入原设计的补充操作不扩展完成门槛。

1. Import **Settings** from the MUI package in Unity Package Manager.
2. Choose **Tools → MUI → Samples → Open Settings Scene**, then enter Play mode. The launcher keeps the scene camera. Alternatively add `SettingsDemo` to an empty GameObject in a scene with an active Camera; the camera prevents Unity's “No cameras rendering” message from covering the Overlay UI.
3. Move the slider: generated two-way binding updates Volume and the status label.
4. Click Save: the generated async command disables its button while saving, then updates Status through CommandContext.Apply. At volume zero, CanExecute disables Save. Reset uses the same generated async command contract with an immediately completed business delegate.

Enable Automatic Walkthrough on SettingsDemo to see these actions demonstrated automatically. The editor launcher also provides a bounded `RunPreviewBatch` entry point for headless Play-mode preview; it creates a sample scene, runs for three seconds, logs the walkthrough and exits. It operates on the supplied project, so use a disposable project for unattended preview.

The sample uses the legacy EventSystem input module. Enable the legacy input backend (or Both), or supply an EventSystem configured for the project's input backend. An EventSystem is created only when none exists.

This sample directly drives binding, not a Navigator. It does not yet exercise Presenter hooks, result completion or UI resource loading.

The analyzer DLL is included under Analyzers with the RoslynAnalyzer label. After changing generator source, rebuild it with `bash 'Tools~/publish-generator.sh'`.

## 滑动条范围绑定

示例的 MinimumVolume、MaximumVolume、DiscreteVolume 分别绑定 MinValue、MaxValue、WholeNumbers，当前生成代码先写入这三个配置，再建立 Volume 的双向绑定。默认范围仍为 0–1，默认允许小数。SliderElement 同时提供 Direction，直接更新原生方向，不翻转整个 RectTransform。

Value 和范围拒绝 NaN/Infinity。范围和整数模式写入沿用原生裁剪/舍入，并通知实际属性变化；即使界面处于隐藏准备阶段，也不会仅依赖被输入门控拦截的原生事件。配置写入可能触发项目注册的原生 onValueChanged 监听器，不保证静默。项目应先设置有效范围和整数模式，再写入期望值；多属性配置不是原子事务。直接操作原生 Slider 不由 Element 保证完整通知。

已验证示例和依赖编译、生成绑定顺序及公开菜单启动后的实际画面。真实鼠标拖动与动态范围切换仍须单独验收；已有原生属性/事件观察不代替真实输入。

## 显示名输入绑定

PlayerName 输入框展示 Standard 内容预设、单行、16 字符上限、ReadOnly 单向绑定及公共 IInputFieldElement 文本双向绑定。PlayerNameConverter 去除首尾空白；空名称保留输入草稿及原模型值，NameValidation 接收校验失败，NameError 显示错误，Save 暂停接纳。有效名称清除错误并回写规范化文字。点击 Lock / Unlock 切换用户编辑权限；模型仍可主动更新只读文本。Save 沿用原示例的延迟命令，将当前显示名和音量显示在状态文字中，不保存到账号或服务器。Reset 仍只重置音量。

输入节点构建位于 SettingsDemo.Input.cs，业务示例状态位于 SettingsViewModel.cs。生成代码先配置输入模式与限制，再建立文本绑定；输入框显示文字关闭富文本。这里只演示旧版 InputField，不能用它证明 TMP、输入法或移动软键盘行为。已完成属性绑定和切换命令的生成/编译检查，并核对实际输入布局；真实输入与只读切换仍待验收，自动 walkthrough 不替代手动输入。

## 独立滚动条契约

ScrollbarViewModel 为单独的绑定示例，不自动加入 SettingsDemo。项目可创建自己的 View，在名为 Position 的原生 Scrollbar 上挂 ScrollbarElement，再使用生成的 ScrollbarViewModelBindingFactory 创建绑定。Steps、HandleSize、Position 分别对应步数、手柄比例与双向当前值；初始连续值为 0.25、手柄比例为 0.2。控件还支持 Direction、Interactable，以及 UIAutomation.SetInput<float>。

值和手柄比例沿原生规则限制到 0–1，拒绝非有限值；步数必须非负，0/1 为连续值，大于 1 分档。程序 Value 写入使用 SetValueWithoutNotify，真实原生事件经 View 输入门控后通知绑定；配置步数可能按原生规则修改当前值。批量挂载识别 Scrollbar，命名建议前缀为 Sbr_。如果滚动条已经由 ScrollRect 管理，不要再用独立模型写入其值和比例，避免争抢控制。此示例已完成生成/编译检查，未提供配套 Prefab，实际拖动与编辑器挂载尚未运行验收。

View 结构检查会报告缺失手柄、手柄指向根节点/外部层级和跨嵌套 View 引用。绑定契约检查还会扫描所检查 View 层级内（包括非激活节点）的原生 ScrollRect；若它引用该滚动条，而模型又正向写入 Value 或 Size，则报告双方来源。OneWayToSource 仅观察不冲突；方向和交互开关也不按位置写入处理。此检查不扫描其他场景对象，不证明运行时动态改接或自定义驱动不存在冲突。

输入框 CharacterLimit 使用 OneTime 绑定，建立绑定时读取一次 NameCharacterLimit，后续修改该属性不会刷新当前控件；重新绑定会读取新值。其余输入值绑定保持原有模式。

## 显式程序集注册

SettingsModule.cs 通过 ViewModule 指定 MUI.Samples.Settings.Generated.SettingsBindings；SettingsDemo 在创建绑定前调用 Initialize。项目可同样指定稳定的入口名称，无需逐个登记模型工厂，也不使用运行时反射扫描。重复调用沿用注册表幂等规则。
