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

        public string ResourceKey
        {
            get;
        }
    }
}
