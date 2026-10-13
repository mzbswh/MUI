using System.Threading.Tasks;
using MUI.UGUI;

namespace MUI.Samples.Settings
{
    [UIWidget("SettingsView")]
    public partial class SettingsViewModel : ViewModel
    {
        [ObservableProperty]
        [Bind("PlayerName", nameof(UnityEngine.UI.InputField.contentType))]
        private UnityEngine.UI.InputField.ContentType nameContentType = UnityEngine.UI.InputField.ContentType.Standard;
        [ObservableProperty]
        [Bind("PlayerName", nameof(UnityEngine.UI.InputField.lineType))]
        private UnityEngine.UI.InputField.LineType nameLineType = UnityEngine.UI.InputField.LineType.SingleLine;
        [ObservableProperty]
        [Bind("PlayerName", nameof(UnityEngine.UI.InputField.characterLimit), bindingMode: BindingMode.OneTime)]
        private int nameCharacterLimit = 16;
        [ObservableProperty]
        [Bind("PlayerName", nameof(UnityEngine.UI.InputField.readOnly))]
        private bool nameReadOnly;
        [ObservableProperty]
        [Bind("PlayerName", nameof(UnityEngine.UI.InputField.text), typeof(PlayerNameConverter),
            BindingMode.TwoWay, ValidationProperty = nameof(NameValidation))]
        private string playerName = "Player";
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(NameError))]
        private BindingValidationState nameValidation;
        // 先建立范围，再推送当前值；范围写入会沿用原生 Slider 的裁剪行为。
        [ObservableProperty]
        [Bind("Volume", nameof(UnityEngine.UI.Slider.minValue))]
        private float minimumVolume;
        [ObservableProperty]
        [Bind("Volume", nameof(UnityEngine.UI.Slider.maxValue))]
        private float maximumVolume = 1;
        [ObservableProperty]
        [Bind("Volume", nameof(UnityEngine.UI.Slider.wholeNumbers))]
        private bool discreteVolume;
        [ObservableProperty]
        [Bind("Volume", nameof(UnityEngine.UI.Slider.value), bindingMode: BindingMode.TwoWay)]
        private float volume = 0.5f;
        [ObservableProperty]
        [Bind("Status", nameof(UnityEngine.UI.Text.text))]
        private string status = "Volume: 50%";

        [Bind("NameError", nameof(UnityEngine.UI.Text.text))]
        public string NameError => NameValidation.Message ?? string.Empty;

        private bool CanSave => Volume > 0 && NameValidation.IsValid;

        [Command]
        [BindCommand("ToggleNameLock", nameof(UnityEngine.UI.Button.onClick))]
        private void ToggleNameLock()
        {
            NameReadOnly = !NameReadOnly;
            Status = NameReadOnly ? "Name: read only" : "Name: editable";
        }

        [Command(CanExecute = nameof(CanSave))]
        [BindCommand("Save", nameof(UnityEngine.UI.Button.onClick))]
        private async ValueTask SaveAsync(CommandContext context)
        {
            context.Apply(() => Status = "Saving...");
            await Task.Delay(350, context.Token);
            context.Apply(() => Status = $"Saved: {PlayerName}, {Volume:P0}");
        }

        [Command]
        [BindCommand("Reset", nameof(UnityEngine.UI.Button.onClick))]
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
