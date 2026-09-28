using System.Diagnostics;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private void RememberTerminal(ViewHandle handle, CloseOutcome result)
        {
            var now = Stopwatch.GetTimestamp();
            terminal[handle] = result;
            terminalOrder.Enqueue(new TerminalRecord(handle, now));
            PruneTerminals(now);
        }

        private bool TryGetTerminal(ViewHandle handle, out CloseOutcome result)
        {
            PruneTerminals(Stopwatch.GetTimestamp());
            return terminal.TryGetValue(handle, out result);
        }

        private void PruneTerminals() => PruneTerminals(Stopwatch.GetTimestamp());

        private void PruneTerminals(long now)
        {
            while (terminalOrder.Count != 0)
            {
                var oldest = terminalOrder.Peek();
                var elapsed = now - oldest.ClosedAt;
                if (terminalOrder.Count <= terminalCapacity &&
                    (elapsed < 0 || elapsed / (double)Stopwatch.Frequency < terminalDuration.TotalSeconds))
                {
                    break;
                }

                terminal.Remove(terminalOrder.Dequeue().Handle);
            }
        }

        private readonly struct TerminalRecord
        {
            internal TerminalRecord(ViewHandle handle, long closedAt)
            {
                Handle = handle;
                ClosedAt = closedAt;
            }

            internal ViewHandle Handle
            {
                get;
            }

            internal long ClosedAt
            {
                get;
            }
        }
    }
}
