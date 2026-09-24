using System;

namespace MUI.Localization
{
    /// <summary>显式复数规则返回的语法类别，不根据语言前缀推断。</summary>
    public enum PluralCategory
    {
        Zero,
        One,
        Two,
        Few,
        Many,
        Other
    }

    /// <summary>复制参数数组，但不深拷贝参数对象；调用方应传入不可变参数快照。</summary>
    public sealed class LocalizedMessage
    {
        private readonly object[] arguments;

        /// <summary>创建普通文本请求；参数用于 string.Format 的位置占位符。</summary>
        public LocalizedMessage(string key, params object[] arguments) : this(key, null, arguments)
        {
        }

        private LocalizedMessage(string key, decimal? count, object[] arguments)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A semantic text key is required.", nameof(key));
            }

            Key = key;
            Count = count;
            this.arguments = arguments == null ? Array.Empty<object>() : (object[])arguments.Clone();
        }

        /// <summary>稳定的语义文本键。</summary>
        public string Key
        {
            get;
        }

        /// <summary>用于选择复数类别的数量；普通文本为 null。</summary>
        public decimal? Count
        {
            get;
        }

        /// <summary>创建复数请求；数量占据参数 {0}，额外参数从 {1} 开始。</summary>
        public static LocalizedMessage Plural(string key, decimal count, params object[] additionalArguments)
        {
            var args = new object[1 + (additionalArguments == null ? 0 : additionalArguments.Length)];
            args[0] = count;
            if (additionalArguments != null)
            {
                Array.Copy(additionalArguments, 0, args, 1, additionalArguments.Length);
            }

            return new LocalizedMessage(key, count, args);
        }

        internal string Format(string template, IFormatProvider culture) => string.Format(culture, template, arguments);
    }
}
