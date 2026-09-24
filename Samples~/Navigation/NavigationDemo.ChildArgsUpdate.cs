using System.Threading.Tasks;
using MUI.ChildViews;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        private async Task DemonstrateChildArgsUpdateAsync(ChildViewScope scope,
            ChildViewHandle<ThingItemViewModel, string> child, ThingItemPresenter presenter)
        {
            try
            {
                var updated = await child.UpdateArgsAsync("Iron", cancellation.Token);
                Debug.Log($"MUI 子视图参数更新：状态={updated.Status}，参数={child.Args}，" +
                    $"文本={child.ViewModel.Label}，打开次数={presenter.OpenCount}");
                await scope.DeactivateAsync(child, cancellation.Token);
                await scope.PrepareReactivationAsync(child, cancellation.Token);
                child.Commit();
                Debug.Log($"MUI 子视图更新后恢复：参数={child.Args}，文本={child.ViewModel.Label}，打开次数={presenter.OpenCount}");

                var late = child.UpdateArgsAsync("Slow").AsTask();
                var busy = await child.UpdateArgsAsync("Ignored");
                var deactivation = scope.DeactivateAsync(child, cancellation.Token).AsTask();
                presenter.ArgsRelease.TrySetResult(true);
                var cancelled = await late;
                await deactivation;
                Debug.Log($"MUI 子视图更新期间停用：重复请求={busy.Rejection}，更新={cancelled.Status}，" +
                    $"状态={child.State}，候选释放={presenter.CandidatesDisposed}，关闭回调与候选重叠={presenter.ClosedDuringArgsUpdate}");
                await scope.PrepareReactivationAsync(child, cancellation.Token);
                child.Commit();
            }
            finally
            {
                presenter.ArgsRelease.TrySetResult(true);
            }
        }
    }
}
