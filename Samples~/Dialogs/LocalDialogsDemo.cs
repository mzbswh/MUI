using System;
using System.Threading.Tasks;
using MUI.Dialogs;
using MUI.Navigation;
using MUI.Resources;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Dialogs
{
    /// <summary>
    /// 本地 Prefab 对话框示例，创建和关闭使用统一导航协议。
    /// 本组件独占配置的宿主，并在销毁时取消等待和关闭宿主。
    /// </summary>
    public sealed class LocalDialogsDemo : MonoBehaviour
    {
        [SerializeField] private UIHost host = null;
        [SerializeField] private string confirmationKey = "demo.confirmation";
        [SerializeField] private string alertKey = "demo.alert";
        private readonly LifetimeScope lifetime = new LifetimeScope();
        private Route<ConfirmationViewModel, CloseConfirmation, bool> confirmationRoute;
        private Route<AlertViewModel, AlertRequest, Unit> alertRoute;
        private ViewHandle active;
        private bool initialized;
        private bool showing;

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
                host.Initialize();
                initialized = true;
                ShowConfirmation();
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }
        }

        /// <summary>确认后等待窗口清理，再展示操作完成提示；重复请求不覆盖活动窗口。</summary>
        [ContextMenu("显示确认框")]
        public void ShowConfirmation() => Show(ConfirmAsync);

        private async Task ConfirmAsync()
        {
            var result = await OpenAsync(confirmationRoute, new CloseConfirmation(
                "提交示例", "是否提交此次操作？取消不会执行业务行为。", "提交", "取消"));
            lifetime.Token.ThrowIfCancellationRequested();
            if (result.IsCompleted && result.Value)
            {
                Debug.Log("已取得确认结果，此处可执行项目业务操作。", this);
                await AlertAsync();
            }
            else
            {
                Debug.Log($"用户未确认：{result.Status}/{result.Reason}", this);
            }
        }

        /// <summary>展示独立提示框，等待用户操作及窗口清理。</summary>
        [ContextMenu("显示提示框")]
        public void ShowAlert() => Show(AlertAsync);

        private async Task AlertAsync()
        {
            var result = await OpenAsync(alertRoute,
                new AlertRequest("操作完成", "本地资源与异步资源使用同一套导航接口。", "知道了"));
            lifetime.Token.ThrowIfCancellationRequested();
            Debug.Log($"提示框结束：{result.Status}", this);
        }

        private async Task<ViewResult<TResult>> OpenAsync<TModel, TArgs, TResult>(
            Route<TModel, TArgs, TResult> route, TArgs args) where TModel : ViewModel
        {
            var opened = await host.Navigator.OpenAsync(route, args, lifetime.Token);
            if (!opened.IsSuccess)
            {
                throw new InvalidOperationException($"对话框打开失败：{opened.Status}/{opened.Rejection}", opened.Error);
            }

            active = opened.Handle.Identity;
            try
            {
                var result = await opened.Handle.WaitForResultAsync(lifetime.Token);
                // 结果提交不等于退出完成；保留活动句柄直到资源真正归还。
                var cleanup = await host.Navigator.WaitForCleanupAsync(active, lifetime.Token);
                if (result.Error != null)
                {
                    throw result.Error;
                }
                if (cleanup.Error != null || cleanup.Cleanup != CleanupStatus.Complete)
                {
                    throw new InvalidOperationException($"对话框清理失败：{cleanup.Cleanup}", cleanup.Error);
                }
                return result;
            }
            finally
            {
                active = default;
            }
        }

        private async void Show(Func<Task> operation)
        {
            if (!initialized || host == null || lifetime.IsEnded || showing)
            {
                return;
            }

            showing = true;
            try
            {
                await operation();
            }
            catch (OperationCanceledException) when (lifetime.IsEnded) { }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
            finally
            {
                showing = false;
            }
        }

        /// <summary>普通关闭没有确认值，不执行业务确认操作。</summary>
        [ContextMenu("关闭当前对话框")]
        public async void CloseDialog()
        {
            if (!initialized || host == null || !active.IsValid)
            {
                return;
            }
            try
            {
                await host.Navigator.CloseAsync(active, lifetime.Token);
            }
            catch (OperationCanceledException) when (lifetime.IsEnded)
            {
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        /// <summary>沿用导航器的返回与模态规则。</summary>
        [ContextMenu("返回")]
        public async void Back()
        {
            if (!initialized || host == null)
            {
                return;
            }
            try
            {
                await host.Navigator.BackAsync(lifetime.Token);
            }
            catch (OperationCanceledException) when (lifetime.IsEnded)
            {
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        private async void OnDestroy()
        {
            var shutdown = initialized;
            initialized = false;
            try
            {
                await lifetime.DisposeAsync();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
            if (shutdown && host != null)
            {
                try
                {
                    await host.ShutdownAsync();
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                }
            }
            active = default;
        }
    }
}
