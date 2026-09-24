using System.Threading.Tasks;
using MUI.UGUI;

namespace MUI.Samples.Settings
{
    [ViewContract("SettingsView")]
    public partial class SettingsViewModel : ViewModel
    {
        [ObservableProperty]
        [Bind("PlayerName", nameof(InputFieldElement.ContentType))]
        private UnityEngine.UI.InputField.ContentType nameContentType = UnityEngine.UI.InputField.ContentType.Standard;
        [ObservableProperty]
        [Bind("PlayerName", nameof(InputFieldElement.LineType))]
        private UnityEngine.UI.InputField.LineType nameLineType = UnityEngine.UI.InputField.LineType.SingleLine;
        [ObservableProperty]
        [Bind("PlayerName", nameof(InputFieldElement.CharacterLimit), bindingMode: BindingMode.OneTime)]
        private int nameCharacterLimit = 16;
        [ObservableProperty]
        [Bind("PlayerName", nameof(InputFieldElement.ReadOnly))]
        private bool nameReadOnly;
        [ObservableProperty]
        [Bind("PlayerName", nameof(InputFieldElement.Value), bindingMode: BindingMode.TwoWay)]
        private string playerName = "Player";
        // 先建立范围，再推送当前值；范围写入会沿用原生 Slider 的裁剪行为。
        [ObservableProperty]
        [Bind("Volume", nameof(SliderElement.MinValue))]
        private float minimumVolume;
        [ObservableProperty]
        [Bind("Volume", nameof(SliderElement.MaxValue))]
        private float maximumVolume = 1;
        [ObservableProperty]
        [Bind("Volume", nameof(SliderElement.WholeNumbers))]
        private bool discreteVolume;
        [ObservableProperty]
        [Bind("Volume", nameof(SliderElement.Value), bindingMode: BindingMode.TwoWay)]
        private float volume = 0.5f;
        [ObservableProperty]
        [Bind("Status", nameof(TextElement.Content))]
        private string status = "Volume: 50%";

        private bool CanSave => Volume > 0;

        [Command]
        [BindCommand("ToggleNameLock", nameof(ButtonElement.Clicked))]
        private void ToggleNameLock()
        {
            NameReadOnly = !NameReadOnly;
            Status = NameReadOnly ? "Name: read only" : "Name: editable";
        }

        [Command(CanExecute = nameof(CanSave))]
        [BindCommand("Save", nameof(ButtonElement.Clicked))]
        private async ValueTask SaveAsync(CommandContext context)
        {
            context.Apply(() => Status = "Saving...");
            await Task.Delay(350, context.Token);
            context.Apply(() => Status = $"Saved: {PlayerName}, {Volume:P0}");
        }

        [Command]
        [BindCommand("Reset", nameof(ButtonElement.Clicked))]
        private void ResetVolume()
        {
            Volume = 0.5f;
            Status = $"Volume: {Volume:P0}";
        }

        protected override void OnPropertyChanged(string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);
            if (propertyName == nameof(Volume))
            {
                Status = $"Volume: {Volume:P0}";
            }
        }
    }
}
