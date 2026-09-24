using System;

namespace MUI.UGUI
{
    /// <summary>稳定身份与借用的表现模型；列表不负责销毁模型。</summary>
    public sealed class VirtualListItem
    {
        public VirtualListItem(object key, ViewModel viewModel, float? height = null, string templateKey = null)
        {
            if (height.HasValue && (height.Value <= 0 || float.IsNaN(height.Value) || float.IsInfinity(height.Value)))
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            if (templateKey != null && string.IsNullOrWhiteSpace(templateKey))
            {
                throw new ArgumentException("Template key must be null or non-empty.", nameof(templateKey));
            }

            TemplateKey = templateKey;
            Height = height;
            Key = key ?? throw new ArgumentNullException(nameof(key));
            ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }

        /// <summary>可选条目高度；null 使用列表默认高度。高度变化通过同键替换条目发布。</summary>
        public float? Height
        {
            get;
        }

        /// <summary>命名模板键；null 使用列表默认模板，字符串比较区分大小写。</summary>
        public string TemplateKey
        {
            get;
        }

        public object Key
        {
            get;
        }

        public ViewModel ViewModel
        {
            get;
        }
    }
}
