using System;

namespace MUI.Themes
{
    /// <summary>不依赖 Unity 的 RGBA 值，各通道必须为 0 到 1 的有限数。</summary>
    public readonly struct ThemeColor
    {
        public ThemeColor(float red, float green, float blue, float alpha = 1)
        {
            Validate(red);
            Validate(green);
            Validate(blue);
            Validate(alpha);
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }

        public float Red
        {
            get;
        }

        public float Green
        {
            get;
        }

        public float Blue
        {
            get;
        }

        public float Alpha
        {
            get;
        }

        private static void Validate(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Theme color channels must be finite and between zero and one.");
            }
        }
    }

    /// <summary>控件五种交互状态的配色；不包含动画时长或原生控件资源。</summary>
    public readonly struct ThemeStateColors
    {
        public ThemeStateColors(ThemeColor normal, ThemeColor highlighted, ThemeColor pressed, ThemeColor selected, ThemeColor disabled)
        {
            Normal = normal;
            Highlighted = highlighted;
            Pressed = pressed;
            Selected = selected;
            Disabled = disabled;
        }

        public ThemeColor Normal
        {
            get;
        }

        public ThemeColor Highlighted
        {
            get;
        }

        public ThemeColor Pressed
        {
            get;
        }

        public ThemeColor Selected
        {
            get;
        }

        public ThemeColor Disabled
        {
            get;
        }
    }

    /// <summary>带类型的语义标识。目录保存不可变值，不持有 Unity 资源句柄。</summary>
    public readonly struct ThemeToken<T>
    {
        /// <summary>创建语义键；仅支持 ThemeColor、ThemeStateColors、float、bool、string。</summary>
        public ThemeToken(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A semantic theme token is required.", nameof(name));
            }

            if (typeof(T) != typeof(ThemeColor) && typeof(T) != typeof(ThemeStateColors) &&
                typeof(T) != typeof(float) && typeof(T) != typeof(bool) && typeof(T) != typeof(string))
            {
                throw new NotSupportedException("Theme values currently support ThemeColor, ThemeStateColors, float, bool and string only.");
            }

            Name = name;
        }

        public string Name
        {
            get;
        }
    }

    /// <summary>经校验的目录项，保留名称、精确类型和值。</summary>
    public sealed class ThemeValue
    {
        private ThemeValue(string name, Type type, object value)
        {
            Name = name;
            Type = type;
            Value = value;
        }

        public string Name
        {
            get;
        }

        internal Type Type
        {
            get;
        }

        internal object Value
        {
            get;
        }

        /// <summary>创建目录项；拒绝默认空键、空值、不支持的类型以及非有限浮点数。</summary>
        public static ThemeValue Create<T>(ThemeToken<T> token, T value)
        {
            // default(ThemeToken<T>) 会绕过构造器，因此写入目录前必须重新校验。
            var validated = new ThemeToken<T>(token.Name);
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (value is float number && (float.IsNaN(number) || float.IsInfinity(number)))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Theme scalar must be finite.");
            }

            return new ThemeValue(validated.Name, typeof(T), value);
        }
    }
}
