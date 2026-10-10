using System;
using System.Collections.Generic;

namespace MUI.Navigation
{
    /// <summary>宿主共享排序池；Maximum + 1 专供关闭手势屏障，不分配给页面。</summary>
    public sealed class RenderOrderOptions
    {
        private readonly Dictionary<int, int> layerGaps = new Dictionary<int, int>();

        public RenderOrderOptions(int minimum = -16000, int maximum = 16000,
            int defaultPageSpan = 32, int preferredGap = 32, int sortingLayerId = 0, IReadOnlyDictionary<int, int> layerGapOverrides = null, int preferredLayerGap = 128)
        {
            if (minimum < short.MinValue || maximum >= short.MaxValue || maximum < minimum ||
                defaultPageSpan < 2 || defaultPageSpan > maximum - minimum + 1 || preferredGap < 0 || preferredGap > maximum - minimum + 1 ||
                preferredLayerGap < 0 || preferredLayerGap > maximum - minimum + 1)
            {
                throw new ArgumentOutOfRangeException(nameof(minimum), "排序范围必须在 -32768～32766 内，页面跨度至少为 2，预留间隔不得超过总范围。");
            }
            if (layerGapOverrides != null)
            {
                foreach (var entry in layerGapOverrides)
                {
                    if (entry.Value < 0 || entry.Value > maximum - minimum + 1)
                    {
                        throw new ArgumentOutOfRangeException(nameof(layerGapOverrides), "Layer 预留间隔必须在 0 到宿主总范围之间。");
                    }
                    layerGaps.Add(entry.Key, entry.Value);
                }
            }
            Minimum = minimum;
            Maximum = maximum;
            DefaultPageSpan = defaultPageSpan;
            PreferredGap = preferredGap;
            PreferredLayerGap = preferredLayerGap;
            SortingLayerId = sortingLayerId;
        }

        public int Minimum
        {
            get;
        }

        public int Maximum
        {
            get;
        }

        public int DefaultPageSpan
        {
            get;
        }

        public int PreferredGap
        {
            get;
        }

        public int PreferredLayerGap
        {
            get;
        }

        public int SortingLayerId
        {
            get;
        }

        /// <summary>取得该 Layer 之后的跨层间隔；未覆盖时使用默认 Layer 间隔。</summary>
        public int GetLayerGapAfter(int layer)
        {
            return layerGaps.TryGetValue(layer, out var gap) ? gap : PreferredLayerGap;
        }
    }
}
