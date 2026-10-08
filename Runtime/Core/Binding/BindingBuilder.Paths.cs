using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace MUI
{
    public sealed partial class BindingBuilder<TViewModel> where TViewModel : ViewModel
    {
        // 每个绑定会话独立持有通知链；退订先使会话失效，再移除全部旧拥有者监听。
        private sealed class SourcePathSubscription<TValue> : IDisposable
        {
            private readonly BindingBuilder<TViewModel> builder;
            private readonly BindingSourcePath<TViewModel, TValue> path;
            private readonly Action changed;
            private readonly INotifyPropertyChanged[] owners;
            private readonly PropertyChangedEventHandler[] handlers;
            private readonly Action connectAction;
            private readonly Action notificationAction;
            private bool refreshing;
            private bool pending;
            private bool disposed;

            internal SourcePathSubscription(BindingBuilder<TViewModel> builder,
                BindingSourcePath<TViewModel, TValue> path, Action changed)
            {
                this.builder = builder;
                this.path = path;
                this.changed = changed;
                connectAction = () => RefreshCore(false);
                notificationAction = () => RefreshCore(true);
                owners = new INotifyPropertyChanged[path.Count];
                handlers = new PropertyChangedEventHandler[path.Count];
                for (var i = 0; i < handlers.Length; ++i)
                {
                    var index = i;
                    handlers[i] = (sender, args) => OnChanged(index, sender, args);
                }
            }

            internal long Revision
            {
                get; private set;
            }

            internal void Connect() => Refresh(false);

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                var errors = new List<Exception>();
                for (var i = owners.Length - 1; i >= 0; --i)
                {
                    var owner = owners[i];
                    owners[i] = null;
                    if (owner != null)
                    {
                        try
                        {
                            owner.PropertyChanged -= handlers[i];
                        }
                        catch (Exception error)
                        {
                            errors.Add(error);
                        }
                    }
                }

                if (errors.Count > 0)
                {
                    throw new AggregateException("Binding source path detachment failed.", errors);
                }
            }

            private void OnChanged(int index, object sender, PropertyChangedEventArgs args)
            {
                if (disposed || !builder.IsBindingThread() || !builder.session.IsActive ||
                    !ReferenceEquals(sender, owners[index]) ||
                    (!string.IsNullOrEmpty(args.PropertyName) && args.PropertyName != path[index].PropertyName))
                {
                    return;
                }

                unchecked
                {
                    ++Revision;
                }
                Refresh(true);
            }

            private void Refresh(bool notify)
            {
                builder.RunBinding(notify ? notificationAction : connectAction, "PathNotification");
            }

            private void RefreshCore(bool notify)
            {
                pending = true;
                if (refreshing)
                {
                    return;
                }

                refreshing = true;
                try
                {
                    var passes = 0;
                    while (pending && !disposed && builder.session.IsActive)
                    {
                        pending = false;
                        if (++passes > 32)
                        {
                            throw new InvalidOperationException("Binding source path did not stabilize after 32 synchronous updates.");
                        }

                        for (var i = 0; i < owners.Length && !disposed && builder.session.IsActive; ++i)
                        {
                            var next = path[i].Owner(builder.model);
                            if (ReferenceEquals(next, owners[i]))
                            {
                                continue;
                            }

                            var previous = owners[i];
                            owners[i] = null;
                            if (previous != null)
                            {
                                previous.PropertyChanged -= handlers[i];
                            }

                            if (disposed || !builder.session.IsActive)
                            {
                                break;
                            }

                            owners[i] = next;
                            if (next != null)
                            {
                                next.PropertyChanged += handlers[i];
                            }
                        }

                        if (!disposed && builder.session.IsActive && notify)
                        {
                            changed();
                        }
                    }
                }
                finally
                {
                    refreshing = false;
                    pending = false;
                }
            }
        }
    }
}
