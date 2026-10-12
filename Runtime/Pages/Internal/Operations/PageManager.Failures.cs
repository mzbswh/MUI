namespace MUI.Pages
{
    public sealed partial class PageManager
    {
        private CleanupStatus CloseAfterFailure(PageInstance instance, DismissReason reason)
        {
            var cleanup = BeginClose(instance, reason);
            Observe(cleanup);
            return CleanupState(cleanup);
        }
    }
}
