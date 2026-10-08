using System;
using System.Text;
using MUI.UGUI;

namespace MUI.Editor
{
    /// <summary>编辑器命名建议；项目可替换纯命名函数，运行时绑定仍按实际名称与类型解析。</summary>
    public static class ElementNaming
    {
        private static Func<string, Type, string> rule;

        internal static string BindingName(string name)
        {
            const string clone = "(Clone)";
            return name.EndsWith(clone, StringComparison.Ordinal) ? name.Substring(0, name.Length - clone.Length) : name;
        }

        /// <summary>
        /// 配置项目命名规则，传 null 恢复默认规则。通常从 InitializeOnLoad 注册。
        /// 回调只能计算名字，不修改层级、组件或规则配置；结果仍须在命名窗口审核。
        /// </summary>
        public static void SetRule(Func<string, Type, string> suggest)
        {
            rule = suggest;
        }

        internal static string Suggest(string name, Type type)
        {
            var configured = rule;
            var result = configured == null ? SuggestDefault(name, type) : configured(name, type);
            if (string.IsNullOrWhiteSpace(result) || result != result.Trim() || result != BindingName(result))
            {
                throw new InvalidOperationException("命名规则必须返回非空、无首尾空白且不带 (Clone) 后缀的名字。");
            }
            if (!ReferenceEquals(configured, rule))
            {
                throw new InvalidOperationException("命名规则在计算过程中被替换，请重新生成建议。");
            }
            return result;
        }

        /// <summary>计算内置前缀建议，项目规则可调用它组合自己的约定；不会再次调用项目规则。</summary>
        public static string SuggestDefault(string name, Type type)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (type == null || !typeof(Element).IsAssignableFrom(type))
            {
                throw new ArgumentException("命名类型必须继承 Element。", nameof(type));
            }
            var prefix = Prefix(type);
            var value = BindingName(name);
            foreach (var known in new[]
            {
                "Btn_",
                "Txt_",
                "Img_",
                "Sld_",
                "Sbr_",
                "Tog_",
                "Inp_",
                "Scr_",
                "Lst_",
                "Msk_",
                "Lyt_",
                "Ddp_",
                "Elm_"
            })
            {
                if (value.StartsWith(known, StringComparison.OrdinalIgnoreCase))
                {
                    value = value.Substring(known.Length);
                    break;
                }
            }

            // 保留命名窗口为重名控件分配的数字后缀，避免下一次建议再次改名。
            var suffix = string.Empty;
            var separator = value.LastIndexOf('_');
            if (separator > 0 && separator < value.Length - 1)
            {
                var numericSuffix = true;
                for (var index = separator + 1; index < value.Length; ++index)
                {
                    if (value[index] < '0' || value[index] > '9')
                    {
                        numericSuffix = false;
                        break;
                    }
                }

                if (numericSuffix)
                {
                    suffix = value.Substring(separator);
                    value = value.Substring(0, separator);
                }
            }

            var result = new StringBuilder();
            var uppercase = true;
            foreach (var c in value)
            {
                if (!char.IsLetterOrDigit(c))
                {
                    uppercase = true;
                    continue;
                }

                result.Append(uppercase ? char.ToUpperInvariant(c) : c);
                uppercase = false;
            }

            return prefix + (result.Length == 0 ? "Control" : result.ToString()) + suffix;
        }

        private static string Prefix(Type type)
        {
            if (typeof(RecyclingListElement).IsAssignableFrom(type))
            {
                return "Lst_";
            }

            if (typeof(ButtonElement).IsAssignableFrom(type))
            {
                return "Btn_";
            }

            if (typeof(TextElement).IsAssignableFrom(type) || type.FullName == "MUI.TMP.TMPTextElement")
            {
                return "Txt_";
            }

            if (typeof(ImageElement).IsAssignableFrom(type) || typeof(RawImageElement).IsAssignableFrom(type))
            {
                return "Img_";
            }

            if (typeof(SliderElement).IsAssignableFrom(type))
            {
                return "Sld_";
            }

            if (typeof(ScrollbarElement).IsAssignableFrom(type))
            {
                return "Sbr_";
            }

            if (typeof(ToggleElement).IsAssignableFrom(type))
            {
                return "Tog_";
            }

            if (typeof(InputFieldElement).IsAssignableFrom(type) || type.FullName == "MUI.TMP.TMPInputFieldElement")
            {
                return "Inp_";
            }

            if (typeof(DropdownElement).IsAssignableFrom(type) || type.FullName == "MUI.TMP.TMPDropdownElement")
            {
                return "Ddp_";
            }

            if (typeof(ScrollRectElement).IsAssignableFrom(type))
            {
                return "Scr_";
            }

            if (typeof(MaskElement).IsAssignableFrom(type) || typeof(RectMaskElement).IsAssignableFrom(type))
            {
                return "Msk_";
            }

            if (typeof(LayoutSizeElement).IsAssignableFrom(type) || typeof(AspectRatioElement).IsAssignableFrom(type))
            {
                return "Lyt_";
            }

            return "Elm_";
        }
    }
}
