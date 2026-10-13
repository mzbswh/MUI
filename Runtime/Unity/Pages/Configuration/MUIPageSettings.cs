using System;
using MUI.Pages;

namespace MUI.Unity
{
    [Serializable]
    public sealed class MUIPageSettings : PagePolicyEntry
    {
#if UNITY_EDITOR
        public string PrefabGuid;
        public string PrefabPath;
        public bool ResourceKeyFromPath;
#endif
    }
}
