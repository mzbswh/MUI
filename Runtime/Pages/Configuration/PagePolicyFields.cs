using System;

namespace MUI.Pages
{
    [Flags]
    public enum PagePolicyFields
    {
        None = 0,
        Layer = 1,
        Coverage = 2,
        Focus = 4,
        Modal = 8,
        Back = 16,
        Instances = 32,
        Tick = 64,
        Cache = 128,
        Timeouts = 256,
        RenderOrder = 512,
        PageScope = 1024,
        Input = 2048,
        All = 4095
    }
}
