using UnityEngine;

namespace MUI.Samples.Navigation
{
    public class PagePresenter : Presenter<PageViewModel, PageArgs, int>
    {
        protected override void OnCreate() => Debug.Log("MUI Navigation OnCreate");

        protected override void OnOpen(PageArgs args)
        {
            ViewModel.Title = args.Title;
            ViewModel.Selection = args.Selection;
            Debug.Log("MUI Navigation OnOpen: " + args.Title);
        }

        protected override void OnCovered() => Debug.Log("MUI Navigation OnCovered: " + ViewModel.Title);

        protected override void OnRevealed() => Debug.Log("MUI Navigation OnRevealed: " + ViewModel.Title);

        protected override void OnFocus() => Debug.Log("MUI Navigation OnFocus: " + ViewModel.Title);

        protected override void OnUnfocus() => Debug.Log("MUI Navigation OnUnfocus: " + ViewModel.Title);

        protected override void OnClose() => Debug.Log("MUI Navigation OnClose: " + ViewModel.Title);

        protected override void OnDestroy() => Debug.Log("MUI Navigation OnDestroy: " + ViewModel.Title);
    }
}
