using System;
using System.Collections.Generic;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewScope
    {
        private List<IVisualRetentionView> retainedViews;
        private bool releasingRetainedViews;

        internal bool HasVisualRetention => retainedViews != null || releasingRetainedViews;

        /// <summary>
        /// 在父取消前保留稳定子项的显示门控。候选继续保持隐藏；
        /// 子项不支持保留或保留失败时，撤销本次已经取得的保留状态。
        /// </summary>
        public void BeginVisualRetention()
        {
            RequireActive();
            if (releasingRetainedViews)
            {
                throw new InvalidOperationException("Cannot acquire visual retention while releasing it.");
            }

            if (retainedViews != null)
            {
                return;
            }

            var retained = new List<IVisualRetentionView>();
            retainedViews = retained;
            try
            {
                foreach (var handle in handles.ToArray())
                {
                    var view = handle.GetVisualRetentionView();
                    if (view == null)
                    {
                        continue;
                    }

                    // 外部实现可能先修改自身再抛错，因此先登记回滚对象。
                    retained.Add(view);
                    view.BeginVisualRetention();
                    RequireActive();
                    if (!ReferenceEquals(retainedViews, retained))
                    {
                        throw new OperationCanceledException("Visual retention ended during acquisition.");
                    }
                }
            }
            catch (Exception failure)
            {
                try
                {
                    EndVisualRetention();
                }
                catch (Exception cleanup)
                {
                    throw new AggregateException("Visual retention and rollback failed.", failure, cleanup);
                }

                throw;
            }
        }

        /// <summary>即使父激活已经取消，也要尝试撤销所有子项的显示保留。</summary>
        public void EndVisualRetention()
        {
            RequireThread();
            var retained = retainedViews;
            retainedViews = null;
            if (retained == null)
            {
                return;
            }

            var errors = new List<Exception>();
            releasingRetainedViews = true;
            try
            {
                for (var i = retained.Count - 1; i >= 0; --i)
                {
                    try
                    {
                        if (retained[i].IsAlive)
                        {
                            retained[i].EndVisualRetention();
                        }
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                    }
                }
            }
            finally
            {
                releasingRetainedViews = false;
            }

            if (errors.Count != 0)
            {
                throw new AggregateException("Visual retention release failed.", errors);
            }
        }
    }
}
