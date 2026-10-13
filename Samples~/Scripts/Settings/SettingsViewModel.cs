using System;
using System.Threading.Tasks;
using UnityEngine.UI;

namespace MUI.Examples
{
    [UIPage(typeof(Unit), typeof(Unit), ResourceKey = "MUIExamples/Settings")]
    public partial class SettingsViewModel : ViewModel
    {
        // 普通属性和按钮入口由 Settings.prefab 生成；这里只演示转换、校验和计算属性。
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Initials))]
        [Bind("NamePreview", nameof(Text.text))]
        [Bind("PlayerName", nameof(InputField.text), typeof(PlayerNameConverter), BindingMode.TwoWay,
            ValidationProperty = nameof(NameValidation))]
        private string playerName = "小明";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(NameError))]
        private BindingValidationState nameValidation;

        [Bind("NameError", nameof(Text.text))]
        public string NameError => NameValidation.Message ?? string.Empty;

        [DependsOn(nameof(Volume))]
        [Bind("VolumeLabel", nameof(Text.text))]
        public string VolumeCaption => $"{Volume:P0}";

        [Bind("Initials", nameof(Text.text))]
        public string Initials
        {
            get
            {
                var words = PlayerName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    return "?";
                }
                return (words[0].Substring(0, 1) + (words.Length > 1 ? words[words.Length - 1].Substring(0, 1) : "")).ToUpperInvariant();
            }
        }

        private bool CanSave => Volume > 0 && NameValidation.IsValid;

        partial void OnToggleNameLock(CommandContext context)
        {
            NameReadOnly = !NameReadOnly;
            Status = NameReadOnly ? "名称已锁定，只读模式" : "名称已解锁，可以编辑";
        }

        [Command(CanExecute = nameof(CanSave))]
        private async ValueTask SaveAsync(CommandContext context)
        {
            context.Apply(() => Status = "正在保存…");
            await Task.Delay(350, context.Token);
            context.Apply(() => Status = $"已保存：{PlayerName}，音量 {Volume:P0}");
        }

        partial void OnReset(CommandContext context)
        {
            Volume = 0.5f;
            Status = $"当前音量：{Volume:P0}";
        }

        partial void OnVolumeChanged(float previous, float value)
        {
            Status = $"当前音量：{value:P0}";
        }
    }
}
