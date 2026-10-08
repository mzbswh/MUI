using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>原生控件适配器，只初始化一次，在最终释放时退订监听器。</summary>
    public abstract partial class Element : MonoBehaviour, IElement, IAccessibleElement, IDisposable
    {
        private readonly List<Action> cleanup = new List<Action>();
        private bool elementInitialized;
        private bool disposed;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool IsAlive
        {
            get
            {
                UnityMainThread.Require();
                return this != null && !disposed;
            }
        }

        public string Name
        {
            get
            {
                RequireAlive();
                var value = gameObject.name;
                const string suffix = "(Clone)";
                return value.EndsWith(suffix, StringComparison.Ordinal) ? value.Substring(0, value.Length - suffix.Length) : value;
            }
        }

        public bool Visible
        {
            get
            {
                RequireAlive();
                return gameObject.activeSelf;
            }

            set
            {
                RequireAlive();
                if (gameObject.activeSelf == value)
                {
                    return;
                }

                gameObject.SetActive(value);
                NotifyChanged();
            }
        }

        /// <summary>所属界面正在保留退役画面；异步状态提示应延后写入可见节点。</summary>
        protected bool IsVisualRetentionActive
        {
            get
            {
                if (!IsAlive)
                {
                    return false;
                }

                var owner = GetComponentInParent<View>(true);
                return owner != null && owner.IsRetainingVisuals;
            }
        }

        public void Initialize()
        {
            RequireAlive();
            if (elementInitialized)
            {
                return;
            }

            try
            {
                OnInitialize();
                elementInitialized = true;
            }
            catch (Exception failure)
            {
                try
                {
                    Dispose();
                }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException(failure, cleanupFailure);
                }

                throw;
            }
        }

        internal void EndVisualRetention() => OnVisualRetentionEnded();

        internal void ResetForCache()
        {
            RequireAlive();
            OnResetForCache();
        }

        /// <summary>
        /// 所属 View 的旧激活完全清理后解除自定义控件保存的模型、参数及活动引用。
        /// 此钩子不启动新工作；抛错会淘汰整个 View，后续仍执行最终 Dispose。
        /// </summary>
        protected virtual void OnResetForCache()
        {
        }

        /// <summary>显示保留结束后应用期间积累的表现状态；不会恢复旧激活。</summary>
        protected virtual void OnVisualRetentionEnded()
        {
        }

        protected virtual void OnInitialize()
        {
        }

        /// <summary>门控原生输入通知，但不抑制显式属性同步。</summary>
        protected bool CanReceiveInput(Selectable control)
        {
            if (!IsAlive || !isActiveAndEnabled || control == null || !control.isActiveAndEnabled || !control.IsInteractable())
            {
                return false;
            }

            // 查询当前所有者，池化元素可能在初始化后被移动。
            var owner = GetComponentInParent<View>(true);
            if (owner != null && !owner.IsInputEnabled)
            {
                return false;
            }

            var host = GetComponentInParent<UIHost>(true);
            return host == null || host.CanReceiveSharedPointerInput();
        }

        protected void OnDispose(Action callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            RequireAlive();
            cleanup.Add(callback);
        }

        protected T RequireComponent<T>()
                    where T : UnityEngine.Component
        {
            RequireAlive();
            var component = GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException($"{GetType().Name} requires {typeof(T).Name} on '{name}'.");
            }

            return component;
        }

        protected void RequireAlive()
        {
            if (!IsAlive)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        protected void NotifyChanged([CallerMemberName] string property = null)
        {
            UnityMainThread.Require();
            PropertyChanged?.Invoke(this, PropertyChangedEventArgsCache.Get(property));
        }

        public void Dispose()
        {
            UnityMainThread.Require();
            if (disposed)
            {
                return;
            }

            disposed = true;
            var errors = new List<Exception>();
            for (var i = cleanup.Count - 1; i >= 0; --i)
            {
                try
                {
                    cleanup[i]();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            cleanup.Clear();
            PropertyChanged = null;
            if (errors.Count > 0)
            {
                throw new AggregateException("Element cleanup failed.", errors);
            }
        }

        protected virtual void OnDestroy()
        {
            try
            {
                Dispose();
            }
            catch (Exception error)
            {
                UnityErrorLogging.Report(error);
            }
        }
    }
}
