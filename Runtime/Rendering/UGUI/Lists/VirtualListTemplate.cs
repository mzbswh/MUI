using System;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>虚拟列表中的命名模板；在父 View 初始化前配置，初始化后不支持热替换。</summary>
    [Serializable]
    public sealed class VirtualListTemplate
    {
        [SerializeField] private string key;
        [SerializeField] private View view;

        public VirtualListTemplate(string key, View view)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Template key must not be empty.", nameof(key));
            }

            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            this.key = key;
            this.view = view;
        }

        public string Key => key;

        public View View => view;
    }
}
