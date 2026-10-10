using System;
using UnityEngine;

namespace MUI.UGUI
{
    public enum ViewHiddenMode
    {
        CanvasGroupOnly,
        DeactivateContent
    }

    public sealed partial class View
    {
        [SerializeField] private ViewHiddenMode hiddenMode;
        [SerializeField] private GameObject hiddenContentRoot;
        private GameObject suspendedContent;
        private bool applyingHiddenContent;
        private bool hiddenContentDirty;

        public ViewHiddenMode HiddenMode => hiddenMode;

        public GameObject HiddenContentRoot => hiddenContentRoot;

        /// <summary>显式选择隐藏时停用的内容子树；View 根节点始终由原有生命周期管理。</summary>
        public void ConfigureHiddenContent(ViewHiddenMode mode, GameObject contentRoot = null)
        {
            RequireAlive();
            ValidateHiddenContent(mode, contentRoot);
            hiddenMode = mode;
            hiddenContentRoot = contentRoot;
            ApplyGates();
        }

        private void ValidateHiddenContent(ViewHiddenMode mode, GameObject contentRoot)
        {
            if (mode != ViewHiddenMode.CanvasGroupOnly && mode != ViewHiddenMode.DeactivateContent)
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }
            if (mode == ViewHiddenMode.DeactivateContent && (contentRoot == null ||
                contentRoot == gameObject || !contentRoot.transform.IsChildOf(transform)))
            {
                throw new ArgumentException("停用内容模式必须指定 View 根节点下的内容子节点。", nameof(contentRoot));
            }
        }

        private void ApplyHiddenContentState()
        {
            if ((hiddenMode == ViewHiddenMode.CanvasGroupOnly && suspendedContent == null) || initializing || !IsAlive)
            {
                return;
            }
            hiddenContentDirty = true;
            if (applyingHiddenContent)
            {
                return;
            }
            applyingHiddenContent = true;
            try
            {
                while (hiddenContentDirty && IsAlive)
                {
                    hiddenContentDirty = false;
                    ValidateHiddenContent(hiddenMode, hiddenContentRoot);
                    var visible = retainingVisuals ? retainedVisible && exitVisible : hostVisible && localVisible;
                    var shouldSuspend = !visible && hiddenMode == ViewHiddenMode.DeactivateContent;
                    if (suspendedContent != null && (!shouldSuspend || suspendedContent != hiddenContentRoot))
                    {
                        var previous = suspendedContent;
                        suspendedContent = null;
                        // 只恢复框架停用的节点，不激活原本就处于停用状态的内容。
                        previous.SetActive(true);
                        hiddenContentDirty = true;
                        continue;
                    }
                    if (shouldSuspend && hiddenContentRoot.activeSelf)
                    {
                        suspendedContent = hiddenContentRoot;
                        hiddenContentRoot.SetActive(false);
                    }
                }
            }
            finally
            {
                applyingHiddenContent = false;
            }
        }
    }
}
