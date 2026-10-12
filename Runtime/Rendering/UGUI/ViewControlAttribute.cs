using System;
namespace MUI.UGUI
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class GenerateViewControlsAttribute : Attribute { }

    /// <summary>声明页面需要的原生控件引用，供编辑器反向识别；与属性数据绑定独立。</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class ViewControlAttribute : Attribute
    {
        public ViewControlAttribute(string prefabGuid, string name, Type adapterType)
        { PrefabGuid = prefabGuid; Name = name; AdapterType = adapterType; }
        public string PrefabGuid { get; }
        public string Name { get; }
        public Type AdapterType { get; }
        public Type ComponentType { get; set; }
        public Type TargetType { get; set; }
        public bool Generated { get; set; }
    }
}
