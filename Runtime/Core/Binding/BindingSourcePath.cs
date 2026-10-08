using System;
using System.ComponentModel;

namespace MUI
{
    /// <summary>从根模型到叶属性的通知链。生成器提供访问器，实例仅保存不可变的路径定义。</summary>
    public sealed class BindingSourcePath<TViewModel> where TViewModel : ViewModel
    {
        private readonly BindingSourceMember<TViewModel>[] members;

        public BindingSourcePath(params BindingSourceMember<TViewModel>[] members)
        {
            if (members == null || members.Length == 0)
            {
                throw new ArgumentException("A source path requires at least one member.", nameof(members));
            }

            this.members = (BindingSourceMember<TViewModel>[])members.Clone();
            foreach (var member in this.members)
            {
                if (member.Owner == null || string.IsNullOrWhiteSpace(member.PropertyName))
                {
                    throw new ArgumentException("A source path cannot contain default members.", nameof(members));
                }
            }
        }

        internal int Count => members.Length;

        internal BindingSourceMember<TViewModel> this[int index] => members[index];

        internal INotifyPropertyChanged GetOwner(TViewModel model) => members[members.Length - 1].Owner(model);

        internal INotifyPropertyChanged[] CaptureOwners(TViewModel model)
        {
            var owners = new INotifyPropertyChanged[members.Length];
            for (var i = 0; i < owners.Length; ++i)
            {
                owners[i] = members[i].Owner(model);
            }

            return owners;
        }

        internal bool HasOwners(TViewModel model, INotifyPropertyChanged[] owners)
        {
            for (var i = 0; i < owners.Length; ++i)
            {
                if (!ReferenceEquals(owners[i], members[i].Owner(model)))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
