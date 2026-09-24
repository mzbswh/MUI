using System;

namespace MUI
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class BindCommandAttribute : Attribute
    {
        public BindCommandAttribute(string target, string eventName)
        {
            Target = target;
            EventName = eventName;
        }

        public string Target
        {
            get;
        }

        public string EventName
        {
            get;
        }

        public Type ElementType
        {
            get; set;
        }

        public string InteractableProperty { get; set; } = "Interactable";
    }
}
