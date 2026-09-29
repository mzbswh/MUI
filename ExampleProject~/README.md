# MUI 基础示例工程

本目录保留独立工程模板设置。基础验收请在空 Unity 2022.3.62f3 工程中添加本地 MUI 包，并从 Package Manager 导入 Basic Example。需要生成备用独立工程时，在仓库根目录运行 `python3 Tools~/create-basic-example.py /private/tmp/MUI-BasicExample`；工具从 `Samples~/BasicExample` 复制资源，再复制本目录的 Packages 与 ProjectSettings，并更新本地包引用。输出目录必须尚不存在，且位于 MUI 包目录之外。

在生成工程中打开 `Assets/Basic/Basic.unity`，然后进入 Play Mode。也可使用 `MUI > Basic Example > Open Scene` 菜单。

场景包含 `UIHost`、`EventSystem` 和 `BasicDemo`。运行后，示例通过纯同步类型化 Route 打开真实的 `BasicView` Prefab；标题来自生成的 ViewModel 绑定。点击 **Done**，页面返回整数 `42`；重新打开按钮显示 `42: Reopen`，Console 显示结果与清理状态。点击该按钮可再次打开页面。

项目运行时代码在 `Assets/Basic/Scripts`，场景菜单和构建目录登记在 `Assets/Basic/Editor`。进入 Play Mode 前可执行 `Tools/MUI/Validate Build Catalogs` 检查示例页面的 Prefab 与生成绑定 Manifest；Player 构建也会执行相同校验。示例直接引用 Prefab，不包含资源下载、持久化或平台服务接入。
