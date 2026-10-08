using System;
using System.Collections.Generic;
using System.IO;
using MUI.Navigation;
using MUI.UGUI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MUI.Editor
{
    /// <summary>宿主的手动运行快照检查器，不保存导航器、业务对象或任务引用。</summary>
    [CustomEditor(typeof(UIHost))]
    public sealed partial class UIHostInspector : UnityEditor.Editor
    {
        private NavigationSnapshot snapshot;
        private readonly List<ViewInstanceSnapshot> filtered = new List<ViewInstanceSnapshot>();
        private TextField search;
        private Label summary;
        private ListView instances;
        private ScrollView details;
        private HelpBox message;
        private IntegerField limit;
        private bool rebuilding;
        private string capturedAt;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            // 保留 Unity 序列化属性的默认编辑、撤销与预制体覆盖体验。
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            var directory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)));
            var path = (directory ?? string.Empty).Replace('\\', '/') + "/UIHostInspector.uss";
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (sheet != null)
            {
                root.styleSheets.Add(sheet);
            }
            else
            {
                root.Add(new HelpBox("运行检查器缺少样式：" + path, HelpBoxMessageType.Warning));
            }
            var panel = new Foldout { text = "导航运行快照（手动采集）", value = true };
            panel.AddToClassList("mui-host-diagnostics");
            root.Add(panel);
            panel.Add(new HelpBox("仅采集已初始化宿主的元数据，不启动宿主或调用业务。快照不会自动刷新；域重载后须重新采集。局部输入与射线请查看 View 检查器。", HelpBoxMessageType.Info));
            limit = new IntegerField("实例与历史上限（1—4096）") { value = 256 };
            panel.Add(limit);
            panel.Add(new Button(Capture) { text = "采集导航快照" });
            panel.Add(new Button(ClearSnapshot) { text = "清空快照" });
            panel.Add(new Button(ShowOverview) { text = "查看焦点与历史" });
            message = new HelpBox("尚未采集。", HelpBoxMessageType.Info);
            panel.Add(message);
            summary = new Label();
            summary.AddToClassList("mui-host-summary");
            panel.Add(summary);
            search = new TextField("筛选路由或句柄");
            search.RegisterValueChangedCallback(_ => RebuildList());
            panel.Add(search);
            instances = new ListView
            {
                itemsSource = filtered,
                fixedItemHeight = 24,
                selectionType = SelectionType.Single,
                makeItem = () =>
                {
                    var label = new Label();
                    label.AddToClassList("mui-host-row");
                    return label;
                },
                bindItem = (element, index) =>
                {
                    var item = filtered[index];
                    var label = (Label)element;
                    label.text = $"#{item.Handle.Id} {item.RouteKey} · {item.State}";
                    label.tooltip = item.Handle.ToString();
                }
            };
            instances.AddToClassList("mui-host-instances");
            instances.selectionChanged += values =>
            {
                if (rebuilding)
                {
                    return;
                }
                foreach (var value in values)
                {
                    var item = (ViewInstanceSnapshot)value;
                    ShowInstance(item);
                    return;
                }
            };
            panel.Add(instances);
            details = new ScrollView();
            details.AddToClassList("mui-host-details");
            panel.Add(details);
            ClearSnapshot();
            AddTraceControls(root);
            return root;
        }

        private void Capture()
        {
            try
            {
                var host = target as UIHost;
                if (!Application.isPlaying || host == null)
                {
                    throw new InvalidOperationException("请在播放模式中选择有效的 UIHost。");
                }
                // Navigator 属性在未初始化时明确报错，不隐式安装运行服务。
                snapshot = host.Navigator.CaptureSnapshot(limit.value);
                capturedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                summary.text = $"采集于 {capturedAt}\n宿主退出：{snapshot.IsShutdown}；提交版本：{snapshot.CommitVersion}\n" +
                    $"实例：{snapshot.Instances.Count}/{snapshot.TotalInstances}；历史：{snapshot.RecentHistory.Count}/{snapshot.HistoryCount}\n" +
                    $"缓存：{snapshot.CachedViewCount}；正在释放缓存：{snapshot.RetiringCachedViewCount}；预加载占位：{snapshot.PreloadReservationCount}\n" +
                    $"排队请求：{snapshot.PendingRequestCount}；延后请求：{snapshot.PostedRequestCount}；超时未清理：{snapshot.PendingCleanupCount}；丢弃事件：{snapshot.DroppedLifecycleEventCount}\n" +
                    $"当前未确认清理责任：{snapshot.UnconfirmedCleanupCount}；历史清理失败：{snapshot.HasCleanupFailure}";
                message.text = snapshot.IsPresentationSettled ? "采集成功。门控只代表导航策略，不证明最终显示或点击资格。" : "采集时表现尚未收敛，门控和覆盖来源可能处于过渡状态。";
                message.messageType = snapshot.IsPresentationSettled ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning;
                if (snapshot.InstancesTruncated || snapshot.HistoryTruncated || snapshot.CleanupResponsibilitiesTruncated)
                {
                    message.text += " 实例、历史或清理责任已截断，不能视为完整账本。";
                }
                RebuildList();
                ShowOverview();
            }
            catch (Exception error)
            {
                // 失败时清空旧结果，避免把上一次成功快照误认为本次采集。
                ClearSnapshot();
                message.text = error.Message;
                message.messageType = HelpBoxMessageType.Error;
            }
        }

        private void ClearSnapshot()
        {
            snapshot = null;
            summary.text = string.Empty;
            message.text = "尚未采集。";
            message.messageType = HelpBoxMessageType.Info;
            RebuildList();
            details.Clear();
        }

        private void RebuildList()
        {
            rebuilding = true;
            try
            {
                instances.ClearSelection();
                filtered.Clear();
                if (snapshot != null)
                {
                    var query = search.value ?? string.Empty;
                    foreach (var item in snapshot.Instances)
                    {
                        if (item.RouteKey.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            item.Handle.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            filtered.Add(item);
                        }
                    }
                }
                instances.Rebuild();
            }
            finally
            {
                rebuilding = false;
            }
        }

        private void NavigateTo(ViewHandle handle)
        {
            if (snapshot == null)
            {
                return;
            }
            foreach (var item in snapshot.Instances)
            {
                if (item.Handle != handle)
                {
                    continue;
                }
                search.SetValueWithoutNotify(string.Empty);
                RebuildList();
                var index = filtered.IndexOf(item);
                instances.SetSelection(index);
                instances.ScrollToItem(index);
                ShowInstance(item);
                return;
            }
            details.Clear();
            AddDetail("快照时间", capturedAt);
            details.Add(new HelpBox("该句柄不在本次快照中，可能已关闭或被截断；请重新采集或提高上限：" + handle, HelpBoxMessageType.Info));
        }
    }
}
