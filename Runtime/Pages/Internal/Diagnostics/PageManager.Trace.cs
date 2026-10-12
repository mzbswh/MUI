using MUI.Pages.Diagnostics;

namespace MUI.Pages
{
    public sealed partial class PageManager
    {
        private readonly PageTraceBuffer traceBuffer = new PageTraceBuffer();
        public void StartLifecycleTrace(int capacity = 512)
        {
            AssertThread();
            traceBuffer.Start(capacity);
        }
        public void StopLifecycleTrace(bool clear = false)
        {
            AssertThread();
            traceBuffer.Stop(clear);
        }
        public PageTraceSnapshot CaptureLifecycleTrace()
        {
            AssertThread();
            return traceBuffer.Capture(DroppedLifecycleEventCount);
        }
        private void RecordLifecycleTrace(PageEvent value)
        {
            if (traceBuffer.IsRecording)
                traceBuffer.Append(new PageTraceEntry(value));
        }
        private void AppendTrace(PageTraceEntry entry) => traceBuffer.Append(entry);
    }
}
