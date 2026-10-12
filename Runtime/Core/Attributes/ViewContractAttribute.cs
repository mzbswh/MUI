using System;

namespace MUI
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ViewContractAttribute : Attribute
    {
        public ViewContractAttribute(string resourceKey)
        {
            ResourceKey = resourceKey;
        }

        /// <summary>供编辑器定位页面 Prefab，不参与运行时资源加载。</summary>
        public string PrefabAssetGuid { get; set; }

        public string ResourceKey
        {
            get;
        }
    }
}
