namespace MUI.BasicExample
{
    public sealed class BasicPagePresenter : Presenter<BasicPageViewModel, string, int>
    {
        protected override void OnOpen(string title) => ViewModel.Title = title;
    }
}
