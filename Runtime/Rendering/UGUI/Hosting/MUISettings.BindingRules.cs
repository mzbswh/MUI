using System;
using System.Collections.Generic;
using UnityEngine;

namespace MUI.UGUI
{
    public enum ControlNameMatch { Prefix, Suffix, Contains, Regex }

    public enum NativeControlKind { Image, Text, Button }

    [Serializable]
    public sealed class ControlNamingRule
    {
        public string Prefix; // 保留序列化字段名以兼容已有规则；现在保存匹配表达式。
        public ControlNameMatch Match;
        public bool OverrideDefaultProperties;
        public List<string> DefaultBindingProperties = new List<string>();
        public string BindingProperty; // 空值使用适配器默认属性；也可以指定事件名。
        public BindingMode BindingDirection = BindingMode.OneWay;
        public bool OverrideBindingDirection;
        [HideInInspector] public NativeControlKind Kind;
        public List<string> ComponentTypes;
        public IReadOnlyList<string> EffectiveComponentTypes => ComponentTypes ?? DefaultTypes(Kind);
        public static List<string> DefaultTypes(NativeControlKind kind)
        {
            if (kind == NativeControlKind.Button) return new List<string> { "UnityEngine.UI.Button, UnityEngine.UI" };
            if (kind == NativeControlKind.Text) return new List<string> { "UnityEngine.UI.Text, UnityEngine.UI", "TMPro.TMP_Text, Unity.TextMeshPro" };
            return new List<string> { "UnityEngine.UI.Image, UnityEngine.UI" };
        }
        public ControlNamingRule(string prefix, NativeControlKind kind) { Prefix = prefix; Kind = kind; ComponentTypes = DefaultTypes(kind); }
    }

    [Serializable]
    public sealed class NativePropertyNotification
    {
        [Tooltip("组件类型全名及程序集，例如 UnityEngine.UI.Slider, UnityEngine.UI")]
        public string ComponentType;
        [Tooltip("原生属性名，例如 value")]
        public string Property;
        [Tooltip("公开 UnityEvent 字段或属性，支持零或一个参数，例如 onValueChanged")]
        public string Event;
        [Tooltip("可选：静默赋值方法，例如 SetValueWithoutNotify；留空使用属性 setter")]
        public string Setter;
    }

    [Serializable]
    public sealed class ComponentMemberNaming
    {
        public string ComponentType;
        public string ValueFormat;
        public string CommandFormat;
    }

    public sealed partial class MUISettings
    {
        public const string DefaultValueMemberNameFormat = "{Name}{Component}{DefaultSuffix}";
        public const string DefaultCommandMemberNameFormat = "{Name}{Component}{DefaultSuffix}Command";
        [SerializeField] private string valueMemberNameFormat = DefaultValueMemberNameFormat;
        [SerializeField] private string commandMemberNameFormat = DefaultCommandMemberNameFormat;
        public string ValueMemberNameFormat => string.IsNullOrWhiteSpace(valueMemberNameFormat) ? DefaultValueMemberNameFormat : valueMemberNameFormat;
        public string CommandMemberNameFormat => string.IsNullOrWhiteSpace(commandMemberNameFormat) ? DefaultCommandMemberNameFormat : commandMemberNameFormat;
        [SerializeField] private List<ComponentMemberNaming> componentMemberNaming = new List<ComponentMemberNaming>();

        public string ResolveMemberNameFormat(Type componentType, bool command)
        {
            for (var type = componentType; type != null; type = type.BaseType)
            {
                if (componentMemberNaming == null) break;
                foreach (var entry in componentMemberNaming)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.ComponentType) || Type.GetType(entry.ComponentType, false) != type) continue;
                    var format = command ? entry.CommandFormat : entry.ValueFormat;
                    if (!string.IsNullOrWhiteSpace(format)) return format;
                }
            }
            return command ? CommandMemberNameFormat : ValueMemberNameFormat;
        }
        [SerializeField] private List<NativePropertyNotification> nativePropertyNotifications = new List<NativePropertyNotification>();

        public NativePropertyNotification ResolvePropertyNotification(Type component, string property)
        {
            for (var type = component; type != null; type = type.BaseType)
                foreach (var entry in nativePropertyNotifications)
                    if (entry != null && entry.Property == property && !string.IsNullOrEmpty(entry.ComponentType) && Type.GetType(entry.ComponentType, false) == type)
                        return entry;
            return BuiltInPropertyNotification(component, property);
        }

        public static NativePropertyNotification BuiltInPropertyNotification(Type component, string property)
        {
            for (var type = component; type != null; type = type.BaseType)
            {
                string expected = null, setter = null;
                switch (type.FullName)
                {
                    case "UnityEngine.UI.Slider":
                    case "UnityEngine.UI.Scrollbar":
                    case "UnityEngine.UI.Dropdown":
                    case "TMPro.TMP_Dropdown": expected = "value"; setter = "SetValueWithoutNotify"; break;
                    case "UnityEngine.UI.Toggle": expected = "isOn"; setter = "SetIsOnWithoutNotify"; break;
                    case "UnityEngine.UI.InputField":
                    case "TMPro.TMP_InputField": expected = "text"; setter = "SetTextWithoutNotify"; break;
                    case "UnityEngine.UI.ScrollRect": expected = "normalizedPosition"; break;
                }
                if (expected == property) return new NativePropertyNotification { Property = property, Event = "onValueChanged", Setter = setter };
            }
            return null;
        }

        [SerializeField] private List<ControlNamingRule> controlNamingRules = CreateControlNamingRules();
        public IReadOnlyList<ControlNamingRule> ControlNamingRules => controlNamingRules;
        public static List<ControlNamingRule> CreateControlNamingRules() => new List<ControlNamingRule>
        {
            new ControlNamingRule("Img_", NativeControlKind.Image),
            new ControlNamingRule("Txt_", NativeControlKind.Text),
            new ControlNamingRule("Btn_", NativeControlKind.Button)
        };
    }
}
