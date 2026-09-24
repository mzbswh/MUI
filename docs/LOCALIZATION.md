# 本地化文本目录与切换

MUI.Localization 只依赖 MUI.Core，包含只读目录、语义消息、格式化和文本订阅。项目负责加载语言数据、选择语言及持久化；UI 服务借用准备好的目录，始终同步刷新文本，不持有加载器或资源凭证。

## 创建与绑定

```csharp
var zh = new LocalizationCatalog("zh-CN",
    new Dictionary<string, Translation>
    {
        ["inventory.count"] = new Translation("共 {0:N0} 件道具")
    }, count => PluralCategory.Other);
var app = new Lifetime(LifetimeMode.Synchronous);
var localization = app.OwnDisposable(new LocalizationService(zh));
var text = localization.Observe(app, LocalizedMessage.Plural("inventory.count", 5),
    value => System.Console.WriteLine(value.Text));
text.Message = LocalizedMessage.Plural("inventory.count", 8);
// 项目准备另一份目录后，调用 localization.SetCatalog(nextCatalog)。
app.Dispose();
```

Observe 创建时立即格式化。带 Lifetime 的重载在取消后停止写目标，最终清理时解除订阅；无 Lifetime 的重载由调用者自行 Dispose。订阅可以写 TextElement/TMPTextElement.Content，但不能与另一绑定同时拥有该属性。回调应同步完成。

服务的构造、SetCatalog、通知和 Dispose 均不创建任务。项目需要异步加载时，等待自己的数据服务完成并检查激活状态后调用 SetCatalog；并发请求的最新意图及取消由项目协调。

## 格式与回退

普通消息参数从 {0} 起；Plural 消息把数量放到 {0}，附加参数从 {1} 起。参数数组复制，其中对象应由项目提供不可变快照。数字和日期使用 CultureInfo 复合格式；不是 ICU MessageFormat，不提供命名参数、性别选择或表达式引擎。

项目显式提供复数规则，框架不猜测语言前缀，也不实现完整 CLDR。LocalizationCatalog 可引用回退目录：复数规则使用译文所属语言，数字/日期使用当前活动语言的 Culture。LocalizedTextValue.Locale 是活动语言，TextLocale 是实际译文语言，IsRightToLeft 仅提供方向元数据。

## 切换与失败

SetCatalog 拒绝 null，同一目录引用重复设置不刷新。提交目录后同步通知订阅；格式或写入错误报告到 UIErrors，继续其他订阅，不回滚整次语言切换。缺键、错误格式和非法复数结果明确报错，目标可能保留旧文本。

订阅允许消息更新和退订，重入刷新有 32 次稳定上限。不能在文本发布回调中直接 SetCatalog 或销毁服务。Dispose 解除通知和目录引用，不释放项目资源。

## 项目侧 JSON 示例

Resource Integration 示例提供 TextAssetLocalizationProvider 和 UnityResourcesLoader，命名空间为 MUI.Samples.ResourceIntegration。它们不属于 Runtime；项目可以完全替换数据来源。示例复制 JSON 为纯文本目录后归还源资源，语言数据没有字体或贴图持有权。

## JSON 文本格式

资源例如 `Resources/Locales/en.json`，加载 key 为 `Locales/en`：

```json
{
  "locale": "en",
  "entries": [
    { "key": "inventory.title", "text": "Inventory" },
    { "key": "inventory.count", "forms": [
      { "category": "One", "text": "{0:N0} item" },
      { "category": "Other", "text": "{0:N0} items" }
    ] }
  ]
}
```

每个条目选择 text 或 forms，不能同时设置；forms 必须有 Other，可包含 Zero/One/Two/Few/Many。重复 key、重复分支、未知分支、错误语言、缺失字段均拒绝。TextAsset 提供者默认限制 10000 条和 2000000 个 JSON 字符，限制解析规模，不代表原生 I/O 或全部内存字节预算。空字符串是合法译文，缺失译文为 null。

## 验证与边界

服务及示例已完成离线编译，未新增测试；当前 API 的 Unity 文本刷新、异常与生命周期交互仍待运行验收。

字体加载、字形 shaping、完整双向排版、语言偏好持久化及远程语言包管理归项目相关系统。字号变化影响虚拟列表时，项目通过现有测量失效接口协调布局；不能把文本目录切换当成字体资源事务。
