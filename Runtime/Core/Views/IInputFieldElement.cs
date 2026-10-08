using System;

namespace MUI
{
    /// <summary>文本输入的公共值、编辑资格和提交契约；字符校验及输入配置由原生后端负责。</summary>
    public interface IInputFieldElement : IElement
    {
        /// <summary>原生编辑结束，包含失去焦点；有效输入先通知 Value，再发布此事件。</summary>
        event Action EditingEnded;

        /// <summary>当前原生文本；模型赋值不触发业务输入事件。</summary>
        string Value
        {
            get; set;
        }

        bool Interactable
        {
            get; set;
        }

        bool ReadOnly
        {
            get; set;
        }

        /// <summary>选择反向值通知的时机；改变模式不提交正在编辑的草稿。</summary>
        TextInputCommitMode CommitMode
        {
            get; set;
        }
    }
}
