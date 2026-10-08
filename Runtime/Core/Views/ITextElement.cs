namespace MUI
{
    /// <summary>与文字渲染后端无关的内容契约；排版、字体和材质由具体适配器提供。</summary>
    public interface ITextElement : IElement
    {
        string Content
        {
            get; set;
        }
    }
}
