using System;

namespace MUI
{
    /// <summary>声明由父界面管理的组合视图，仅生成绑定，不生成导航入口。</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class UIWidgetAttribute : Attribute
    {
        /// <summary>资源键由模型名称推导：InventoryViewModel → InventoryView。</summary>
        public UIWidgetAttribute()
        {
        }

        public UIWidgetAttribute(string resourceKey)
        {
            ResourceKey = resourceKey;
        }

        /// <summary>显式业务 Presenter；未指定时生成器使用唯一匹配的 Presenter。</summary>
        public Type PresenterType
        {
            get; set;
        }

        public string ResourceKey
        {
            get;
        }

        /// <summary>返回显式资源键或按当前模型类型推导的键，供编辑器及项目工具使用。</summary>
        public string GetResourceKey(Type viewModelType)
        {
            if (viewModelType == null)
                throw new ArgumentNullException(nameof(viewModelType));
            if (ResourceKey != null)
                return ResourceKey;
            var name = viewModelType.Name;
            var arity = name.IndexOf('`');
            if (arity >= 0)
                name = name.Substring(0, arity);
            if (name.EndsWith("ViewModel", StringComparison.Ordinal))
                name = name.Substring(0, name.Length - "ViewModel".Length);
            return name.EndsWith("View", StringComparison.Ordinal) ? name : name + "View";
        }
    }
}
