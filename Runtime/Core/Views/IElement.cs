using System.ComponentModel;

namespace MUI
{
    /// <summary>可绑定元素的最小契约；原生控件由渲染适配器持有。</summary>
    public interface IElement : INotifyPropertyChanged
    {
        /// <summary>所属绑定边界内用于查找的名称。</summary>
        string Name
        {
            get;
        }

        /// <summary>是否仍可访问此元素，不能仅凭托管引用非空判断原生对象存活。</summary>
        bool IsAlive
        {
            get;
        }
    }
}
