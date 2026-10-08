using System;
using System.ComponentModel;

namespace MUI
{
    /// <summary>路径中的一个通知属性及其强类型拥有者访问器，不在运行时解析成员名。</summary>
    public readonly struct BindingSourceMember<TViewModel> where TViewModel : ViewModel
    {
        public BindingSourceMember(string propertyName, Func<TViewModel, INotifyPropertyChanged> owner)
        {
            if (string.IsNullOrWhiteSpace(propertyName) || propertyName.IndexOf('.') >= 0)
            {
                throw new ArgumentException("A path member requires one property name.", nameof(propertyName));
            }

            PropertyName = propertyName;
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public string PropertyName
        {
            get;
        }

        /// <summary>中间模型缺失时返回 null；访问器只能读取所属 UI 线程的状态。</summary>
        public Func<TViewModel, INotifyPropertyChanged> Owner
        {
            get;
        }
    }
}
