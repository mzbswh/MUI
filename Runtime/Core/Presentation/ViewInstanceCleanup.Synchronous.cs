using System;
using System.Collections.Generic;

namespace MUI
{
    internal static partial class ViewInstanceCleanup
    {
        /// <summary>
        /// 同步释放实例资源，再分别退订宿主监听，最后归还视图。
        /// 调用前须结束激活及 Presenter；能力不足时不开始释放。
        /// </summary>
        internal static void Run(Lifetime instance, IDisposable viewResource, List<Exception> errors,
            params Action[] detachListeners)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (instance.Mode != LifetimeMode.Synchronous || !instance.CanDisposeSynchronously)
            {
                throw new InvalidOperationException("Instance cleanup requires a synchronous lifetime without pending work.");
            }

            try
            {
                instance.Dispose();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            foreach (var detach in detachListeners)
            {
                try
                {
                    detach();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            if (viewResource != null)
            {
                try
                {
                    viewResource.Dispose();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }
        }
    }
}
