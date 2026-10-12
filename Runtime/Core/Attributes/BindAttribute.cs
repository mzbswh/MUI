using System;

namespace MUI
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class BindAttribute : Attribute
    {
        public BindAttribute(string target, string propertyName, Type converterType = null, BindingMode bindingMode = BindingMode.OneWay)
        {
            Target = target;
            PropertyName = propertyName;
            ConverterType = converterType;
            Mode = bindingMode;
        }

        public string Target
        {
            get;
        }

        public string PropertyName
        {
            get;
        }

        /// <summary>
        /// 实现对应源类型与目标类型转换的具体类型，须具有公共无参构造函数。
        /// 转换器由独立的绑定上下文创建，因此不能使用模型的私有或受保护嵌套类型。
        /// </summary>
        public Type ConverterType
        {
            get;
        }

        public BindingMode Mode
        {
            get;
        }

        /// <summary>生成器可从 nameof(Element.Property) 推断类型时，此项可省略。</summary>
        /// <summary>原生组件类型；生成强类型访问，不使用运行时反射。回写需提供变化通知。</summary>
        public Type ComponentType { get; set; }

        /// <summary>编译器生成的属性包装或外部属性扩展类型。</summary>
        public Type PropertyAdapterType { get; set; }

        public bool NativeNotifications { get; set; }

        /// <summary>生成的实时只读访问入口使用的内部委托字段。</summary>
        public string ReadAccessorField { get; set; }

        public Type ElementType
        {
            get; set;
        }

        /// <summary>接收反向转换校验结果的可写 BindingValidationState 属性名，仅用于反向绑定。</summary>
        public string ValidationProperty
        {
            get; set;
        }

        /// <summary>相对于被标注模型成员的属性路径，如 "Profile.Name"；中间拥有者须为 ViewModel。</summary>
        public string SourcePath
        {
            get; set;
        }

        /// <summary>嵌套路径中间模型为 null 时投影的目标值；省略时使用目标类型默认值。</summary>
        public object NullValue
        {
            get; set;
        }
    }
}
