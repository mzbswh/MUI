using MUI.Navigation;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        /// <summary>按事件类型读取对应结果；完成事件不保证页面仍然处于打开状态。</summary>
        private static void LogLifecycle(NavigationEvent change)
        {
            var identity = $"路由={change.Route.Key}，序号={change.Sequence}，提交版本={change.CommitVersion}";
            if (change.ArgsUpdateOutcome is ArgsUpdateOutcome args)
            {
                Debug.Log($"MUI 参数更新完成：{identity}，状态={args.Status}，清理={args.Cleanup}，视图故障={args.ViewFaulted}");
            }
            else if (change.RebindOutcome is RebindOutcome rebind)
            {
                Debug.Log($"MUI 模型换绑完成：{identity}，状态={rebind.Status}，清理={rebind.Cleanup}，视图故障={rebind.ViewFaulted}");
            }
            else
            {
                Debug.Log($"MUI 生命周期 {change.Kind}：{identity}，原因={change.Reason}，清理={change.CloseOutcome?.Cleanup}");
            }
        }
    }
}
