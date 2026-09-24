# UI 资源绑定与项目资源系统

UI 框架只管理界面的资源持有期和显示更新。加载、下载、共享缓存、引用计数、预算及最终卸载由项目资源系统负责。

## 常规接入

已有资源对象时，直接绑定 `ImageElement.Sprite`、`RawImageElement.Texture`、`TextElement.Font` 或 `TMPTextElement.FontAsset`。这些属性借用对象，不取得卸载权。资源所有者必须覆盖控件使用期。

需要资源键绑定时，在 View 激活前注入项目加载器：

```csharp
// 完全同步：项目后端必须真正提供同步加载与释放。
view.ConfigureSynchronousResources(projectLoader);
```

异步后端使用 `view.ConfigureResources(projectAsyncLoader)`。绑定 `SpriteSource`、`TextureSource` 等 Source 属性即可请求显示更新，支持 OneWay 和 OneTime。空键清空显示；失败保留旧显示并报告错误。Source 托管期间不能同时直接写同一对象属性。

父 View 的加载器可由最近子 View 借用，资源持有期仍属于子 View 自身。加载器由项目持有，View 不销毁加载器。配置只能在激活前或上次清理成功后修改。

`WaitForResourceSources` 默认关闭；开启后，本 View 默认加载器发起的初始 Source 请求纳入显示准备。后续换键仍渐进更新。它不负责网络调度、资源预下载或全局缓存。

## 界面逻辑加载

项目逻辑可以通过现有资源服务取得资源持有凭证，再交给 `Lifetime` 兜底释放。`Lifetime.Load/LoadAsync` 位于 `Samples~/ResourceIntegration`，是项目侧接线示例，只调用项目后端并登记返回凭证，不共享请求或缓存资产。

```csharp
// activation 为允许异步的界面激活生命周期。
// 需要导入 Resource Integration 示例；这是项目扩展，不是 Runtime API。
var sprite = await activation.LoadAsync<UnityEngine.Sprite>(projectLoader, key, token);
activation.Token.ThrowIfCancellationRequested();
// 随后更新表现数据；项目须保证解除控件引用早于凭证最终归还。
```

示例中的完全同步扩展使用 `activation.Load<T>(projectSynchronousLoader, key)`；同步后端不能通过等待异步任务模拟同步。异步加载器即使忽略取消，迟到资源也必须归还，不能写入已结束的界面。

归还凭证只代表 UI 放弃本次持有权。它不代表可以直接 Destroy 共享资源，也不保证物理内存立即下降。

## 控件内部实现

`ResourceSlot<T>` 已移动到 `Runtime/Rendering/UGUI/Resources` 并设为内部类型，不再属于公开资源接入 API。`CreateSpriteSlot/CreateTextureSlot/CreateMaterialSlot/CreateFontSlot` 及同步对应入口均为控件私有实现。

槽只协调一个显示位置：

- 新请求使旧请求失效，迟到结果归还项目后端。
- 新资源成功赋值后归还旧资源；失败保留旧显示。
- 停止显示前保持当前持有权，解除原生引用后归还。
- 原生赋值结果不确定时保留可能仍被使用的凭证并报告失败，避免提前卸载。

业务不需要了解槽、请求代际或冻结状态。特殊控件通过 Source 配置接口接入，或自行将资源持有权交给 Lifetime。

## 字体与材质

TMP 字体直接借用项目提供的 FontAsset。框架不管理字体加载、图集、回退字体和共享材质。TMP 的 font = null 会回退默认字体，不能作为解除所有引用的证据。

uGUI 已有 FontSource 和 MaterialSource 是可选属性适配，不定义字体或材质缓存。字体与材质匹配由项目负责；字体变化影响虚拟列表高度时，调用现有测量失效接口。

## 已移出的能力

共享加载器、资源预算和全局内存回收协调器已删除。具体 Unity Resources、JSON 目录读取及带 Prefab 驻留账本的提供方在 `Samples~/ResourceIntegration`，只供项目接入参考，不随框架 Runtime 导入。

UIHost 不订阅平台低内存事件。项目可显式调用 `ClearInactiveContent/ClearInactiveContentAsync` 清理该宿主的预加载和停用页面；Tab 使用 `ClearCache/ClearCacheAsync`。框架不协调项目资源池。
