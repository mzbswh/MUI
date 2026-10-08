using System;

namespace MUI.Navigation
{
    /// <summary>为具有 ViewContract 的模型生成强类型路由工厂；业务服务仍通过项目工厂传入。</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ViewRouteAttribute : Attribute
    {
        public ViewRouteAttribute(Type argsType, Type resultType)
        {
            ArgsType = argsType;
            ResultType = resultType;
        }

        public Type ArgsType
        {
            get;
        }

        public Type ResultType
        {
            get;
        }

        public Type PresenterType
        {
            get; set;
        }

        public string Key
        {
            get; set;
        }

    }
}
