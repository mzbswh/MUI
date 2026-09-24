using System;

namespace MUI
{
    /// <summary>生成器写入的绑定元数据版本，供其他程序集编译时读取；业务无需手动声明。</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class GeneratedBindingMetadataAttribute : Attribute
    {
        public GeneratedBindingMetadataAttribute(int version)
        {
            Version = version;
        }

        public int Version
        {
            get;
        }
    }
}
