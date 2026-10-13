using System;

namespace MUI.Pages
{
    [Serializable]
    public sealed class PagePolicyPreset
    {
        public string Name;
        public PagePolicyFields Overrides;
        public PagePolicySettings Values = new PagePolicySettings();

        public PagePolicyPreset(string name, PagePolicyFields overrides)
        {
            Name = name;
            Overrides = overrides;
        }
    }
}
