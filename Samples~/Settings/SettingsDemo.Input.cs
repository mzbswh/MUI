using MUI.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.Samples.Settings
{
    public sealed partial class SettingsDemo
    {
        /// <summary>构造原生输入框，输入配置与当前文本由生成绑定设置。</summary>
        private static void AddNameInput(Transform parent)
        {
            var root = Node("PlayerName", parent, new Vector2(400, 44), new Vector2(0, -20));
            var background = root.gameObject.AddComponent<Image>();
            background.color = new Color(0.22f, 0.26f, 0.32f);
            var field = root.gameObject.AddComponent<InputField>();
            field.targetGraphic = background;

            var text = Node("Text", root, new Vector2(376, 40), Vector2.zero).gameObject.AddComponent<Text>();
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 22;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.supportRichText = false;
            text.raycastTarget = false;
            field.textComponent = text;

            var placeholder = Node("Placeholder", root, new Vector2(376, 40), Vector2.zero).gameObject.AddComponent<Text>();
            placeholder.font = text.font;
            placeholder.fontSize = 22;
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.color = new Color(0.7f, 0.72f, 0.76f);
            placeholder.text = "Player name";
            placeholder.raycastTarget = false;
            field.placeholder = placeholder;
            root.gameObject.AddComponent<InputFieldElement>();

            var error = Node("NameError", parent, new Vector2(400, 24), new Vector2(0, -53));
            var errorText = error.gameObject.AddComponent<Text>();
            errorText.font = text.font;
            errorText.fontSize = 16;
            errorText.alignment = TextAnchor.MiddleLeft;
            errorText.color = new Color(1f, 0.65f, 0.45f);
            errorText.raycastTarget = false;
            error.gameObject.AddComponent<TextElement>();
        }
    }
}
