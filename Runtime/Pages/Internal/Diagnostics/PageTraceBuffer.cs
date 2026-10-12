using System;
using MUI.Pages.Diagnostics;

namespace MUI.Pages
{
    internal sealed class PageTraceBuffer
    {
        private PageTraceEntry[] traceEntries;
        private bool traceRecording;
        private int traceStart;
        private int traceCount;
        private long traceOverwritten;
        private object traceSession;
        internal long NextOperationId;

        internal void Start(int capacity = 512)
        {
            if (capacity < 1 || capacity > 8192)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }
            var buffer = new PageTraceEntry[capacity];
            traceEntries = buffer;
            traceSession = new object();
            traceStart = 0;
            traceCount = 0;
            traceOverwritten = 0;
            traceRecording = true;
        }
        internal void Stop(bool clear = false)
        {
            traceRecording = false;
            if (clear)
            {
                traceEntries = null;
                traceStart = 0;
                traceCount = 0;
                traceOverwritten = 0;
            }
        }
        internal PageTraceSnapshot Capture(long droppedEvents)
        {
            var entries = new PageTraceEntry[traceCount];
            for (var i = 0; i < traceCount; ++i)
            {
                entries[i] = traceEntries[(traceStart + i) % traceEntries.Length];
            }
            return new PageTraceSnapshot(traceRecording, traceEntries == null ? 0 : traceEntries.Length,
                traceOverwritten, droppedEvents, entries);
        }
        internal void Append(PageTraceEntry entry)
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
        internal bool IsRecording => traceRecording;
        internal object Session => traceSession;
        internal int Capacity => traceEntries == null ? 0 : traceEntries.Length;
    }
}
