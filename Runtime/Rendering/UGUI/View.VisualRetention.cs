using System;
using System.Collections.Generic;

namespace MUI.UGUI
{
    public sealed partial class View
    {
        private bool retainingVisuals;
        private bool releasingVisuals;
        private bool retainedVisible;
        private float retainedAlpha;

        internal bool IsRetainingVisuals => retainingVisuals;

        /// <summary>保存当前显示状态；随后收到隐藏门控时继续显示，但不再接收输入。</summary>
        public void BeginVisualRetention()
        {
            RequireAlive();
            Initialize();
            if (releasingVisuals)
            {
                throw new InvalidOperationException("Cannot acquire visual retention while releasing it.");
            }

            if (retainingVisuals)
            {
                return;
            }

            exitAlpha = 1;
            exitVisible = true;
            retainedVisible = IsVisible;
            retainedAlpha = group.alpha;
            retainingVisuals = true;
            try
            {
                if (childViews != null)
                {
                    childViews.BeginVisualRetention();
                }

                RequireAlive();
                if (!retainingVisuals)
                {
                    throw new OperationCanceledException("View visual retention ended during acquisition.");
                }

                ApplyGates();
                RequireAlive();
                if (!retainingVisuals)
                {
                    throw new OperationCanceledException("View visual retention ended while updating input gates.");
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
                    throw new AggregateException("View retention and rollback failed.", failure, cleanup);
                }

                throw;
            }
        }

        /// <summary>恢复应用最新门控；停用后的子项保持隐藏，不恢复旧激活。</summary>
        public void EndVisualRetention()
        {
            if (!retainingVisuals)
            {
                return;
            }

            retainingVisuals = false;
            releasingVisuals = true;
            var errors = new List<Exception>();
            try
            {
                try
                {
                    if (childViews != null)
                    {
                        childViews.EndVisualRetention();
                    }
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }

                foreach (var element in elements.ToArray())
                {
                    try
                    {
                        if (element != null && element.IsAlive)
                        {
                            element.EndVisualRetention();
                        }
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                    }
                }

                try
                {
                    if (IsAlive)
                    {
                        ApplyGates();
                    }
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }
            finally
            {
                releasingVisuals = false;
            }

            if (errors.Count != 0)
            {
                throw new AggregateException("View visual retention release failed.", errors);
            }
        }
    }
}
