namespace MUI.Samples.Settings
{
    /// <summary>演示输入失败保留草稿与模型值；成功时由绑定回写规范化后的名称。</summary>
    public sealed class PlayerNameConverter : IValidatingBindingConverter<string, string>
    {
        public string Convert(string value) => value;

        public BindingConversionResult<string> TryConvertBack(string value)
        {
            var name = value == null ? string.Empty : value.Trim();
            return name.Length == 0
                ? BindingConversionResult<string>.Failure("Enter a player name.")
                : BindingConversionResult<string>.Success(name);
        }
    }
}
