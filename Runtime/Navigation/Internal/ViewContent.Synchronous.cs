using System;
using System.Collections.Generic;
using MUI.Resources;

namespace MUI.Navigation
{
    internal sealed partial class ViewContent<TViewModel, TArgs, TResult> where TViewModel : ViewModel
    {
        private ISynchronousViewLease synchronousLease;

        /// <summary>只读检查实例资源与模型能否同步归还；激活仍由导航实例先结束。</summary>
        internal override bool CanReleaseSynchronously => Mode == LifetimeMode.Synchronous &&
            instance.CanDisposeSynchronously && lease == null &&
            (Lifecycle == null || (!Lifecycle.IsRebinding && Lifecycle.CanReleaseModelSynchronously));

        internal void AdoptSynchronous(ISynchronousViewLease acquired)
        {
            if (Mode != LifetimeMode.Synchronous || lease != null || synchronousLease != null || instance.IsEnded)
            {
                throw new InvalidOperationException("Content cannot adopt a synchronous lease in its current state.");
            }

            // 先取得所有权，凭证属性读取失败也必须经过同一同步释放路径。
            synchronousLease = acquired ?? throw new InvalidOperationException("View provider returned a null synchronous lease.");
            View = acquired.View;
            if (View == null || !View.IsAlive)
            {
                throw new InvalidOperationException("View provider returned an invalid synchronous View.");
            }
        }

        internal override void ReleaseCached(List<Exception> errors) => Release(errors);

        /// <summary>调用前结束激活；同步执行销毁、实例释放、宿主退订和视图归还。</summary>
        internal void Release(List<Exception> errors, params Action[] detachHost)
        {
            if (errors == null)
            {
                throw new ArgumentNullException(nameof(errors));
            }

            if (!CanReleaseSynchronously)
            {
                throw new InvalidOperationException("View content cannot be released synchronously while work or unsupported resources remain.");
            }

            if (Lifecycle != null)
            {
                Lifecycle.Destroy(errors);
            }

            ViewInstanceCleanup.Run(instance, synchronousLease, errors, detachHost);
            synchronousLease = null;
            View = null;
            Lifecycle = null;
        }
    }
}
