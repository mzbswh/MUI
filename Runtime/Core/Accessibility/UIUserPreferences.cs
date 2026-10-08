using System;
using System.ComponentModel;
using System.Threading;

namespace MUI
{
    /// <summary>独立于主题数据的用户偏好，持久化由项目提供。</summary>
    public sealed class UIUserPreferences : ObservableObject
    {
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private float fontScale = 1;
        private bool reducedMotion;
        private bool publishing;
        private int pending;

        public float FontScale
        {
            get
            {
                RequireThread();
                return fontScale;
            }

            set
            {
                RequireThread();
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Font scale must be positive and finite.");
                }

                if (fontScale == value)
                {
                    return;
                }

                fontScale = value;
                Publish(1);
            }
        }

        public bool ReducedMotion
        {
            get
            {
                RequireThread();
                return reducedMotion;
            }

            set
            {
                RequireThread();
                if (reducedMotion == value)
                {
                    return;
                }

                reducedMotion = value;
                Publish(2);
            }
        }

        private void Publish(int changed)
        {
            pending |= changed;
            if (publishing)
            {
                return;
            }

            publishing = true;
            try
            {
                var passes = 0;
                while (pending != 0)
                {
                    if (++passes > 32)
                    {
                        UIErrors.Report(new InvalidOperationException("UI preferences did not stabilize after 32 updates."));
                        break;
                    }

                    var next = pending;
                    pending = 0;
                    if ((next & 1) != 0)
                    {
                        OnPropertyChangedSafely(nameof(FontScale), UIErrors.Report);
                    }

                    if ((next & 2) != 0)
                    {
                        OnPropertyChangedSafely(nameof(ReducedMotion), UIErrors.Report);
                    }
                }
            }
            finally
            {
                publishing = false;
                pending = 0;
            }
        }

        public IDisposable Observe(LifetimeScope owner, Action<UIUserPreferences> apply)
        {
            RequireThread();
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (apply == null)
            {
                throw new ArgumentNullException(nameof(apply));
            }

            var subscription = new Subscription(this, owner, apply);
            PropertyChanged += subscription.Changed;
            try
            {
                owner.OwnDisposable(subscription);
                subscription.Refresh();
                return subscription;
            }
            catch
            {
                subscription.Dispose();
                throw;
            }
        }

        private void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("UI preferences require their owning UI thread.");
            }
        }

        private sealed class Subscription : IDisposable
        {
            private UIUserPreferences preferences;
            private LifetimeScope owner;
            private Action<UIUserPreferences> apply;
            private bool refreshing;
            private bool pendingRefresh;

            internal Subscription(UIUserPreferences preferences, LifetimeScope owner, Action<UIUserPreferences> apply)
            {
                this.preferences = preferences;
                this.owner = owner;
                this.apply = apply;
            }

            internal void Changed(object sender, PropertyChangedEventArgs args) => Refresh();

            internal void Refresh()
            {
                if (preferences == null || owner.IsEnded)
                {
                    return;
                }

                pendingRefresh = true;
                if (refreshing)
                {
                    return;
                }

                refreshing = true;
                try
                {
                    var passes = 0;
                    while (pendingRefresh && preferences != null && !owner.IsEnded)
                    {
                        if (++passes > 32)
                        {
                            throw new InvalidOperationException("UI preference observer did not stabilize.");
                        }

                        pendingRefresh = false;
                        apply(preferences);
                    }
                }
                finally
                {
                    refreshing = false;
                    pendingRefresh = false;
                }
            }

            public void Dispose()
            {
                if (preferences == null)
                {
                    return;
                }

                preferences.RequireThread();
                preferences.PropertyChanged -= Changed;
                preferences = null;
                owner = null;
                apply = null;
            }
        }
    }
}
