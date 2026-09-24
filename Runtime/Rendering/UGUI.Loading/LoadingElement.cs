using System;
using System.Collections.Generic;
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
        private long sourceAssignmentVersion;

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

                var assignment = ++sourceAssignmentVersion;
                // 变更所有权前先读取，确保来源线程错误时保留原绑定。
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
                            throw new AggregateException("加载来源退订与显示清理均失败。", failure, renderFailure);
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
                    Action<LoadingSnapshot> nextSubscription = null;
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
                            throw new AggregateException("加载来源订阅与回滚失败。", failures);
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
                    throw new AggregateException("加载提示控件清理失败。", errors);
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
