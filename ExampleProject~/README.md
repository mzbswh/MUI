# MUI 基础示例工程

用 Unity 2022.3.62f3 打开本目录，选择 `MUI > Basic Example > Open Scene`，然后进入 Play Mode。该菜单明确打开工程内的 `Assets/Basic/Basic.unity`；由于工程位于本地 MUI 包目录中，从系统文件对话框选择同一个物理文件可能被 Unity 解释成 `Packages/com.mzbswh.mui/ExampleProject~` 下的空场景。工程通过相对路径引用上一级目录的 MUI 包，移动工程时请保持两者的目录关系。

场景包含 `UIHost`、`EventSystem` 和 `BasicDemo`。运行后，示例通过纯同步类型化 Route 打开真实的 `BasicView` Prefab；标题来自生成的 ViewModel 绑定。点击 **Done**，页面返回整数 `42`，Console 显示结果与清理状态。Play Mode 中可通过 `BasicDemo` 组件的 **Open page** 上下文菜单再次打开。

项目运行时代码在 `Assets/Basic/Scripts`，场景菜单在 `Assets/Basic/Editor`。示例直接引用 Prefab，不包含资源下载、持久化或平台服务接入。
