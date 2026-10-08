using System;

namespace MUI.UGUI
{
    /// <summary>稳定身份与借用的表现模型；列表不负责销毁模型。</summary>
    public sealed class VirtualListItem
    {
        public VirtualListItem(object key, ViewModel viewModel, float? extent = null, string templateKey = null,
            long? contentVersion = null)
        {
            if (extent.HasValue && (extent.Value <= 0 || float.IsNaN(extent.Value) || float.IsInfinity(extent.Value)))
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            if (templateKey != null && string.IsNullOrWhiteSpace(templateKey))
            {
                throw new ArgumentException("Template key must be null or non-empty.", nameof(templateKey));
            }

            if (contentVersion.HasValue && contentVersion.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(contentVersion));
            }

            ContentVersion = contentVersion;
            TemplateKey = templateKey;
            Extent = extent;
            Key = key ?? throw new ArgumentNullException(nameof(key));
            ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }

        /// <summary>单列列表的可选滚动轴尺寸；纵向为高度，横向为宽度。null 使用估算或测量尺寸。多列 Grid 使用列表配置的统一尺寸；变化通过同键替换条目发布。</summary>
        public float? Extent
        {
            get;
        }

        /// <summary>命名模板键；null 使用列表默认模板，字符串比较区分大小写。</summary>
        public string TemplateKey
        {
            get;
        }

        /// <summary>同键、同模板内容的显式版本。null 仅在同一条目实例内保留测量；文字或图片变化应升级版本或显式通知尺寸失效。</summary>
        public long? ContentVersion
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
