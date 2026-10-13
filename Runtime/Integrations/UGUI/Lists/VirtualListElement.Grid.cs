using System;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class VirtualListElement
    {
        [SerializeField]
        private bool automaticColumns;
        [SerializeField, Min(1)]
        private float minimumColumnWidth = 100;

        private bool IsUniformGrid => automaticColumns || columns > 1;

        /// <summary>是否按可用宽度计算列数；即使结果为一列，也保持规则 Grid 的统一尺寸。</summary>
        public bool AutomaticColumns => automaticColumns;

        /// <summary>
        /// 初始化前启用纵向规则 Grid 的自动列数。单元格均分可用宽度；
        /// 宽度不足时保留一列，最小宽度是列数计算依据，不强制内容超出视口。
        /// </summary>
        public void ConfigureAutomaticColumns(float minimumWidth)
        {
            RequireListAlive();
            if (initialized)
            {
                throw new InvalidOperationException("Configure automatic columns before initialization.");
            }
            if (IsHorizontal)
            {
                throw new InvalidOperationException("Automatic columns require vertical scrolling.");
            }
            if (minimumWidth <= 0 || float.IsNaN(minimumWidth) || float.IsInfinity(minimumWidth))
            {
                throw new ArgumentOutOfRangeException(nameof(minimumWidth));
            }

            minimumColumnWidth = minimumWidth;
            automaticColumns = true;
        }

        private void ValidateAutomaticColumns()
        {
            if (automaticColumns && (IsHorizontal || minimumColumnWidth <= 0 ||
                float.IsNaN(minimumColumnWidth) || float.IsInfinity(minimumColumnWidth)))
            {
                throw new InvalidOperationException("Automatic columns require vertical scrolling and a finite positive minimum width.");
            }
        }

        private int CalculateColumnCount()
        {
            var width = AvailableGridWidth;
            if (float.IsNaN(width) || float.IsInfinity(width))
            {
                throw new InvalidOperationException("Grid width must be finite.");
            }
            var count = Math.Max(1, Math.Floor((Math.Max(0, (double)width) + spacing.x) / ((double)minimumColumnWidth + spacing.x)));
            if (count > maxCells)
            {
                throw new InvalidOperationException("Automatic grid column count exceeds the configured cell capacity.");
            }
            return (int)count;
        }

        private void UpdateAutomaticColumns()
        {
            if (automaticColumns && !IsVisualRetentionActive)
            {
                SetColumnCount(CalculateColumnCount());
            }
        }
    }
}
