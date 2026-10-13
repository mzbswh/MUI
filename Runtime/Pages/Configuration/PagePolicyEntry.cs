using System;

namespace MUI.Pages
{
    [Serializable]
    public class PagePolicyEntry
    {
        public string ResourceKey;
        public string ResourceVersion = "1";
        public string Preset;
        public PagePolicyFields Overrides;
        public PagePolicySettings Values = new PagePolicySettings();
    }
}
