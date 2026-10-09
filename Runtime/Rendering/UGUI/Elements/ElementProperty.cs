using System;
using System.Collections.Generic;

namespace MUI.UGUI
{
    /// <summary>
    /// 自定义控件的属性状态。Value 写入检查控件与所属线程，调用原生赋值后发布指定属性通知；
    /// 控件最终释放时清除值、比较器与回调，与控件共用最终作用域。
    /// </summary>
    public sealed class ElementProperty<T> : IDisposable
    {
        private readonly string propertyName;
        private IEqualityComparer<T> comparer;
        private Element owner;
        private Action<T, T> onValueChanged;
        private T value;
        private bool disposed;

        internal ElementProperty(Element owner, string propertyName, T initialValue,
            Action<T, T> onValueChanged, IEqualityComparer<T> comparer)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentException("An element property requires its public notification name.", nameof(propertyName));
            }

            this.owner = owner;
            this.propertyName = propertyName;
            this.onValueChanged = onValueChanged;
            this.comparer = comparer ?? EqualityComparer<T>.Default;
            value = initialValue;
        }

        public T Value
        {
            get
            {
                RequireAlive();
                return value;
            }

            set
            {
                RequireAlive();
                var equal = comparer.Equals(this.value, value);
                // 项目比较器可能重入并结束控件，必须在修改状态前重新验证。
                RequireAlive();
                if (equal)
                {
                    return;
                }

                var previous = this.value;
                this.value = value;
                onValueChanged?.Invoke(previous, value);
                if (!disposed && owner != null && owner.IsAlive)
                {
                    owner.PublishPropertyChange(propertyName);
                }
            }
        }

        public void Dispose()
        {
            UnityMainThread.Require();
            if (disposed)
            {
                return;
            }

            disposed = true;
            owner = null;
            onValueChanged = null;
            comparer = null;
            value = default;
        }

        private void RequireAlive()
        {
            UnityMainThread.Require();
            if (disposed || owner == null || !owner.IsAlive)
            {
                throw new ObjectDisposedException(nameof(ElementProperty<T>));
            }
        }
    }
}
