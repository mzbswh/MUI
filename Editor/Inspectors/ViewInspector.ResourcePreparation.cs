using System;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;

namespace MUI.Editor
{
    public sealed partial class ViewInspector
    {
        private bool showResourcePreparation;
        private ViewResourcePreparationSnapshot resourcePreparationSnapshot;
        private string resourcePreparationError;
        private string resourcePreparationTime;

        private void DrawResourcePreparationDiagnostics()
        {
            showResourcePreparation = EditorGUILayout.Foldout(showResourcePreparation, "首帧资源准备（只读快照）", true);
            if (!showResourcePreparation)
            {
                return;
            }

            if (GUILayout.Button("采集资源准备快照"))
            {
                resourcePreparationSnapshot = null;
                resourcePreparationError = null;
                try
                {
                    var view = target as View;
                    if (view == null)
                    {
                        throw new InvalidOperationException("目标 View 已不存在。");
                    }

                    resourcePreparationSnapshot = view.CaptureResourcePreparationSnapshot(64);
                    resourcePreparationTime = DateTime.Now.ToString("HH:mm:ss");
                }
                catch (Exception error)
                {
                    resourcePreparationError = error.Message;
                }
            }

            if (resourcePreparationError != null)
            {
                EditorGUILayout.HelpBox(resourcePreparationError, MessageType.Error);
            }

            var snapshot = resourcePreparationSnapshot;
            if (snapshot == null)
            {
                EditorGUILayout.HelpBox("手动采集当前默认资源键的准备状态，不会加载资源或改变界面。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("采集时间", resourcePreparationTime);
            EditorGUILayout.LabelField("已启用首帧等待", snapshot.Enabled.ToString());
            if (!snapshot.HasContext)
            {
                EditorGUILayout.HelpBox("没有活动资源上下文：可能尚未激活、没有配置加载器或已经清理。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("激活有效 / 已提交", $"{snapshot.ActivationActive} / {snapshot.Committed}");
            EditorGUILayout.LabelField("准备项 / 等待项", $"{snapshot.TotalCount} / {snapshot.PendingCount}");
            EditorGUILayout.LabelField("失败 / 取消 / 未提交", $"{snapshot.FailedCount} / {snapshot.CancelledCount} / {snapshot.NotAppliedCount}");
            foreach (var entry in snapshot.Entries)
            {
                EditorGUILayout.LabelField(entry.Target, StateLabel(entry.State));
            }

            if (snapshot.IsTruncated)
            {
                EditorGUILayout.HelpBox($"只显示前 {snapshot.Entries.Count} 项，计数包含全部 {snapshot.TotalCount} 项。", MessageType.Info);
            }

            EditorGUILayout.HelpBox("提交后准备账本清空。空账本不代表全部 UI 资源已就绪；手动资源槽、业务任务和子 View 不在此快照内。", MessageType.Info);
        }

        private static string StateLabel(ResourcePreparationState state)
        {
            switch (state)
            {
                case ResourcePreparationState.Loading:
                    return "加载中";
                case ResourcePreparationState.Applied:
                    return "已应用";
                case ResourcePreparationState.NotApplied:
                    return "未提交";
                case ResourcePreparationState.Cancelled:
                    return "已取消";
                case ResourcePreparationState.Failed:
                    return "失败";
                default:
                    return "未知";
            }
        }
    }
}
