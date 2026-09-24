using System;
using MUI.Loading;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>借用加载来源进行显示；聚合、计时和输入所有权仍归来源所有者。</summary>
    [DisallowMultipleComponent]
    public sealed class LoadingElement : Element
    {
        [SerializeField]
        private GameObject indicator = null;
        [SerializeField]
        private Text message = null;
        [SerializeField]
        private Image progress = null;
        [SerializeField]
        private GameObject indeterminate = null;
        private ILoadingSource source;
        private Action<LoadingSnapshot> subscription;
        private long renderVersion;

        public ILoadingSource Source
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

                // 变更所有权前先读取，确保来源线程错误时保留原绑定。
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
                throw new InvalidOperationException("Loading indicator must be a strict child of the adapter.");
            }

            ValidateChild(message == null ? null : message.gameObject, "Message");
            ValidateChild(progress == null ? null : progress.gameObject, "Progress");
            ValidateChild(indeterminate, "Indeterminate");
            if (progress != null && progress.type != Image.Type.Filled)
            {
                throw new InvalidOperationException("Loading progress requires an Image with Filled type.");
            }

            if (progress != null && indeterminate != null && (progress.transform.IsChildOf(indeterminate.transform) || indeterminate.transform.IsChildOf(progress.transform)))
            {
                throw new InvalidOperationException("Progress and indeterminate nodes must be independent branches.");
            }

            if (message != null && ((progress != null && message.transform.IsChildOf(progress.transform)) || (indeterminate != null && message.transform.IsChildOf(indeterminate.transform))))
            {
                throw new InvalidOperationException("Loading message cannot be inside a conditionally hidden progress branch.");
            }

            OnDispose(() =>
            {
                ++renderVersion;
                Detach();
                source = null;
                if (indicator != null)
                {
                    indicator.SetActive(false);
                }
            });
            indicator.SetActive(false);
        }

        private void ValidateChild(GameObject child, string label)
        {
            if (child != null && (child == indicator || !child.transform.IsChildOf(indicator.transform)))
            {
                throw new InvalidOperationException(label + " must be a strict child of the loading indicator.");
            }
        }

        private void Apply(LoadingSnapshot snapshot)
        {
            if (!IsAlive || indicator == null)
            {
                return;
            }

            var version = ++renderVersion;
            if (message != null)
            {
                message.text = snapshot.Message ?? string.Empty;
            }

            if (!CanContinue(version))
            {
                return;
            }

            if (progress != null)
            {
                progress.fillAmount = (float)(snapshot.Progress ?? 0);
                if (!CanContinue(version) || progress == null)
                {
                    return;
                }

                progress.gameObject.SetActive(snapshot.Progress.HasValue);
            }

            if (!CanContinue(version))
            {
                return;
            }

            if (indeterminate != null)
            {
                indeterminate.SetActive(!snapshot.Progress.HasValue);
            }

            if (!CanContinue(version))
            {
                return;
            }

            indicator.SetActive(snapshot.IsVisible);
        }

        private bool CanContinue(long version) => IsAlive && indicator != null && version == renderVersion;

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
