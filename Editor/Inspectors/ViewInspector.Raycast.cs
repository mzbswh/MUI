using System;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MUI.Editor
{
    public sealed partial class ViewInspector
    {
        private bool showRaycastDiagnostics;
        private EventSystem raycastEventSystem;
        private Vector2 raycastPosition;
        private UIRaycastSnapshot raycastSnapshot;
        private string raycastError;
        private string raycastTime;

        private void DrawRaycastDiagnostics()
        {
            showRaycastDiagnostics = EditorGUILayout.Foldout(showRaycastDiagnostics, "实际射线查询（显式执行）", true);
            if (!showRaycastDiagnostics)
            {
                return;
            }
            EditorGUILayout.HelpBox("输入 Game 画面的屏幕像素坐标，左下角为原点。查询会执行 Raycaster 和项目过滤回调，不派发点击或改变焦点；不会自动刷新。", MessageType.Info);
            raycastEventSystem = (EventSystem)EditorGUILayout.ObjectField("事件系统（空为当前）",
                raycastEventSystem, typeof(EventSystem), true);
            raycastPosition = EditorGUILayout.Vector2Field("屏幕坐标", raycastPosition);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("查询当前位置的命中对象"))
                {
                    try
                    {
                        var view = target as View;
                        if (view == null)
                        {
                            throw new InvalidOperationException("目标 View 已不存在。");
                        }
                        var system = raycastEventSystem == null ? EventSystem.current : raycastEventSystem;
                        raycastSnapshot = UIRaycastDiagnostics.Capture(system, raycastPosition, view.gameObject, maxHits: 64);
                        raycastTime = DateTime.Now.ToString("HH:mm:ss");
                        raycastError = null;
                    }
                    catch (Exception error)
                    {
                        raycastSnapshot = null;
                        raycastError = error.Message;
                    }
                }
            }
            if (!string.IsNullOrEmpty(raycastError))
            {
                EditorGUILayout.HelpBox(raycastError, MessageType.Error);
            }
            if (raycastSnapshot == null)
            {
                return;
            }
            EditorGUILayout.LabelField("采集时间 / 坐标", $"{raycastTime} / {raycastSnapshot.Position}");
            EditorGUILayout.LabelField("有效命中 / 失效结果", $"{raycastSnapshot.TotalHits} / {raycastSnapshot.SkippedHits}");
            EditorGUILayout.LabelField("当前 View 首次命中序号", raycastSnapshot.FirstTargetIndex.ToString());
            EditorGUILayout.LabelField("最前命中属于当前 View", raycastSnapshot.TopHitBelongsToTarget.ToString());
            if (raycastSnapshot.IsTruncated)
            {
                EditorGUILayout.HelpBox("仅保留前 64 个有效命中；当前 View 首次命中序号仍按全部结果计算。", MessageType.Warning);
            }
            for (var i = 0; i < raycastSnapshot.Hits.Count; ++i)
            {
                var hit = raycastSnapshot.Hits[i];
                EditorGUILayout.LabelField($"[{i}] {hit.Path}（{hit.ObjectId}）", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("点击处理对象", hit.ClickHandlerId == 0 ? "无" : $"{hit.ClickHandlerPath}（{hit.ClickHandlerId}）", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("射线器", hit.RaycasterType, EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("排序层 / 顺序 / 深度 / 距离", $"{hit.SortingLayer} / {hit.SortingOrder} / {hit.Depth} / {hit.Distance:0.###}");
            }
            EditorGUILayout.HelpBox("结果反映采集时的射线排序。处理目标存在不保证点击执行；按下/拖动状态、Selectable 交互资格、命令门控与自定义输入模块需另行检查。路径最多 64 层、每层名称最多 128 字符，省略号表示截断。", MessageType.Info);
        }
    }
}
