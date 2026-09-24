# 资源预算职责

资源预算由项目资源系统管理。MUI 已删除 ResourceBudget、BudgetedResourceLoader 和 SharedResourceLoader，不提供替代实现。界面仅持有后端交付的凭证，并在不再显示资源后归还。页面实例缓存的容量控制属于 UI 内部复用策略，不代表项目资源预算或物理内存测量。
