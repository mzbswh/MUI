namespace MUI.Samples.Navigation
{
    public readonly struct PageArgs
    {
        public PageArgs(string title, int selection)
        {
            Title = title;
            Selection = selection;
        }

        public string Title
        {
            get;
        }

        public int Selection
        {
            get;
        }
    }
}
