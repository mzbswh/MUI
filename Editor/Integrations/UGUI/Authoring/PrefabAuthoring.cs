using UnityEngine;
using UnityEngine.UI;

namespace MUI.Editor
{
    /// <summary>供页面向导和可选模块向导共用的原生 Prefab 构建操作。</summary>
    public static class PrefabAuthoring
    {
        /// <summary>创建居中、非射线目标的默认文本节点；调用方负责布局和节点生命周期。</summary>
        public static Text CreateText(string name, Transform parent, string content)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Text));
            node.transform.SetParent(parent, false);
            var text = node.GetComponent<Text>();
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.text = content;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }
    }
}
