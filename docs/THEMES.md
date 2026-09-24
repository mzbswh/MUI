# 主题 Token 与切换

MUI.Themes 不引用 Unity；主题目录是复制后的只读值集合，模块仅依赖 MUI.Core，ThemeToken<T> 通过语义名称和类型读取值。支持 ThemeColor、ThemeStateColors、float、bool、string；拒绝其他类型和 null 值。颜色通道为有限的 0–1 值，标量必须有限，负间距等业务约束由消费者决定。string 可描述字体资源 key，但不持有字体或贴图对象。

## 定义与接入

```csharp
var surface = new ThemeToken<ThemeColor>("surface.panel");
var spacing = new ThemeToken<float>("spacing.items");
var dark = new ThemeCatalog("dark", new[]
{
    ThemeValue.Create(surface, new ThemeColor(0.12f, 0.14f, 0.18f)),
    ThemeValue.Create(spacing, 12f)
});
var light = new ThemeCatalog("light", new[]
{
    ThemeValue.Create(surface, new ThemeColor(0.95f, 0.96f, 0.98f)),
    ThemeValue.Create(spacing, 12f)
});
var themes = appLifetime.OwnDisposable(new ThemeService(dark));

// panelImage 是 UnityEngine.UI.Image，itemsLayout 是 HorizontalOrVerticalLayoutGroup。
MUI.UGUI.ThemeBindings.BindColor(themes, activationLifetime, panelImage, surface);
MUI.UGUI.ThemeBindings.BindSpacing(themes, activationLifetime, itemsLayout, spacing);
themes.SetCatalog(light);
```

示例省略 using 与项目变量。构造时传入项目已准备的初始目录。Observe(owner, token, apply) 也可用于项目自定义控件；会立即赋值、登记到 owner，在 owner.Cancel 后停止写入，在 owner.Dispose 后解除订阅。返回 IDisposable 可提前解除，登记失败也会解除。回调必须同步完成，并独占对应目标属性的写入权。

## 完全同步接入

ThemeService 始终同步应用数据和释放订阅，不需要选择执行模式，也不提供加载方法。

```csharp
var app = new Lifetime(LifetimeMode.Synchronous);
var spacing = new ThemeToken<float>("spacing.items");
var compact = new ThemeCatalog("compact", new[] { ThemeValue.Create(spacing, 8f) });
var comfortable = new ThemeCatalog("comfortable", new[] { ThemeValue.Create(spacing, 16f) });
var themes = app.OwnDisposable(new ThemeService(compact));
themes.Observe(app, spacing, value => System.Console.WriteLine(value));
themes.SetCatalog(comfortable);
app.Dispose();
```

需要异步获取主题配置时，项目自行等待配置服务完成、检查页面或应用生命周期，再调用 SetCatalog。服务不接管加载任务、取消策略或资源凭证。Dispose 只解除订阅与目录引用，不卸载项目资源。

## 控件状态

按钮等 Selectable 使用 ThemeToken<ThemeStateColors>，一次提供 Normal、Highlighted、Pressed、Selected、Disabled 五个颜色。ThemeBindings.BindSelectableColors 只支持 ColorTint 转换，通过整体 ColorBlock 更新保留原生状态机，并保留当前 colorMultiplier/fadeDuration。不要同时把按钮 targetGraphic.color 交给 BindColor 或另一高亮脚本；SpriteSwap、Animation 主题状态适配尚未实现。

BindColor 接受 Graphic，因此也能用于原生 Text 与 TextMeshProUGUI 的基础颜色，不要求核心模块依赖 TMP；复杂文本材质、渐变和字体资产仍由对应渲染配置负责。

## 提交与失败

SetCatalog 接收非空的只读目录；同一目录引用重复设置不刷新。目录可指定 fallback，缺失 Token 向后查找，错误类型不被 fallback 隐藏。

提交前检查全部活跃订阅需要的 Token，缺失或类型错误保留旧主题且不写目标。通过校验后提交目录并逐个同步通知；目标写入抛错报告到 UIErrors，继续其他目标，不回滚已提交主题。方法正常返回不代表每个原生控件都成功刷新，应观察错误报告。

发布回调不能重入 SetCatalog 或 Dispose，应安排到本轮发布结束后执行。订阅可在回调中释放。所有目录与 Token 值的准备、持久化、异步请求顺序由项目负责；服务只借用显示数据，不管理字体或贴图。

## 验证范围

Themes 与 UGUI 编译通过，零警告、零错误；没有新增测试。尚未运行验收主题校验拒绝、实际颜色和控件状态、布局重建、目标异常或生命周期清理。


## 用户字号与减少动画

`Core/Accessibility/UIUserPreferences` 是 UI 线程上的独立可观察设置，默认 FontScale=1、ReducedMotion=false。FontScale 必须正且有限；该对象不读取/写入项目设置文件，也不在主题更换时重置。订阅按 Lifetime 管理，取消后停止写目标，最终释放时解除；观察者异常隔离到 UIErrors，同步重入有 32 轮上限。

```csharp
var preferences = new UIUserPreferences { FontScale = 1.25f, ReducedMotion = true };
var bodySize = new ThemeToken<float>("font.body.size");
// 活动主题应包含 ThemeValue.Create(bodySize, 24f)。
MUI.UGUI.PreferenceBindings.BindFontSize(themes, preferences, activationLifetime, bodyText, bodySize);
MUI.TMP.TMPPreferenceBindings.BindFontSize(themes, preferences, activationLifetime, tmpText, bodySize);
MUI.UGUI.PreferenceBindings.BindReducedMotion(preferences, activationLifetime, confirmButton);
```

bodyText 是原生 Text，tmpText 是 TextMeshProUGUI，confirmButton 是 Selectable。同一文本选择对应的一种适配，TMP 仍为可选程序集。字号始终从主题基准 × 当前用户缩放计算，不读取缩放后的当前字号累乘；主题和偏好任意一方改变都会更新。合成值必须正且有限，Text 转成至少 1 的整数（中点远离零舍入），超出整数范围拒绝；TMP 保持浮点字号。

Text best-fit/TMP auto-sizing 会抵消用户指定字号，因此绑定前和刷新时均拒绝这两种模式，项目需要明确关闭它们。字号变化触发原生文本布局失效，但没有据此实现虚拟列表重测、锚点或焦点恢复。非法字号/原生写入异常仍遵循订阅错误报告，不回滚已经改变的设置/主题。

减少动画适配目前只控制 Selectable 的 ColorTint.fadeDuration：启用时为 0，关闭时恢复绑定时的作者值，保留配色和其他 ColorBlock 字段，因此可与 ThemeBindings.BindSelectableColors 组合。解绑时先解除偏好订阅，再恢复该作者值，避免池化节点下一轮误把 0 当作默认时长。同一 fadeDuration 只能由一个绑定拥有，运行中改为其他 Transition 不受支持。恢复已销毁对象时跳过原生访问。

这不是页面转场、Animator、滚动惯性或所有动效的全局关闭。后续动画提供者仍需消费同一偏好；平台读屏语义和完整无障碍支持也没有随这两个设置自动完成。Core/Themes/UGUI/TMP 编译通过，零警告、零错误；未新增测试，字号/动效/池化恢复尚未 Unity 运行验收。


## 程序集引用

纯主题服务位于 `MUI.Themes`。使用 `MUI.UGUI.ThemeBindings` 或 `MUI.UGUI.PreferenceBindings` 的项目 asmdef 需引用 `MUI.UGUI.Themes`；使用 `MUI.TMP.TMPPreferenceBindings` 需引用 `MUI.TMP.Themes`。命名空间和方法调用不变，基础 `MUI.UGUI` / `MUI.TMP` 不再传递主题依赖。项目直接使用 ThemeService/ThemeToken 时也应显式引用 `MUI.Themes`。

`PreferenceBindings.BindReducedMotion` 与字号绑定位于同一工具类，因此也位于 `MUI.UGUI.Themes`；页面转场的 ReducedMotion 仍只依赖 Core。两个主题适配程序集使用原生控件，不要求引用基础 MUI 渲染程序集。TMP.Themes 使用与 TMP 相同的 TextMeshPro 安装条件。同一 UPM 包仍会编译满足条件的程序集，拆分不等于按项目引用自动剔除源码。
