namespace MUI
{
    public enum AccessibilityRole
    {
        Unspecified,
        Generic,
        Text,
        Button,
        Toggle,
        Slider,
        TextInput,
        Dropdown,
        Image,
        List,
        Tab,
        TabList,
        ProgressBar
    }

    [System.Flags]
    public enum AccessibilityState
    {
        None = 0,
        Disabled = 1,
        Selected = 2,
        Checked = 4,
        Expanded = 8,
        ReadOnly = 16,
        Busy = 32,
        Invalid = 64
    }

    /// <summary>供检查与可选平台桥接使用的语义数据，不是原生屏幕阅读器实现。</summary>
    public interface IAccessibleElement : IElement
    {
        string AccessibilityLabel
        {
            get;
        }

        string AccessibilityDescription
        {
            get;
        }

        string AccessibilityValue
        {
            get;
        }

        AccessibilityRole SemanticRole
        {
            get;
        }

        AccessibilityState SemanticState
        {
            get;
        }

        int AccessibilityOrder
        {
            get;
        }

        bool AccessibilityHidden
        {
            get;
        }
    }
}
