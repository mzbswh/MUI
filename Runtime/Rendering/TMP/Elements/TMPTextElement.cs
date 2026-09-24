using MUI.UGUI;
using TMPro;
using UnityEngine;

namespace MUI.TMP
{
    [DisallowMultipleComponent, RequireComponent(typeof(TextMeshProUGUI))]
    public sealed partial class TMPTextElement : GraphicElement
    {
        private TextMeshProUGUI target;

        private TextMeshProUGUI Target
        {
            get
            {
                RequireAlive();
                if (target == null)
                {
                    target = RequireComponent<TextMeshProUGUI>();
                }

                return target;
            }
        }

        public string Content
        {
            get => Target.text;
            set
            {
                if (Target.text == value)
                {
                    return;
                }

                Target.text = value;
                NotifyChanged();
            }
        }

        protected override UnityEngine.UI.MaskableGraphic GraphicComponent => Target;

        protected override AccessibilityRole DefaultAccessibilityRole => AccessibilityRole.Text;

        protected override string DefaultAccessibilityLabel => Content;

        /// <summary>读取共享字体材质，不能读取会创建材质实例的 fontMaterial。</summary>
        protected override Material ReadMaterial(UnityEngine.UI.MaskableGraphic graphic) =>
            ((TextMeshProUGUI)graphic).fontSharedMaterial;

        protected override void WriteMaterial(UnityEngine.UI.MaskableGraphic graphic, Material value)
        {
            var text = (TextMeshProUGUI)graphic;
            if (value == null)
            {
                // 清空自定义材质时恢复当前字体的默认材质，不保留即将归还的资源槽材质。
                var font = text.font;
                value = font == null ? null : font.material;
            }

            text.fontSharedMaterial = value;
        }

        protected override void OnInitialize()
        {
            target = RequireComponent<TextMeshProUGUI>();
            base.OnInitialize();
        }
    }
}
