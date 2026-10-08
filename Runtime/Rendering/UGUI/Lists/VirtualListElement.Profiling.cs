using Unity.Profiling;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class VirtualListElement
    {
        private static readonly ProfilerMarker updateMarker = new ProfilerMarker("MUI.VirtualList.LateUpdate");
        private static readonly ProfilerMarker rangeMarker = new ProfilerMarker("MUI.VirtualList.VisibleRange");
        private static readonly ProfilerMarker cellLayoutMarker = new ProfilerMarker("MUI.VirtualList.CellLayout");
        private static readonly ProfilerMarker nativeMeasurementMarker = new ProfilerMarker("MUI.VirtualList.NativeMeasurement");
        private static readonly ProfilerMarker refreshMarker = new ProfilerMarker("MUI.VirtualList.RefreshSlice");
        private static readonly ProfilerMarker itemBindingMarker = new ProfilerMarker("MUI.VirtualList.ItemBinding");
        private int measurementFrame = -1;
        private int measurementAttempts;
        private long totalMeasurementAttempts;

        /// <summary>只读测量计数，不强制布局或物化条目；Frame 为 -1 表示尚未执行维护帧。</summary>
        public VirtualListMeasurementSnapshot MeasurementSnapshot
        {
            get
            {
                RequireListAlive();
                return new VirtualListMeasurementSnapshot(measurementFrame, measurementAttempts, totalMeasurementAttempts);
            }
        }

        private void BeginMeasurementFrame()
        {
            var frame = Time.frameCount;
            if (measurementFrame != frame)
            {
                measurementFrame = frame;
                measurementAttempts = 0;
            }
        }

        private void RecordMeasurementAttempt()
        {
            BeginMeasurementFrame();
            if (measurementAttempts < int.MaxValue)
            {
                ++measurementAttempts;
            }

            if (totalMeasurementAttempts < long.MaxValue)
            {
                ++totalMeasurementAttempts;
            }
        }
    }
}
