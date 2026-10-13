using MUI.UGUI;

namespace MUI.Examples
{
    [UIPage(typeof(Unit), typeof(Unit), ResourceKey = "MUIExamples/Resources")]
    public partial class ResourcesViewModel : ViewModel
    {
        private bool alternate;

        [ObservableProperty, Bind("Artwork", nameof(ImageElement.SpriteSource), ElementType = typeof(ImageElement))]
        private string artwork = "MUIExamples/Artwork/Forest";

        [Command]
        private void Switch()
        {
            alternate = !alternate;
            Artwork = "MUIExamples/Artwork/" + (alternate ? "Dunes" : "Forest");
            Status = (alternate ? "沙丘" : "森林") + "：上一张图片的资源凭证已释放";
        }

        [Command]
        private void Clear()
        {
            Artwork = null;
            Status = "已清空资源键，图片凭证已释放";
        }
    }
}
