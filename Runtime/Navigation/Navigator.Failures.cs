namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private CleanupStatus CloseAfterFailure(ViewInstance instance, DismissReason reason)
        {
            var cleanup = BeginClose(instance, reason);
            Observe(cleanup);
            return CleanupState(cleanup);
        }

    }
}
