# MUI 基础示例工程

本目录是位于 UPM 包内的工程模板。先在仓库根目录运行 `python3 Tools~/create-basic-example.py /private/tmp/MUI-BasicExample`，再用 Unity 2022.3.62f3 打开生成的工程。输出目录必须尚不存在，也可以指定其他位于 MUI 包目录之外的位置。生成工具会复制 Assets、Packages 与 ProjectSettings，并按输出位置更新本地包引用和锁文件。

在生成工程中打开 `Assets/Basic/Basic.unity`，然后进入 Play Mode。模板仍保留 `MUI > Basic Example > Open Scene` 菜单；直接把包内模板当工程打开时，同一物理场景可能被 Unity 映射到 `Packages/com.mzbswh.mui/ExampleProject~`，因此不作为推荐验收方式。

场景包含 `UIHost`、`EventSystem` 和 `BasicDemo`。运行后，示例通过纯同步类型化 Route 打开真实的 `BasicView` Prefab；标题来自生成的 ViewModel 绑定。点击 **Done**，页面返回整数 `42`；重新打开按钮显示 `42: Reopen`，Console 显示结果与清理状态。点击该按钮可再次打开页面。

项目运行时代码在 `Assets/Basic/Scripts`，场景菜单在 `Assets/Basic/Editor`。示例直接引用 Prefab，不包含资源下载、持久化或平台服务接入。
