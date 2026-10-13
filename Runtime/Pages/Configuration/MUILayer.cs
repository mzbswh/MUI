using System;

namespace MUI.Pages
{
    [Serializable]
    public sealed class MUILayer
    {
        public string Name;
        public int Order;

        public int LayerGapAfter = -1;

        public MUILayer(string name, int order)
        {
            Name = name;
            Order = order;
        }
    }
}
