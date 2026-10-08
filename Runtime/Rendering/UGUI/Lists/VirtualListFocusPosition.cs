using System;
using System.Collections.Generic;

namespace MUI.UGUI
{
    /// <summary>同一列表来源代际内的逻辑焦点；不保存 View、控件或模型引用。</summary>
    public sealed class VirtualListFocusPosition
    {
        internal VirtualListFocusPosition(object ownerIdentity, long sourceGeneration, object key, string elementName,
            string[] controlPath = null)
        {
            OwnerIdentity = ownerIdentity;
            SourceGeneration = sourceGeneration;
            Key = key;
            ElementName = elementName;
            ControlPath = controlPath == null ? null : Array.AsReadOnly(controlPath);
        }

        internal object OwnerIdentity
        {
            get;
        }

        internal long SourceGeneration
        {
            get;
        }

        public object Key
        {
            get;
        }

        /// <summary>条目 View 内的绑定元素名；没有 Element 适配器或控件位于子 View 时为空。</summary>
        public string ElementName
        {
            get;
        }

        /// <summary>从条目 View 根到原生控件的唯一节点名称路径；同级重名时为空，恢复按默认焦点回退。</summary>
        public IReadOnlyList<string> ControlPath
        {
            get;
        }
    }
}
