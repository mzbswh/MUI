using System;
using System.Collections.Generic;
using MUI.Notifications;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>借用通知来源进行显示，不拥有来源，也不推进其时钟。</summary>
    [DisallowMultipleComponent]
    public sealed class NotificationElement : Element
    {
        [SerializeField]
        private GameObject indicator = null;
        [SerializeField]
        private Text message = null;
        [SerializeField]
        private Button dismissButton = null;
        private INotificationSource source;
        private Action<NotificationSnapshot> subscription;
        private long displayedId;
        private long renderVersion;
        private long sourceAssignmentVersion;

        public INotificationSource Source
        {
            get
            {
                RequireAlive();
                return source;
            }

            set
            {
                Initialize();
                if (ReferenceEquals(source, value))
                {
                    return;
                }

                var assignment = ++sourceAssignmentVersion;
                var snapshot = value == null ? default : value.Snapshot;
                if (!IsAlive || assignment != sourceAssignmentVersion)
                {
                    return;
                }

                try
                {
                    Detach();
                }
                catch (Exception failure)
                {
                    if (IsAlive && assignment == sourceAssignmentVersion && source == null)
                    {
                        try
                        {
                            Apply(default);
                        }
                        catch (Exception renderFailure)
                        {
                            throw new AggregateException("通知来源退订与显示清理均失败。", failure, renderFailure);
                        }
                    }

                    throw;
                }

                if (!IsAlive || assignment != sourceAssignmentVersion)
                {
                    return;
                }

                source = value;
                var versionBeforeSubscription = renderVersion;
                if (value != null)
                {
                    var expectedSource = value;
                    Action<NotificationSnapshot> nextSubscription = null;
                    nextSubscription = state =>
                    {
                        if (ReferenceEquals(source, expectedSource) &&
                            ReferenceEquals(subscription, nextSubscription))
                        {
                            Apply(state);
                        }
                    };
                    subscription = nextSubscription;
                    try
                    {
                        value.Changed += nextSubscription;
                    }
                    catch (Exception error)
                    {
                        var cleared = assignment == sourceAssignmentVersion &&
                            ReferenceEquals(source, value) && ReferenceEquals(subscription, nextSubscription);
                        if (cleared)
                        {
                            source = null;
                            subscription = null;
                        }

                        var failures = new List<Exception> { error };
                        try
                        {
                            value.Changed -= nextSubscription;
                        }
                        catch (Exception cleanupError)
                        {
                            failures.Add(cleanupError);
                        }

                        if (cleared && IsAlive && assignment == sourceAssignmentVersion &&
                            source == null && subscription == null)
                        {
                            try
                            {
                                Apply(default);
                            }
                            catch (Exception renderFailure)
                            {
                                failures.Add(renderFailure);
                            }
                        }

                        if (failures.Count > 1)
                        {
                            throw new AggregateException("通知来源订阅与回滚失败。", failures);
                        }

                        throw;
                    }

                    if (!IsAlive || assignment != sourceAssignmentVersion ||
                        !ReferenceEquals(source, value) || !ReferenceEquals(subscription, nextSubscription))
                    {
                        value.Changed -= nextSubscription;
                        return;
                    }
                }

                if (renderVersion == versionBeforeSubscription)
                {
                    Apply(snapshot);
                }

                if (IsAlive && assignment == sourceAssignmentVersion && ReferenceEquals(source, value))
                {
                    NotifyChanged();
                }
            }
        }

        protected override void OnInitialize()
        {
            if (indicator == null || indicator.transform == transform || !indicator.transform.IsChildOf(transform))
            {
                throw new InvalidOperationException("Notification indicator must be a strict child of its adapter.");
            }

            if (message == null || !message.transform.IsChildOf(indicator.transform))
            {
                throw new InvalidOperationException("Notification message must be inside its indicator.");
            }

            if (dismissButton != null && !dismissButton.transform.IsChildOf(indicator.transform))
            {
                throw new InvalidOperationException("Notification dismiss button must be inside its indicator.");
            }

            OnDispose(() =>
            {
                ++renderVersion;
                List<Exception> errors = null;
                try
                {
                    Detach();
                }
                catch (Exception error)
                {
                    errors = new List<Exception> { error };
                }

                source = null;
                displayedId = 0;
                if (dismissButton != null)
                {
                    try
                    {
                        dismissButton.onClick.RemoveListener(Dismiss);
                    }
                    catch (Exception error)
                    {
                        if (errors == null)
                        {
                            errors = new List<Exception>();
                        }

                        errors.Add(error);
                    }
                }

                if (indicator != null)
                {
                    try
                    {
                        indicator.SetActive(false);
                    }
                    catch (Exception error)
                    {
                        if (errors == null)
                        {
                            errors = new List<Exception>();
                        }

                        errors.Add(error);
                    }
                }

                if (errors != null)
                {
                    throw new AggregateException("通知控件清理失败。", errors);
                }
            });
            if (dismissButton != null)
            {
                dismissButton.onClick.AddListener(Dismiss);
            }

            indicator.SetActive(false);
        }

        private void Dismiss()
        {
            if (source == null || !CanReceiveInput(dismissButton))
            {
                return;
            }

            source.Dismiss(displayedId);
        }

        private void Apply(NotificationSnapshot snapshot)
        {
            if (!IsAlive || indicator == null)
            {
                return;
            }

            var version = ++renderVersion;
            displayedId = snapshot.Id;
            if (message != null)
            {
                message.text = snapshot.Current == null ? string.Empty : snapshot.Current.Message;
            }

            // 渲染期间原生回调可能替换来源或销毁当前元素。
            if (version != renderVersion || !IsAlive || indicator == null)
            {
                return;
            }

            indicator.SetActive(snapshot.IsVisible);
        }

        private void Detach()
        {
            var previousSource = source;
            var previousSubscription = subscription;
            source = null;
            subscription = null;
            if (previousSource != null && previousSubscription != null)
            {
                previousSource.Changed -= previousSubscription;
            }
        }
    }
}
