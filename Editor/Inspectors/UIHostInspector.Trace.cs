using System;
using MUI.Navigation;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MUI.Editor
{
    public sealed partial class UIHostInspector
    {
        private NavigationTraceSnapshot traceSnapshot;
        private Label traceSummary;
        private HelpBox traceMessage;
        private Button copyTrace;

        private void AddTraceControls(VisualElement root)
        {
            traceSnapshot = null;
            var foldout = new Foldout { text = "导航生命周期追踪", value = false };
            root.Add(foldout);
            foldout.Add(new HelpBox("显式开始后由宿主记录最近 512 个生命周期事件，不随检查器重绘采集。关闭检查器不会停止宿主记录；重复开始清空上一轮。打开、替换、返回、关闭及批量关闭、结果完成、参数更新、模型换绑、预加载、导航器退出、显式等待清理及异步 PostOpen 记录请求起止，并记录候选的依赖、模型、资源与激活准备阶段；尚非完整操作追踪。", HelpBoxMessageType.Info));
            foldout.Add(new Button(() => RunTraceOperation(navigator => navigator.StartLifecycleTrace())) { text = "开始新一轮记录（512 条）" });
            foldout.Add(new Button(() => RunTraceOperation(navigator => navigator.StopLifecycleTrace())) { text = "停止记录，保留时间线" });
            foldout.Add(new Button(() => RunTraceOperation(navigator => navigator.StopLifecycleTrace(clear: true))) { text = "停止并清除时间线" });
            foldout.Add(new Button(() => RunTraceOperation(_ => { })) { text = "采集时间线状态" });
            copyTrace = new Button(() =>
            {
                if (traceSnapshot != null)
                {
                    EditorGUIUtility.systemCopyBuffer = traceSnapshot.ExportText();
                }
            })
            {
                text = "复制已采集报告"
            };
            copyTrace.SetEnabled(false);
            foldout.Add(copyTrace);
            traceSummary = new Label();
            traceSummary.AddToClassList("mui-host-summary");
            foldout.Add(traceSummary);
            traceMessage = new HelpBox("未采集。报告不包含业务参数或异常消息，但路由键仍需由项目避免放入敏感信息。", HelpBoxMessageType.Info);
            foldout.Add(traceMessage);
        }

        private void RunTraceOperation(Action<Navigator> action)
        {
            try
            {
                var host = target as UIHost;
                if (!Application.isPlaying || host == null)
                {
                    throw new InvalidOperationException("请在播放模式中选择有效的 UIHost。");
                }
                var navigator = host.Navigator;
                action(navigator);
                traceSnapshot = navigator.CaptureLifecycleTrace();
                traceSummary.text = $"采集时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n记录中：{traceSnapshot.IsRecording}；保留：{traceSnapshot.Entries.Count}/{traceSnapshot.Capacity}\n" +
                    $"旧记录覆盖：{traceSnapshot.OverwrittenCount}；宿主通知丢弃：{traceSnapshot.DroppedNotificationCount}";
                traceMessage.text = "操作完成。复制报告只导出本次采集的数据，后续事件须重新采集。时间列为相对首条保留记录的间隔；请求结束行另外给出请求总耗时。";
                traceMessage.messageType = HelpBoxMessageType.Info;
                copyTrace.SetEnabled(true);
            }
            catch (Exception error)
            {
                traceSnapshot = null;
                traceSummary.text = string.Empty;
                copyTrace.SetEnabled(false);
                traceMessage.text = error.Message;
                traceMessage.messageType = HelpBoxMessageType.Error;
            }
        }
    }
}
