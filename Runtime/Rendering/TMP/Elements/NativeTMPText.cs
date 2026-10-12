using MUI.UGUI;
using TMPro;
using UnityEngine;

namespace MUI.TMP
{
    public sealed class NativeTMPText : NativeControl, ITextStyleElement
    {
        private readonly TMP_Text text;
        public float FontSize { get => text.fontSize; set { if (text.fontSize == value) return; text.fontSize = value; Changed(); } }
        public NativeTMPText(string name, TMP_Text target) : base(name, target) { text = target; }
        public string Content { get => text.text; set { if (text.text == value) return; text.text = value; Changed(); } }
        public Color Color { get => text.color; set { if (text.color == value) return; text.color = value; Changed(); } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void RegisterAdapter()
        {
            Register<TMP_Text>((name, target) => new NativeTMPText(name, target));
            Register<TMP_InputField>((name, target) => new NativeTMPInputField(name, target));
            Register<TMP_Dropdown>((name, target) => new NativeTMPDropdown(name, target));
        }
    }
}
