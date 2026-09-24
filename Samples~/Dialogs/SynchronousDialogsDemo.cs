using System;
using MUI.Dialogs;
using MUI.Navigation;
using MUI.Resources;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Dialogs
{
    /// <summary>
    /// 完全同步的确认与提示流程：直接打开，通过一次性订阅接收用户稍后提交的结果。
    /// 本组件独占并负责关闭配置的宿主，不与其他启动组件共用同一个 UIHost。
    /// </summary>
    public sealed class SynchronousDialogsDemo : MonoBehaviour
    {
        [SerializeField] private UIHost host = null;
        [SerializeField] private string confirmationKey = "demo.confirmation";
        [SerializeField] private string alertKey = "demo.alert";
        private readonly Lifetime observations = new Lifetime(LifetimeMode.Synchronous);
        private Route<ConfirmationViewModel, CloseConfirmation, bool> confirmationRoute;
        private Route<AlertViewModel, AlertRequest, Unit> alertRoute;
        private ViewHandle active;
        private bool initialized;
        private bool pendingAlert;

        private void Start()
        {
            if (host == null)
            {
                Debug.LogError("请配置此示例独占的 UIHost 及两种对话框的 Prefab 目录。", this);
                return;
            }

            try
            {
                confirmationRoute = ConfirmationDialog.CreateRoute(new ViewResource(confirmationKey));
                alertRoute = AlertDialog.CreateRoute(new ViewResource(alertKey));
                host.InitializeSynchronous();
                initialized = true;
                ShowConfirmation();
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }
        }

        /// <summary>打开后立即返回；再次请求时保留当前对话框，不借用或覆盖别人的结果。</summary>
        [ContextMenu("同步显示确认框")]
        public void ShowConfirmation()
        {
            if (!CanOpen() || pendingAlert)
            {
                return;
            }

            var opened = host.Navigator.Open(confirmationRoute, new CloseConfirmation(
                "提交示例", "是否提交此次操作？取消不会执行业务行为。", "提交", "取消"));
            if (!opened.IsSuccess)
            {
                Debug.LogWarning($"确认框打开失败：{opened.Status}/{opened.Rejection}", this);
                if (opened.Error != null)
                {
                    Debug.LogException(opened.Error, this);
                }
                return;
            }

            active = opened.Handle.Identity;
            opened.Handle.ObserveResult(observations, result =>
            {
                active = default;
                if (!AcceptResult(result))
                {
                    return;
                }

                // 用户确认与取消都有正常类型化结果；返回/外部关闭则属于 Dismissed。
                if (result.IsCompleted && result.Value)
                {
                    Debug.Log("已取得确认结果，此处可执行项目的同步业务操作。", this);
                    // 只登记后续意图，在 Update 退出导航结果通知栈后再打开下一界面。
                    pendingAlert = true;
                }
                else
                {
                    Debug.Log($"用户未确认：{result.Status}/{result.Reason}", this);
                }
            });
        }

        private void Update()
        {
            if (!pendingAlert || !CanOpen())
            {
                return;
            }

            pendingAlert = false;
            ShowAlert();
        }

        /// <summary>独立提示框也通过同步打开和结果订阅完成，不安装异步确认服务。</summary>
        [ContextMenu("同步显示提示框")]
        public void ShowAlert()
        {
            if (!CanOpen() || pendingAlert)
            {
                return;
            }

            var opened = host.Navigator.Open(alertRoute,
                new AlertRequest("操作完成", "此流程没有创建或等待异步任务。", "知道了"));
            if (!opened.IsSuccess)
            {
                Debug.LogWarning($"提示框打开失败：{opened.Status}/{opened.Rejection}", this);
                if (opened.Error != null)
                {
                    Debug.LogException(opened.Error, this);
                }
                return;
            }

            active = opened.Handle.Identity;
            opened.Handle.ObserveResult(observations, result =>
            {
                active = default;
                if (AcceptResult(result))
                {
                    Debug.Log($"提示框结束：{result.Status}，清理={result.Cleanup}", this);
                }
            });
        }

        /// <summary>沿用正常关闭协议；不会把无结果关闭当成用户确认。</summary>
        [ContextMenu("同步关闭当前对话框")]
        public void CloseDialog()
        {
            if (initialized && host != null && active.IsValid)
            {
                var closed = host.Navigator.Close(active);
                Debug.Log($"对话框关闭：{closed.Status}，清理={closed.Cleanup}", this);
            }
        }

        /// <summary>复用宿主的返回与模态规则；项目也可将返回按键接到 UIHost.RequestBack。</summary>
        [ContextMenu("同步返回")]
        public void Back()
        {
            if (initialized && host != null)
            {
                var closed = host.Navigator.Back();
                Debug.Log($"返回结果：{closed.Status}，清理={closed.Cleanup}", this);
            }
        }

        private bool CanOpen() => initialized && host != null && !observations.IsEnded && !active.IsValid;

        private bool AcceptResult<T>(ViewResult<T> result)
        {
            if (result.Error != null)
            {
                Debug.LogException(result.Error, this);
                return false;
            }
            if (result.Cleanup != CleanupStatus.Complete)
            {
                Debug.LogError($"对话框清理未成功，停止后续业务：{result.Cleanup}", this);
                return false;
            }
            return true;
        }

        private void OnDestroy()
        {
            pendingAlert = false;
            // 先撤销订阅，避免宿主关闭时向正在销毁的组件派发业务结果。
            try
            {
                observations.Dispose();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }

            if (initialized && host != null)
            {
                try
                {
                    host.Shutdown();
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                }
            }
            initialized = false;
            active = default;
        }
    }
}
