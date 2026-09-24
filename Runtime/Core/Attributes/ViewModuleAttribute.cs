using System;

namespace MUI
{
    /// <summary>配置当前程序集的显式绑定注册入口；不创建业务服务或扫描运行时程序集。</summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class ViewModuleAttribute : Attribute
    {
        public ViewModuleAttribute(string generatedNamespace, string registryName = "Bindings")
        {
            GeneratedNamespace = generatedNamespace;
            RegistryName = registryName;
        }

        public string GeneratedNamespace
        {
            get;
        }

        public string RegistryName
        {
            get;
        }
    }
}
