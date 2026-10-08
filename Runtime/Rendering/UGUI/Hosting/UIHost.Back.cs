using System;
using System.Threading.Tasks;
using MUI.Navigation;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MUI.UGUI
{
    public sealed partial class UIHost
    {
        private bool backInputPending;
        private int lastBackInputFrame = -1;

        /// <summary>已接纳的返回输入完成后发布；状态可能是关闭、已处理、阻止或没有目标。</summary>
        public event Action<CloseOutcome> BackInputCompleted;

        /// <summary>
        /// 供按钮、输入动作或平台返回回调连接的无参数入口；必须在 Unity 主线程调用。
        /// 输入系统应仅在按下阶段派发，同一手势由局部取消或此入口之一处理。
        /// </summary>
        public void RequestBack() => TryRequestBack();

        /// <summary>
        /// 接纳一次返回输入。同帧重复输入、尚未完成的返回及不可用宿主返回 false。
        /// true 仅表示已接纳，不代表页面已关闭；具体结果通过 BackInputCompleted 发布。
        /// 在视觉退出或请求拒绝前拒绝追加输入，不积压请求。
        /// </summary>
        public bool TryRequestBack() => TryRequestBack(GetInputEventSystem());

        /// <summary>从指定 EventSystem 接纳返回；多 EventSystem 输入适配器应传入事件实际来源。</summary>
        public bool TryRequestBack(EventSystem inputSystem)
        {
            UnityMainThread.Require();
            if (inputSystem == null)
            {
                inputSystem = EventSystem.current;
            }

            var recipient = ResolveBackRecipient(inputSystem);
            return recipient != null && recipient.TryRequestBackForSelf(inputSystem);
        }

        private bool TryRequestBackForSelf(EventSystem inputSystem)
        {
            if (this == null || !isActiveAndEnabled || navigator == null || navigator.IsShutdown ||
                shutdown != null || backInputPending || lastBackInputFrame == Time.frameCount)
            {
                return false;
            }

            if (!BackInputConsumption.TryConsume(inputSystem))
            {
                return false;
            }

            // 先占位，阻止关闭回调和完成通知再次派发同一帧的输入。
            lastBackInputFrame = Time.frameCount;
            backInputPending = true;
            _ = DispatchBackInputAsync();

            return true;
        }

        private async Task DispatchBackInputAsync()
        {
            var releaseStarted = false;
            try
            {
                var operation = navigator.BackAsync(out var inputSettled);
                _ = ReleaseBackInputAsync(inputSettled);
                releaseStarted = true;
                var outcome = await operation;
                PublishBackInput(outcome);
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
                if (!releaseStarted)
                {
                    backInputPending = false;
                }
            }
        }

        private async Task ReleaseBackInputAsync(Task inputSettled)
        {
            try
            {
                await inputSettled;
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
            finally
            {
                backInputPending = false;
            }
        }

        private void PublishBackInput(CloseOutcome outcome)
        {
            // 退出期间的迟到结果不再回调已释放的项目界面。
            if (this == null || navigator.IsShutdown || shutdown != null)
            {
                return;
            }

            var handlers = BackInputCompleted;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<CloseOutcome> handler in handlers.GetInvocationList())
            {
                // 前一个订阅者可能已经关闭宿主。
                if (this == null || navigator.IsShutdown || shutdown != null)
                {
                    break;
                }

                try
                {
                    handler(outcome);
                }
                catch (Exception error)
                {
                    UIErrors.Report(error);
                }
            }
        }
    }
}
