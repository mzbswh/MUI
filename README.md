# MUI

面向 Unity uGUI 的页面、生成绑定、导航和虚拟列表框架。目标环境为 **Unity 2022.3.62f3**；TMP 与 Input System 通过独立适配程序集接入。

当前分支正在按统一异步生命周期重构。部分旧同步 API 与示例仍待迁移，尚未完成完整 Unity/IL2CPP 运行验收；请以[实现记录](docs/IMPLEMENTATION-STATUS.md)区分已编译与已运行验证的能力。

## 安装与首次运行

1. 在目标 Unity 工程中打开 **Window → Package Manager**。
2. 点击 **+ → Add package from disk…**，选择本仓库根目录的 `package.json`。
3. 在 MUI 的 **Samples** 中导入 **Basic Example**。
4. 打开导入目录中的 `Basic.unity`，进入 Play Mode。预期点击 **Done** 后得到 `42`，再通过 **42: Reopen** 打开新页面。

框架通过本地 UPM 引用安装，不复制 Runtime 到 Assets。示例按需导入；Navigation 示例须先导入 Resource Integration。Basic 的上述交互为验收步骤，当前重构版本仍待实际验证。

## 开发入口

普通页面由 ViewModel、带 Element 的 View/Prefab、Route 和宿主配置组成，Presenter 按需使用。属性、命令与绑定接线由包内生成器生成。

- [入门与生命周期](Documentation~/index.md)
- [虚拟列表接入](Documentation~/virtual-lists.md)
- [Basic Example 源码与手写文件说明](Samples~/BasicExample/README.md)
- [完整设计目标](docs/DESIGN-GOALS.md)
- [实现与验证状态](docs/IMPLEMENTATION-STATUS.md)
- [变更记录](CHANGELOG.md)

资源加载后端、业务数据仓库、分页与选择规则由项目提供。Provider 的目标契约为统一异步获取，本地资源可立即完成；不要在 Unity 主线程阻塞等待任务。

采用 [MIT License](LICENSE.md)。
