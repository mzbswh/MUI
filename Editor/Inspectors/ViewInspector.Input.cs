using System;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;

namespace MUI.Editor
{
    public sealed partial class ViewInspector
    {
        private bool showInputDiagnostics;
        private ViewInputSnapshot inputSnapshot;
        private string inputSnapshotError;
        private string inputSnapshotTime;
        private static readonly (ViewInputRestriction restriction, string label)[] InputRestrictionLabels =
        {
            (ViewInputRestriction.ViewUnavailable, "View 已失效（释放、根组件丢失等）"),
            (ViewInputRestriction.Disposed, "View 已逻辑释放"),
            (ViewInputRestriction.NotInitialized, "尚未完成 View 初始化"),
            (ViewInputRestriction.InactiveHierarchy, "对象或祖先未激活"),
            (ViewInputRestriction.HiddenByHost, "宿主请求隐藏"),
            (ViewInputRestriction.HiddenLocally, "局部请求隐藏"),
            (ViewInputRestriction.HostInputDisabled, "宿主禁用交互"),
            (ViewInputRestriction.LocalInputDisabled, "局部禁用交互"),
            (ViewInputRestriction.LocalGateBlocked, "存在尚未释放的局部输入阻挡"),
            (ViewInputRestriction.LocalGateDisposed, "局部输入门控已释放"),
            (ViewInputRestriction.RetainingVisuals, "正在保留旧画面，仅显示不接收输入"),
            (ViewInputRestriction.MissingCanvasGroup, "根节点缺少 CanvasGroup"),
            (ViewInputRestriction.CanvasGroupInputDisabled, "根 CanvasGroup 禁用 Selectable 交互"),
            (ViewInputRestriction.CanvasGroupRaycastsDisabled, "根 CanvasGroup 禁用射线"),
        };

        private void DrawInputDiagnostics()
        {
            EditorGUILayout.Space();
            showInputDiagnostics = EditorGUILayout.Foldout(showInputDiagnostics, "输入与可见性诊断（只读快照）", true);
            if (!showInputDiagnostics)
            {
                return;
            }
            if (GUILayout.Button("采集输入快照"))
            {
                var view = target as View;
                try
                {
                    inputSnapshot = view == null ? null : view.CaptureInputSnapshot(maxBlockerReasons: 64);
                    inputSnapshotError = view == null ? "目标 View 已不存在。" : null;
                    inputSnapshotTime = DateTime.Now.ToString("HH:mm:ss");
                }
                catch (Exception error)
                {
                    inputSnapshot = null;
                    inputSnapshotError = error.Message;
                }
            }
            if (!string.IsNullOrEmpty(inputSnapshotError))
            {
                EditorGUILayout.HelpBox(inputSnapshotError, MessageType.Error);
            }
            if (inputSnapshot == null)
            {
                EditorGUILayout.HelpBox("点击采集读取当前状态；不会初始化、激活或修改 View。", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField("采集时间", inputSnapshotTime);
            EditorGUILayout.LabelField("View 有效 / 初始化", $"{inputSnapshot.ViewAlive} / {inputSnapshot.Initialized}");
            EditorGUILayout.LabelField("层级激活", inputSnapshot.ActiveInHierarchy.ToString());
            EditorGUILayout.LabelField("宿主可见 / 局部可见", $"{inputSnapshot.HostVisible} / {inputSnapshot.LocalVisible}");
            EditorGUILayout.LabelField("宿主交互 / 局部交互", $"{inputSnapshot.HostInteractable} / {inputSnapshot.LocalInteractable}");
            EditorGUILayout.LabelField("View 可见 / 输入资格", $"{inputSnapshot.ViewVisible} / {inputSnapshot.ViewInputEnabled}");
            EditorGUILayout.LabelField("正在保留旧画面", inputSnapshot.RetainingVisuals.ToString());
            EditorGUILayout.LabelField("观察到的限制", EditorStyles.boldLabel);
            if (inputSnapshot.Restrictions == ViewInputRestriction.None)
            {
                EditorGUILayout.LabelField("未发现此 View 与根 CanvasGroup 的限制。");
            }
            foreach (var item in InputRestrictionLabels)
            {
                if ((inputSnapshot.Restrictions & item.restriction) != 0)
                {
                    EditorGUILayout.LabelField("• " + item.label, EditorStyles.wordWrappedLabel);
                }
            }
            var gate = inputSnapshot.InputGate;
            if (gate == null)
            {
                EditorGUILayout.LabelField("局部门控", "尚未创建；本次采集不会创建它。");
            }
            else
            {
                EditorGUILayout.LabelField("局部阻挡 / 已释放", $"{gate.BlockerCount} / {gate.IsDisposed}");
                foreach (var reason in gate.Reasons)
                {
                    EditorGUILayout.LabelField("• " + reason, EditorStyles.wordWrappedLabel);
                }
                if (gate.IsTruncated)
                {
                    EditorGUILayout.HelpBox($"显示前 {gate.Reasons.Count} 个原因，实际阻挡数为 {gate.BlockerCount}。", MessageType.Warning);
                }
            }
            var group = inputSnapshot.CanvasGroup;
            EditorGUILayout.LabelField("根 CanvasGroup", group.Exists ? "存在" : "缺失");
            if (group.Exists)
            {
                EditorGUILayout.LabelField("组件启用 / 透明度", $"{group.Enabled} / {group.Alpha:0.###}");
                EditorGUILayout.LabelField("允许交互 / 射线", $"{group.Interactable} / {group.BlocksRaycasts}");
                EditorGUILayout.LabelField("忽略父组", group.IgnoreParentGroups.ToString());
            }
            EditorGUILayout.HelpBox("这是手动采集的状态，不会自动刷新。透明度为 0 不等于停止射线；输入资格为 true 也不证明控件能收到点击。祖先组、Graphic、Selectable、EventSystem 及遮挡物仍需检查。", MessageType.Info);
        }
    }
}
