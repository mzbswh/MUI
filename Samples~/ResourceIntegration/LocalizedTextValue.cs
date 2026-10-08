namespace MUI.Samples.ResourceIntegration
{
    /// <summary>格式化结果，区分当前语言和实际译文语言。</summary>
    public readonly struct LocalizedTextValue
    {
        internal LocalizedTextValue(string text, string locale, string textLocale, bool rightToLeft)
        {
            Text = text;
            Locale = locale;
            TextLocale = textLocale;
            IsRightToLeft = rightToLeft;
        }

        /// <summary>格式化后的文本。</summary>
        public string Text
        {
            get;
        }

        /// <summary>当前选中的语言，决定数字和日期的格式。</summary>
        public string Locale
        {
            get;
        }

        /// <summary>实际提供译文的目录语言，可能来自回退目录。</summary>
        public string TextLocale
        {
            get;
        }

        /// <summary>实际译文语言的文字方向；不会自动完成布局或字形处理。</summary>
        public bool IsRightToLeft
        {
            get;
        }
    }
}
