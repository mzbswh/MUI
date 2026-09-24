using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private CloseOutcome FindBackTarget(out ViewInstance target)
        {
            target = null;
            // 禁止从生命周期或另一个返回处理器递归派发。
            if (IsReentrant)
            {
                return BackOutcome(CloseStatus.Reentrant);
            }

            var candidates = activeOrder.Where(entry => (entry.State == ViewState.Open && entry.HostInteractable) ||
                (entry.ExitPending && entry.HostVisible && entry.Route.Policy.Modal)).OrderByDescending(entry => entry.Route.Policy.Modal).ThenByDescending(entry => entry.Route.Policy.Layer).ThenByDescending(entry => entry.Order).ToArray();
            foreach (var instance in candidates)
            {
                if (instance.ExitPending && instance.HostVisible && instance.Route.Policy.Modal)
                {
                    return BackOutcome(CloseStatus.Blocked);
                }

                if (instance.State != ViewState.Open || !instance.HostInteractable)
                {
                    continue;
                }

                if (instance.View is IInputView input && !input.IsInputEnabled)
                {
                    if (instance.Route.Policy.Modal)
                    {
                        return BackOutcome(CloseStatus.Blocked);
                    }

                    continue;
                }

                if (WouldWaitForSelf(instance.Handle))
                {
                    return BackOutcome(CloseStatus.Reentrant);
                }

                if (instance.View is ILocalBackView local)
                {
                    try
                    {
                        bool handled;
                        using (EnterCallback(instance))
                        {
                            handled = local.TryHandleLocalBack();
                        }
                        if (handled || instance.State != ViewState.Open || !instance.HostInteractable ||
                            (instance.View is IInputView currentInput && !currentInput.IsInputEnabled))
                        {
                            return BackOutcome(CloseStatus.Handled);
                        }
                    }
                    catch (Exception error)
                    {
                        UIErrors.Report(error);
                        return new CloseOutcome(CloseStatus.Failed, error, CleanupStatus.NotRequired);
                    }
                }

                var policy = instance.Route.Policy;
                if (!policy.Modal && !policy.EnterHistory && policy.BackBehavior == BackBehavior.Close)
                {
                    continue;
                }

                if (policy.BackBehavior == BackBehavior.Ignore)
                {
                    continue;
                }

                if (policy.BackBehavior == BackBehavior.Block)
                {
                    return BackOutcome(CloseStatus.Blocked);
                }

                if (policy.BackBehavior == BackBehavior.HandleByPresenter)
                {
                    BackResponse response;
                    try
                    {
                        using (EnterCallback(instance))
                        {
                            response = instance.HandleBack();
                        }

                        if (!Enum.IsDefined(typeof(BackResponse), response))
                        {
                            throw new InvalidOperationException("Back handler returned an invalid response.");
                        }
                    }
                    catch (Exception error)
                    {
                        UIErrors.Report(error);
                        return new CloseOutcome(CloseStatus.Failed, error, CleanupStatus.NotRequired);
                    }

                    // 处理器可能请求关闭自身，同一次手势不能继续传给下层。
                    if (instance.State != ViewState.Open || response == BackResponse.Handled)
                    {
                        return BackOutcome(CloseStatus.Handled);
                    }

                    if (response == BackResponse.Ignore)
                    {
                        continue;
                    }
                }

                target = instance;
                return default;
            }

            return BackOutcome(CloseStatus.NotFound);
        }

        private static CloseOutcome BackOutcome(CloseStatus status) => new CloseOutcome(status, cleanup: CleanupStatus.NotRequired);

        private static ValueTask<CloseOutcome> BackResult(CloseStatus status) => new ValueTask<CloseOutcome>(new CloseOutcome(status, cleanup: CleanupStatus.NotRequired));
    }
}
