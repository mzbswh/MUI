# 旧示例与诊断夹具

`Samples/` 保留重构前的独立示例、程序化 UI 和特殊生命周期场景，用于维护旧编译入口及复现问题。它们不是推荐的项目接入方式，也不会由 Package Manager 导入。

新的教学入口是仓库 `Samples~/Scenes/MUIExamples.unity`，说明见 `Samples~/README.md`。不要将这两个版本同时复制到同一 Unity 项目。

仓库 `Tools~/Build` 中原有示例程序集继续引用这里，避免丢失诊断源码；`MUI.Examples.csproj` 和 `MUI.Samples.Editor.csproj` 编译新的教学示例。
