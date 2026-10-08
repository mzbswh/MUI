using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    [DisallowMultipleComponent, RequireComponent(typeof(Text))]
    public sealed partial class TextElement : GraphicElement, ITextElement
    {
        private Text target;

        private Text Target
        {
            get
            {
                RequireAlive();
                if (target == null)
                {
                    target = RequireComponent<Text>();
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

        protected override void OnInitialize()
        {
            target = RequireComponent<Text>();
            base.OnInitialize();
            OnDispose(() =>
            {
                // 托管字体由资源槽归还，先解除原生文字组件的借用。
                if (fontResources != null && target != null)
                {
                    target.font = null;
                }
            });
        }
    }
}
