using System;

namespace MUI
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ObservablePropertyAttribute : Attribute
    {
        public ObservablePropertyAttribute(string name = null)
        {
            Name = name;
        }

        public string Name
        {
            get;
        }
    }
}
