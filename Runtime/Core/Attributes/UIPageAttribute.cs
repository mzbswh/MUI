using System;

namespace MUI
{
    /// <summary>声明由导航管理的页面，同时生成视图绑定与强类型打开入口。</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class UIPageAttribute : Attribute
    {
        /// <summary>自动生成打开参数，无业务返回值；默认按模型名匹配同作用域的 Presenter。</summary>
        public UIPageAttribute() : this(typeof(Unit)) { }

        /// <summary>从 Presenter 的 OpenEntry 方法生成参数契约；默认按模型名匹配同作用域的 Presenter。</summary>
        public UIPageAttribute(Type resultType)
        {
            ResultType = resultType;
        }

        public UIPageAttribute(Type argsType, Type resultType)
        {
            ArgsType = argsType;
            ResultType = resultType;
        }

        /// <summary>省略时按模型名称推导资源键。</summary>
        public string ResourceKey { get; set; }

        /// <summary>返回显式资源键或按当前模型类型推导的键，供编辑器及项目工具使用。</summary>
        public string GetResourceKey(Type viewModelType)
        {
            if (viewModelType == null) throw new ArgumentNullException(nameof(viewModelType));
            if (ResourceKey != null) return ResourceKey;
            var name = viewModelType.Name;
            var arity = name.IndexOf('`');
            if (arity >= 0) name = name.Substring(0, arity);
            if (name.EndsWith("ViewModel", StringComparison.Ordinal)) name = name.Substring(0, name.Length - "ViewModel".Length);
            return name.EndsWith("View", StringComparison.Ordinal) ? name : name + "View";
        }

        public Type ArgsType
        {
            get;
        }

        public Type ResultType
        {
            get;
        }

        /// <summary>非约定命名时显式指定；InventoryViewModel 默认匹配 InventoryPresenter。</summary>
        public Type PresenterType
        {
            get; set;
        }

        /// <summary>可选的业务页面入口类名，默认去掉 ViewModel 后缀并补 Page。</summary>
        public string PageName { get; set; }

        public string Key
        {
            get; set;
        }

        /// <summary>指定页面策略预设；省略时 CreateDefinition 使用项目全局默认预设。</summary>
        public string PolicyPreset
        {
            get; set;
        }
    }
}
