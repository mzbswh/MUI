# 资源键绑定

[返回总览](../../README.md)

`Resources.prefab` 的 Artwork 是 Image + ImageElement；[ResourcesViewModel.cs](ResourcesViewModel.cs) 的 Artwork 字段绑定 SpriteSource。实际图片在 `Resources/MUIExamples/Artwork/Forest.png` 与 `Dunes.png`，不是运行时生成的纹理或假加载器。

UIHost 默认 UnityResourcesLoader 同时注入页面资源上下文，资源键使用 `MUIExamples/Artwork/Forest`，不包含 Resources 和扩展名。Change landscape 改键，Release artwork 赋 null。框架接管每次获取的凭证，旧资源和页面退出时按生命周期归还。

接 Addressables/项目资源系统时实现 IResourceLoader，并在 `host.Initialize(resourceLoader: loader)` 注入。宿主借用加载器，后端必须活到所有资源凭证归还。此课展示正常资源使用；取消后的迟到归还等协议检查留在维护目录，不混入主用法。
