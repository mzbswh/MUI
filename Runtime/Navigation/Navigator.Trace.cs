using System;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private NavigationTraceEntry[] traceEntries;
        private bool traceRecording;
        private int traceStart;
        private int traceCount;
        private long traceOverwritten;
        private object traceSession;
        private long nextTracedOperationId;

        /// <summary>在所属 UI 线程开始新一轮记录，清除旧记录；容量为 1 至 8192，不订阅事件或创建任务。</summary>
        public void StartLifecycleTrace(int capacity = 512)
        {
            AssertThread();
            if (capacity < 1 || capacity > 8192)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }
            var buffer = new NavigationTraceEntry[capacity];
            traceEntries = buffer;
            traceSession = new object();
            traceStart = 0;
            traceCount = 0;
            traceOverwritten = 0;
            traceRecording = true;
        }

        /// <summary>停止记录并保留已有时间线；如不再需要可同时清空释放缓冲区。</summary>
        public void StopLifecycleTrace(bool clear = false)
        {
            AssertThread();
            traceRecording = false;
            if (clear)
            {
                traceEntries = null;
                traceStart = 0;
                traceCount = 0;
                traceOverwritten = 0;
            }
        }

        /// <summary>复制按发生顺序排列的已有记录，不执行项目回调，不改变记录状态。</summary>
        public NavigationTraceSnapshot CaptureLifecycleTrace()
        {
            AssertThread();
            var entries = new NavigationTraceEntry[traceCount];
            for (var i = 0; i < traceCount; ++i)
            {
                entries[i] = traceEntries[(traceStart + i) % traceEntries.Length];
            }
            return new NavigationTraceSnapshot(traceRecording, traceEntries == null ? 0 : traceEntries.Length,
                traceOverwritten, DroppedLifecycleEventCount, entries);
        }

        private void RecordLifecycleTrace(NavigationEvent value)
        {
            if (!traceRecording)
            {
                return;
            }
            AppendTrace(new NavigationTraceEntry(value));
        }

        private void AppendTrace(NavigationTraceEntry entry)
        {
            var index = (traceStart + traceCount) % traceEntries.Length;
            traceEntries[index] = entry;
            if (traceCount == traceEntries.Length)
            {
                traceStart = (traceStart + 1) % traceEntries.Length;
                if (traceOverwritten < long.MaxValue)
                {
                    ++traceOverwritten;
                }
            }
            else
            {
                ++traceCount;
            }
        }
    }
}
