using System;
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

                var snapshot = value == null ? default : value.Snapshot;
                Detach();
                source = value;
                if (value != null)
                {
                    var expectedSource = value;
                    subscription = state =>
                    {
                        if (ReferenceEquals(source, expectedSource))
                        {
                            Apply(state);
                        }
                    };
                    source.Changed += subscription;
                }

                Apply(snapshot);
                if (IsAlive)
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
                Detach();
                source = null;
                displayedId = 0;
                if (dismissButton != null)
                {
                    dismissButton.onClick.RemoveListener(Dismiss);
                }

                if (indicator != null)
                {
                    indicator.SetActive(false);
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
            if (source != null && subscription != null)
            {
                source.Changed -= subscription;
            }

            subscription = null;
        }
    }
}
