# MUI

面向 Unity uGUI 的页面、生成绑定、导航和虚拟列表框架。目标环境为 **Unity 2022.3.62f3**；TMP 与 Input System 通过独立适配程序集接入。

核心采用统一异步生命周期；导航、生成绑定、列表、资源与可选输入适配的部分路径已有 Unity Play Mode 和 IL2CPP 运行记录。完整异常矩阵、示例交互和性能验收仍在进行，请以[实现记录](docs/IMPLEMENTATION-STATUS.md)核对具体覆盖范围。

## 安装与首次运行

1. 在目标 Unity 工程中打开 **Window → Package Manager**。
2. 点击 **+ → Add package from disk…**，选择本仓库根目录的 `package.json`。
3. 在 MUI 的 **Samples** 中导入 **Basic Example**。
4. 打开导入目录中的 `Basic.unity`，进入 Play Mode。预期点击 **Done** 后得到 `42`，再通过 **42: Reopen** 打开新页面。

框架通过本地 UPM 引用安装，不复制 Runtime 到 Assets。示例按需导入；Navigation 示例须先导入 Resource Integration。已有 Basic Player 记录覆盖键盘完成、返回和重新打开；上述鼠标交互及最新版本完整示例验收仍待完成。

## 开发入口

普通页面由 ViewModel、带 Element 的 View/Prefab、Route 和宿主配置组成，Presenter 按需使用。属性、命令与绑定接线由包内生成器生成。

- [入门与生命周期](Documentation~/index.md)
- [虚拟列表接入](Documentation~/virtual-lists.md)
- [Basic Example 源码与手写文件说明](Samples~/BasicExample/README.md)
- [完整设计目标](docs/DESIGN-GOALS.md)
- [实现与验证状态](docs/IMPLEMENTATION-STATUS.md)
- [变更记录](CHANGELOG.md)

资源加载后端、业务数据仓库、分页与选择规则由项目提供。Provider 使用统一异步获取契约，本地资源可立即完成；不要在 Unity 主线程阻塞等待任务。

采用 [MIT License](LICENSE.md)。
